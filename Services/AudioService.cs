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
                // Ensure any previous session is safely closed
                mciSendString("close recsound", null, 0, IntPtr.Zero);

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

        public static async Task<string> TranscribeAudioAsync(string filePath)
        {
            await Task.Delay(300);

            if (!File.Exists(filePath)) return "Hata: Kayıt dosyası bulunamadı.";

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < 500)
            {
                return "Ses algılanamadı (Kayıt çok kısa veya mikrofon sessiz).";
            }

            string apiKey = Managers.SettingsManager.Current.GeminiApiKey;
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
