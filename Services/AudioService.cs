using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;
using YaziciCS.Managers;

namespace YaziciCS.Services
{
    public static class AudioService
    {
        private static readonly System.Net.Http.HttpClient SharedClient = new System.Net.Http.HttpClient();
        private static CancellationTokenSource? _activeRecordingCts;
        private static readonly object _recordLock = new();

        private static WhisperFactory? _cachedWhisperFactory;
        private static string? _cachedWhisperModelPath;
        private static readonly object _whisperLock = new();

        public static bool IsRecording { get; private set; } = false;

        public static void StopRecording()
        {
            lock (_recordLock)
            {
                if (IsRecording && _activeRecordingCts != null && !_activeRecordingCts.IsCancellationRequested)
                {
                    _activeRecordingCts.Cancel();
                }
            }
        }

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder? lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        public static async Task<bool> RecordAudioAsync(string tempFilePath, int durationSeconds)
        {
            lock (_recordLock)
            {
                _activeRecordingCts?.Dispose();
                _activeRecordingCts = new CancellationTokenSource();
                IsRecording = true;
            }

            var token = _activeRecordingCts.Token;
            bool recordedWithNAudio = false;

            try
            {
                recordedWithNAudio = await RecordWithNAudioAsync(tempFilePath, durationSeconds, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NAudio recording failed, falling back to MCI: {ex.Message}");
            }

            if (!recordedWithNAudio)
            {
                await RecordWithMciAsync(tempFilePath, durationSeconds, token);
            }

            lock (_recordLock)
            {
                IsRecording = false;
            }

            return File.Exists(tempFilePath) && new FileInfo(tempFilePath).Length > 1000;
        }

        private static async Task<bool> RecordWithNAudioAsync(string tempFilePath, int durationSeconds, CancellationToken token)
        {
            WaveInEvent? waveIn = null;
            WaveFileWriter? writer = null;

            try
            {
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit Mono (Whisper uyumlu)
                    BufferMilliseconds = 50,  // Daha kısa buffer → daha sık veri toplama → daha pürüzsüz kayıt
                    DeviceNumber = 0          // Varsayılan mikrofon
                };

                writer = new WaveFileWriter(tempFilePath, waveIn.WaveFormat);

                waveIn.DataAvailable += (s, e) =>
                {
                    try
                    {
                        writer.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                    catch { }
                };

                waveIn.StartRecording();

                int delayMs = 100;
                int totalSteps = (durationSeconds * 1000) / delayMs;

                for (int i = 0; i < totalSteps; i++)
                {
                    if (token.IsCancellationRequested) break;
                    await Task.Delay(delayMs);
                }

                waveIn.StopRecording();
                await Task.Delay(100);

                writer.Flush();
                writer.Dispose();
                writer = null;

                waveIn.Dispose();
                waveIn = null;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RecordWithNAudioAsync error: {ex.Message}");
                return false;
            }
            finally
            {
                writer?.Dispose();
                waveIn?.Dispose();
            }
        }

        private static async Task RecordWithMciAsync(string tempFilePath, int durationSeconds, CancellationToken token)
        {
            try
            {
                mciSendString("close recsound", null, 0, IntPtr.Zero);
                mciSendString("open new Type waveaudio Alias recsound", null, 0, IntPtr.Zero);
                mciSendString("set recsound bitspersample 16 samplespersec 16000 channels 1 alignment 2", null, 0, IntPtr.Zero);
                mciSendString("record recsound", null, 0, IntPtr.Zero);

                int delayMs = 100;
                int totalSteps = (durationSeconds * 1000) / delayMs;

                for (int i = 0; i < totalSteps; i++)
                {
                    if (token.IsCancellationRequested) break;
                    await Task.Delay(delayMs);
                }

                mciSendString("stop recsound", null, 0, IntPtr.Zero);
                mciSendString($"save recsound \"{tempFilePath}\"", null, 0, IntPtr.Zero);
                mciSendString("close recsound", null, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RecordWithMciAsync error: " + ex.Message);
            }
        }

        public static async Task<string> TranscribeAudioAsync(string filePath)
        {
            await Task.Delay(200);

            if (!File.Exists(filePath)) return "Hata: Kayıt dosyası bulunamadı.";

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < 1000)
            {
                return "Ses algılanamadı (Kayıt çok kısa veya mikrofon sessiz).";
            }

            // Ses seviyesi ön kontrolü — sessiz kayıtları erken tespit et
            float avgLevel = AnalyzeAudioLevel(filePath);
            if (avgLevel < 50) // Neredeyse tamamen sessiz (16-bit max = 32768)
            {
                return "Ses algılanamadı (Mikrofon çok sessiz veya kapalı — mikrofon ayarlarını kontrol edin).";
            }

            string provider = SettingsManager.Current.SttProvider ?? "local";

            if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
            {
                string geminiResult = await TranscribeWithGeminiAsync(filePath);
                if (!string.IsNullOrEmpty(geminiResult) && 
                    !geminiResult.StartsWith("Hata:") && 
                    !geminiResult.StartsWith("Ses anlaşılamadı (Modeller yoğun"))
                {
                    return geminiResult;
                }

                // Otomatik lokal Whisper yedeği
                ActionHandler.ShowNotification?.Invoke("Bilgi", "Gemini kotası/bağlantı hatası nedeniyle lokal Whisper kullanılıyor...", "#3498db");
            }

            return await TranscribeWithWhisperAsync(filePath);
        }

        /// <summary>
        /// WAV dosyasının ortalama ses seviyesini (RMS) analiz eder.
        /// Sessiz kayıtları erken tespit etmek için kullanılır.
        /// </summary>
        private static float AnalyzeAudioLevel(string filePath)
        {
            try
            {
                using var reader = new WaveFileReader(filePath);
                long totalSamples = 0;
                double sumSquares = 0;

                // En fazla ilk 5 saniyelik veriyi analiz et (performans için)
                int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
                int maxBytes = reader.WaveFormat.SampleRate * 5 * bytesPerSample * reader.WaveFormat.Channels;
                byte[] buffer = new byte[4096];
                int totalBytesRead = 0;

                while (totalBytesRead < maxBytes)
                {
                    int bytesRead = reader.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    totalBytesRead += bytesRead;

                    for (int i = 0; i + 1 < bytesRead; i += 2)
                    {
                        short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                        sumSquares += (double)sample * sample;
                        totalSamples++;
                    }
                }

                if (totalSamples == 0) return 0;
                return (float)Math.Sqrt(sumSquares / totalSamples);
            }
            catch
            {
                return float.MaxValue; // Analiz başarısızsa kayda devam et
            }
        }

        // Türkçe için Whisper başlangıç prompt'u — model için dil bağlamı sağlar
        private const string TurkishInitialPrompt =
            "Bu ses kaydı Türkçe konuşma içermektedir. " +
            "Türkçe kelimeler: merhaba, evet, hayır, tamam, lütfen, teşekkür ederim.";

        public static async Task<string> TranscribeWithWhisperAsync(string filePath)
        {
            try
            {
                string modelPath = await EnsureWhisperModelExistsAsync();
                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    return "Hata: Lokal Whisper ses tanıma modeli yüklenemedi.";
                }

                WhisperFactory factory;
                lock (_whisperLock)
                {
                    if (_cachedWhisperFactory == null || _cachedWhisperModelPath != modelPath)
                    {
                        _cachedWhisperFactory?.Dispose();
                        _cachedWhisperFactory = WhisperFactory.FromPath(modelPath);
                        _cachedWhisperModelPath = modelPath;
                    }
                    factory = _cachedWhisperFactory;
                }

                using var processor = factory.CreateBuilder()
                    .WithLanguage("tr")                    // Dili Türkçe'ye kilitle (auto-detect'i devre dışı bırak)
                    .WithPrompt(TurkishInitialPrompt)      // Türkçe bağlam ipucu ver
                    .WithNoSpeechThreshold(0.5f)           // Sessizlik segmentlerini filtrele (0-1, düşük = daha agresif filtre)
                    .WithTemperature(0.0f)                 // Deterministik çıktı için sıcaklığı sıfırla (daha az halüsinasyon)
                    .Build();

                var sb = new StringBuilder();
                using (var fileStream = File.OpenRead(filePath))
                {
                    await foreach (var segment in processor.ProcessAsync(fileStream))
                    {
                        string segText = segment.Text?.Trim() ?? "";

                        // Whisper'ın sık ürettiği sahte/gürültü segmentlerini filtrele
                        if (string.IsNullOrWhiteSpace(segText)) continue;
                        if (IsWhisperHallucination(segText)) continue;

                        sb.Append(segText).Append(' ');
                    }
                }

                string transcribed = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(transcribed))
                {
                    return "Ses algılanamadı (Konuşma net anlaşılamadı veya mikrofon sessiz).";
                }

                return transcribed;
            }
            catch (Exception ex)
            {
                return "Lokal Ses Tanıma Hatası: " + ex.Message;
            }
        }

