using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using YaziciCS.Managers;

namespace YaziciCS.Services
{
    public static class TextService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 2;

        public static async Task<string> GetClipboardTextAsync()
        {
            // Retry loop for OpenClipboard
            for (int i = 0; i < 15; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
                        if (hGlobal != IntPtr.Zero)
                        {
                            IntPtr lpstr = GlobalLock(hGlobal);
                            if (lpstr != IntPtr.Zero)
                            {
                                string text = System.Runtime.InteropServices.Marshal.PtrToStringUni(lpstr);
                                GlobalUnlock(hGlobal);
                                return text ?? "";
                            }
                        }
                    }
                    finally { CloseClipboard(); }
                }
                await Task.Delay(50);
            }
            return "";
        }

        public static async Task SetClipboardTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Retry loop for OpenClipboard
            for (int i = 0; i < 15; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();
                        IntPtr hGlobal = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(text);
                        SetClipboardData(CF_UNICODETEXT, hGlobal);
                        return;
                    }
                    finally { CloseClipboard(); }
                }
                await Task.Delay(50);
            }
        }

        public static System.Collections.Generic.List<string> AvailableModels = new System.Collections.Generic.List<string>();
        public static bool DiscoveryAttempted = false;
        private static string _lastSuccessfulModel = ""; // Cache for the working model

        public static async Task DiscoverModelsAsync(string apiKey)
        {
            if (DiscoveryAttempted) return;
            DiscoveryAttempted = true;

            Console.WriteLine("[AI] Dinamik model listesi çekiliyor...");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await HttpClient.GetAsync(url, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("models", out var models))
                        {
                            foreach (var model in models.EnumerateArray())
                            {
                                string name = model.GetProperty("name").GetString() ?? "";
                                if (string.IsNullOrEmpty(name)) continue;

                                bool supportsGenerate = false;
                                if (model.TryGetProperty("supportedGenerationMethods", out var methods))
                                {
                                    foreach (var method in methods.EnumerateArray())
                                    {
                                        if (method.GetString() == "generateContent")
                                        {
                                            supportsGenerate = true;
                                            break;
                                        }
                                    }
                                }

                                if (supportsGenerate)
                                {
                                    AvailableModels.Add(name);
                                }
                            }
                        }
                    }
                    watch.Stop();
                    Console.WriteLine($"[AI] {AvailableModels.Count} adet uyumlu model bulundu. ({watch.ElapsedMilliseconds}ms)");
                }
                else
                {
                    Console.WriteLine($"[AI] Keşif hatası: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AI] Keşif sırasında hata: " + ex.Message);
            }
        }

        private static void LogDebug(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_debug.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n" + new string('-', 20) + "\n");
                Console.WriteLine($"[DEBUG] {message}");
            }
            catch { }
        }

        public static async Task<string> ImproveTextAsync(string text, bool autoDetectLanguage = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            string apiKey = SettingsManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Hata: GEMINI_API_KEY bulunamadı.";

            if (!DiscoveryAttempted) await DiscoverModelsAsync(apiKey);

            string prompt = autoDetectLanguage 
                ? $"Improve the grammar, spelling, word order, and general flow of the following text in its original language. Return only the corrected text. Text: {text}"
                : $"Aşağıdaki Türkçe metni dilbilgisi, imla, kelime sırası ve genel akıcılık açısından iyileştir. Sadece düzeltilmiş metni döndür. Metin: {text}";

            var prioritized = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(_lastSuccessfulModel)) prioritized.Add(_lastSuccessfulModel);
            foreach (var m in AvailableModels) if (m.Contains("flash") && !m.Contains("experimental") && !prioritized.Contains(m)) prioritized.Add(m);
            foreach (var m in AvailableModels) if (m.Contains("pro") && !m.Contains("experimental") && !prioritized.Contains(m)) prioritized.Add(m);
            foreach (var m in AvailableModels) if (!prioritized.Contains(m)) prioritized.Add(m);

            if (prioritized.Count == 0) prioritized.AddRange(new[] { "models/gemini-1.5-flash", "models/gemini-pro" });
            if (prioritized.Count > 10) prioritized = prioritized.GetRange(0, 10);

            LogDebug($"İşlem başlatıldı. Denenecek model sayısı: {prioritized.Count}");
            string lastError = "Henüz hiçbir model denenmedi.";

            foreach (var modelResourceName in prioritized)
            {
                LogDebug($"Deneniyor: {modelResourceName}");
                string uri = $"https://generativelanguage.googleapis.com/v1beta/{modelResourceName}:generateContent?key={apiKey}";
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                
                try
                {
                    int timeoutSec = modelResourceName.Contains("flash") || modelResourceName == _lastSuccessfulModel ? 8 : 15;
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
                    
                    var response = await HttpClient.PostAsync(uri, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"), cts.Token);
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using (JsonDocument doc = JsonDocument.Parse(responseString))
                        {
                            var textObj = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text");
                            _lastSuccessfulModel = modelResourceName;
                            LogDebug($"BAŞARILI: {modelResourceName}");
                            return textObj.GetString()?.Trim() ?? text;
                        }
                    }
                    else
                    {
                        lastError = $"{modelResourceName}: {response.StatusCode}\n{responseString}";
                        LogDebug($"HATA ({modelResourceName}): {response.StatusCode} - {responseString}");
                        if (modelResourceName == _lastSuccessfulModel) _lastSuccessfulModel = "";
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"{modelResourceName} İstisna: {ex.Message}";
                    LogDebug($"İSTİSNA ({modelResourceName}): {ex.Message}");
                }
            }

            return $"Hiçbir model yanıt vermedi.\n\nAyrıntılar için ana dizindeki ai_debug.txt dosyasına bakın.\n\nSon Hata:\n{lastError}";
        }
    }
}
