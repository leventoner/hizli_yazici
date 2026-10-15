using System;
using System.IO;
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
                    WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit Mono (Whisper compatible)
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
                    .WithLanguage("tr")
                    .Build();

                var sb = new StringBuilder();
                using (var fileStream = File.OpenRead(filePath))
                {
                    await foreach (var segment in processor.ProcessAsync(fileStream))
                    {
                        if (!string.IsNullOrWhiteSpace(segment.Text))
                        {
                            sb.Append(segment.Text).Append(' ');
                        }
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

        private static async Task<string> EnsureWhisperModelExistsAsync()
        {
            string[] searchPaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "ggml-tiny.bin"),
                Path.Combine(Directory.GetCurrentDirectory(), "models", "ggml-tiny.bin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HizliYazici", "models", "ggml-tiny.bin")
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 10_000_000)
                {
                    return path;
                }
            }

            // Model bulunamadıysa AppData içerisine otomatik indir
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HizliYazici", "models");
            Directory.CreateDirectory(targetDir);
            string targetPath = Path.Combine(targetDir, "ggml-tiny.bin");

            try
            {
                ActionHandler.ShowNotification?.Invoke("İndiriliyor", "Lokal Whisper ses modeli indiriliyor (~75MB, bir defalık)...", "#9b59b6");
                using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Tiny);
                using var fileStream = File.OpenWrite(targetPath);
                await modelStream.CopyToAsync(fileStream);
                return targetPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Model download error: {ex.Message}");
                return "";
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
                                    new { text = "Bu ses kaydını dinle ve konuşmayı eksiksiz olarak metne çevir. Sadece konuşulan Türkçe metni döndür, ekleme veya açıklama yapma." }
                                }
                            }
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