        /// <summary>
        /// Whisper'ın sessizlikte veya gürültülü ortamda ürettiği
        /// yaygın halüsinasyon kalıplarını tespit eder.
        /// </summary>
        private static bool IsWhisperHallucination(string text)
        {
            // Tamamı noktalama/boşluk mı?
            if (text.All(c => char.IsPunctuation(c) || char.IsWhiteSpace(c))) return true;

            // Whisper'ın sessizlikte ürettiği bilinen sahte Türkçe/İngilizce ifadeler
            string[] knownHallucinations =
            {
                "teşekkür ederim.", "teşekkürler.", "altyazı:", "altyazılar",
                "thanks for watching", "thank you for watching", "subtitles by",
                "www.", ".com", "çeviri:", "[müzik]", "[music]", "(müzik)",
                "subscribe", "abone ol"
            };

            string lower = text.ToLowerInvariant().Trim();
            foreach (var h in knownHallucinations)
            {
                if (lower == h || lower == h.TrimEnd('.')) return true;
            }

            return false;
        }

        private static async Task<string> EnsureWhisperModelExistsAsync()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataModels = Path.Combine(appData, "HizliYazici", "models");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string cwd = Directory.GetCurrentDirectory();

            // Model kalite sırası: small > base > tiny (small = en iyi Türkçe tanıma, ~242MB)
            var modelPriority = new[]
            {
                ("ggml-small.bin", 200_000_000L, GgmlType.Small),
                ("ggml-base.bin",   70_000_000L, GgmlType.Base),
                ("ggml-tiny.bin",   10_000_000L, GgmlType.Tiny),
            };

