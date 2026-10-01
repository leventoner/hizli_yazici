using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YaziciCS.Services
{
    public static class AudioService
    {
        private static readonly System.Net.Http.HttpClient SharedClient = new System.Net.Http.HttpClient();

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        public static async Task RecordAudioAsync(string tempFilePath, int durationSeconds)
        {
            try 
            {
                mciSendString("open new Type waveaudio Alias recsound", null, 0, IntPtr.Zero);
                mciSendString("set recsound bitspersample 16 samplespersec 16000 channels 1 alignment 2", null, 0, IntPtr.Zero);
                mciSendString("record recsound", null, 0, IntPtr.Zero);
                
                await Task.Delay(durationSeconds * 1000);
                
                mciSendString("stop recsound", null, 0, IntPtr.Zero);
                mciSendString($"save recsound \"{tempFilePath}\"", null, 0, IntPtr.Zero);
                mciSendString("close recsound", null, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Recording error: " + ex.Message);
            }
        }

        private static void LogDebug(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stt_debug.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n" + new string('-', 20) + "\n");
                Console.WriteLine($"[STT DEBUG] {message}");
            }
            catch { }
        }

        public static async Task<string> TranscribeAudioAsync(string filePath)
        {
            // Give the OS a moment to finish writing the file
            await Task.Delay(300);

            if (!File.Exists(filePath)) return "Hata: Kayıt dosyası bulunamadı.";

            string apiKey = Managers.SettingsManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Hata: Gemini API anahtarı bulunamadı (.env dosyasını kontrol edin).";

            var prioritized = TextService.GetPrioritizedModels();

            LogDebug($"Transcription started. Models to try: {prioritized.Count}; retry if all failures are transient.");

            try
            {
                byte[] audioData = File.ReadAllBytes(filePath);
                string base64Audio = Convert.ToBase64String(audioData);
                string lastError = "Henüz hiçbir model denenmedi.";
                bool dailyQuotaReached = false;

                int pass = 1;
                while (true)
                {
                    bool allFailuresRetryable = true;
                    TimeSpan retryDelay = TextService.GetModelRetryDelay(pass);
                    LogDebug($"Model round {pass} started.");
                    foreach (var modelResourceName in prioritized)
                    {
                        LogDebug($"Trying model: {modelResourceName}");
                        var attemptTimer = System.Diagnostics.Stopwatch.StartNew();
                        string uri = $"https://generativelanguage.googleapis.com/v1beta/{modelResourceName}:generateContent?key={apiKey}";

                        var requestBody = new
                        {
                            contents = new[]
                            {
                                new
                                {
                                    parts = new object[]
                                    {
                                        new { inlineData = new { mimeType = "audio/wav", data = base64Audio } },
                                        new { text = "Bu sesi metne çevir. Sadece metni döndür, ekleme yapma. Dil muhtemelen Türkçedir." }
                                    }
                                }
                            }
                        };

                        try
                        {
                            string jsonBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
                            var content = new System.Net.Http.StringContent(jsonBody, Encoding.UTF8, "application/json");
                            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                            var response = await SharedClient.PostAsync(uri, content, cts.Token);
                            string respText = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                using (var doc = System.Text.Json.JsonDocument.Parse(respText))
                                {
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                                    {
                                        var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                                        TextService.LogModelAttempt("speech-transcription", modelResourceName, "success", attemptTimer.ElapsedMilliseconds);
                                        LogDebug($"SUCCESS with {modelResourceName}");
                                        return text?.Trim() ?? "Ses anlaşılamadı.";
                                    }
                                }

                                allFailuresRetryable = false;
                                lastError = $"{modelResourceName}: API boş yanıt döndürdü.";
                                TextService.LogModelAttempt("speech-transcription", modelResourceName, "empty-response", attemptTimer.ElapsedMilliseconds);
                            }
                            else
                            {
                                bool dailyQuotaExceeded = TextService.IsDailyQuotaExceeded(respText);
                                bool retryable = TextService.IsRetryableModelFailure((int)response.StatusCode, null, respText);
                                dailyQuotaReached |= dailyQuotaExceeded;
                                allFailuresRetryable &= retryable;
                                if (retryable)
                                {
                                    TimeSpan modelRetryDelay = TextService.GetModelRetryDelay(respText, pass);
                                    if (modelRetryDelay > retryDelay) retryDelay = modelRetryDelay;
                                }
                                lastError = $"{modelResourceName}: {response.StatusCode} - {respText}";
                                TextService.LogModelAttempt("speech-transcription", modelResourceName, dailyQuotaExceeded ? "quota-exhausted" : $"http-{(int)response.StatusCode}", attemptTimer.ElapsedMilliseconds);
                                LogDebug($"FAILED {modelResourceName}: {lastError}");
                            }
                        }
                        catch (Exception ex)
                        {
                            bool retryable = TextService.IsRetryableModelFailure(null, ex);
                            allFailuresRetryable &= retryable;
                            lastError = $"{modelResourceName} İstisna: {ex.Message}";
                            TextService.LogModelAttempt("speech-transcription", modelResourceName, ex is OperationCanceledException ? "timeout" : retryable ? "network-error" : "exception", attemptTimer.ElapsedMilliseconds);
                            LogDebug($"EXCEPTION {modelResourceName}: {ex.Message}");
                        }
                    }

                    if (!allFailuresRetryable) break;

                    pass++;
                    LogDebug($"All four models returned transient failures. Retrying in {retryDelay.TotalSeconds:0} seconds in the same order.");
                    await Task.Delay(retryDelay);
                }
                if (dailyQuotaReached)
                    return "Hata: Gemini günlük ücretsiz istek kotası dolmuş. Kota yenilenince tekrar deneyin veya Google AI Studio'da ücretlendirmeyi kontrol edin.";

                return $"Ses anlaşılamadı.\n\nAyrıntılar için stt_debug.txt dosyasına bakın.\n\nSon Hata: {lastError}";
            }
            catch (Exception ex)
            {
                return "Hata: " + ex.Message;
            }
        }
    }
}