            // Önce mevcut modelleri ara (kalite sırasına göre)
            foreach (var (fileName, minSize, _) in modelPriority)
            {
                string[] searchPaths =
                {
                    Path.Combine(baseDir, "models", fileName),
                    Path.Combine(cwd, "models", fileName),
                    Path.Combine(appDataModels, fileName),
                };
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path) && new FileInfo(path).Length > minSize)
                        return path;
                }
            }

            // Hiç model yoksa: small'ı indir (en iyi Türkçe kalitesi)
            Directory.CreateDirectory(appDataModels);
            string targetPath = Path.Combine(appDataModels, "ggml-small.bin");

            try
            {
                ActionHandler.ShowNotification?.Invoke(
                    "İndiriliyor",
                    "Türkçe için daha iyi Whisper modeli indiriliyor (~242MB, bir defalık)...",
                    "#9b59b6");
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Small);
                using var fileStream = File.OpenWrite(targetPath);
                await modelStream.CopyToAsync(fileStream);
                return targetPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Small model download error: {ex.Message}");

                // Small indirilemezse tiny'i dene (fallback)
                string tinyPath = Path.Combine(appDataModels, "ggml-tiny.bin");
                try
                {
                    ActionHandler.ShowNotification?.Invoke(
                        "İndiriliyor",
                        "Yedek Whisper modeli indiriliyor (~75MB)...",
                        "#e67e22");
                    using var tinyStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Tiny);
                    using var tinyFile = File.OpenWrite(tinyPath);
                    await tinyStream.CopyToAsync(tinyFile);
                    return tinyPath;
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Tiny model download error: {ex2.Message}");
                    return "";
                }
            }
        }

        public static async Task<string> TranscribeWithGeminiAsync(string filePath)
        {
            string apiKey = SettingsManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Hata: Gemini API anahtarı bulunamadı (.env veya Ayarlar kontrol edin).";

            var prioritized = TextService.GetPrioritizedModels();

            try
            {
                byte[] audioData = File.ReadAllBytes(filePath);
                string base64Audio = Convert.ToBase64String(audioData);
                string lastError = "";

                foreach (var modelResourceName in prioritized)
                {
                    string modelUrl = modelResourceName.StartsWith("models/") ? modelResourceName : $"models/{modelResourceName}";
                    string uri = $"https://generativelanguage.googleapis.com/v1beta/{modelUrl}:generateContent?key={apiKey}";

                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                parts = new object[]
                                {
                                    new { inlineData = new { mimeType = "audio/wav", data = base64Audio } },
                                    new
                                    {
                                        text =
                                            "Bu ses kaydında Türkçe konuşma var. " +
                                            "Ses kaydındaki Türkçe konuşmayı kelimesi kelimesine ve eksiksiz metne çevir. " +
                                            "SADECE konuşulan metni yaz — başka hiçbir şey ekleme, açıklama yapma, dil belirtme. " +
                                            "Eğer ses yoksa veya anlaşılmıyorsa boş string döndür."
                                    }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0,       // Deterministik çıktı
                            topP = 1,
                            topK = 1
                        }
                    };

                    try
                    {
                        string jsonBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
                        var content = new System.Net.Http.StringContent(jsonBody, Encoding.UTF8, "application/json");
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                        var response = await SharedClient.PostAsync(uri, content, cts.Token);
                        string respText = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(respText);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                            {
                                var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                                return text?.Trim() ?? "Ses anlaşılamadı.";
                            }
                        }
                        else
                        {
                            lastError = $"{modelResourceName}: {(int)response.StatusCode}";
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = $"{modelResourceName}: {ex.Message}";
                    }
                }

                return $"Ses anlaşılamadı (Modeller yoğun: {lastError}). Lütfen tekrar deneyin.";
            }
            catch (Exception ex)
            {
                return "Hata: " + ex.Message;
            }
        }
    }
}
