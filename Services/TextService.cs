using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
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

        public static async Task<string> GetClipboardTextAsync()
        {
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

        private static readonly object ModelUsageLogLock = new();

        internal static void LogModelAttempt(string operation, string modelName, string outcome, long elapsedMilliseconds)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model_usage.txt");
                lock (ModelUsageLogLock)
                {
                    if (!File.Exists(logPath))
                        File.WriteAllText(logPath, "timestamp\toperation\tmodel\toutcome\telapsed_ms" + Environment.NewLine);

                    string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
                    string entry = $"{timestamp}\t{operation}\t{modelName}\t{outcome}\t{elapsedMilliseconds}{Environment.NewLine}";
                    File.AppendAllText(logPath, entry);
                }
            }
            catch { }
        }

        // Updated prioritized model list with stable flash fallbacks
        internal static List<string> GetPrioritizedModels()
        {
            return new List<string>
            {
                "gemini-2.5-flash",
                "gemini-2.0-flash",
                "models/gemini-3.8-flash",
                "models/gemini-3.6-flash",
                "gemini-1.5-flash"
            };
        }

        /// <summary>
        /// Gemini'den serbest metin yerine ham yanıt (ör. sözlük için JSON) döndürür.
        /// Tüm model zincirini dener; hiçbiri başarılı olmazsa boş string döner.
        /// </summary>
        internal static Task<(string Text, string? UsedModel)> CallGeminiRawAsync(string prompt, string operation, int timeoutSeconds = 10)
            => CallGeminiRawInternalAsync(prompt, operation, timeoutSeconds);

        /// <summary>
        /// Gemini'den ham yanıt döndürür; yanında kullanılan modelin adını da verir
        /// (başarısız olursa UsedModel null kalır).
        /// </summary>
        private static async Task<(string Text, string? UsedModel)> CallGeminiRawInternalAsync(string prompt, string operation, int timeoutSeconds)
        {
            string? usedModel = null;
            if (string.IsNullOrWhiteSpace(prompt)) return ("", null);

            string apiKey = SettingsManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return ("", null);

            var prioritized = GetPrioritizedModels();
            foreach (var modelResourceName in prioritized)
            {
                var attemptTimer = System.Diagnostics.Stopwatch.StartNew();
                string modelUrl = modelResourceName.StartsWith("models/") ? modelResourceName : $"models/{modelResourceName}";
                string uri = $"https://generativelanguage.googleapis.com/v1beta/{modelUrl}:generateContent?key={apiKey}";
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                    var response = await HttpClient.PostAsync(uri, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"), cts.Token);
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using JsonDocument doc = JsonDocument.Parse(responseString);
                        string? text = doc.RootElement.GetProperty("candidates")[0]
                            .GetProperty("content").GetProperty("parts")[0]
                            .GetProperty("text").GetString();
                        LogModelAttempt(operation, modelResourceName, "success", attemptTimer.ElapsedMilliseconds);
                        return (text?.Trim() ?? "", modelResourceName);
                    }

                    LogModelAttempt(operation, modelResourceName, $"http-{(int)response.StatusCode}", attemptTimer.ElapsedMilliseconds);
                }
                catch
                {
                    LogModelAttempt(operation, modelResourceName, "exception", attemptTimer.ElapsedMilliseconds);
                }
            }

            return ("", null);
        }

        internal static TimeSpan GetModelRetryDelay(int retryCount)
        {
            return TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, Math.Min(retryCount, 3))));
        }

        private static async Task<string> CallGeminiAsync(string prompt, string originalText)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return originalText;

            string apiKey = SettingsManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Hata: GEMINI_API_KEY bulunamadı.";

            var prioritized = GetPrioritizedModels();
            string lastError = "";

            foreach (var modelResourceName in prioritized)
            {
                var attemptTimer = System.Diagnostics.Stopwatch.StartNew();
                string modelUrl = modelResourceName.StartsWith("models/") ? modelResourceName : $"models/{modelResourceName}";
                string uri = $"https://generativelanguage.googleapis.com/v1beta/{modelUrl}:generateContent?key={apiKey}";
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var response = await HttpClient.PostAsync(uri, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"), cts.Token);
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using JsonDocument doc = JsonDocument.Parse(responseString);
                        var textObj = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text");
                        LogModelAttempt("text-improvement", modelResourceName, "success", attemptTimer.ElapsedMilliseconds);
                        string res = textObj.GetString()?.Trim() ?? originalText;
                        if (res.StartsWith("\"\"\"") && res.EndsWith("\"\"\"")) res = res[3..^3].Trim();
                        return res;
                    }
                    else
                    {
                        lastError = $"{modelResourceName}: {response.StatusCode} - {responseString}";
                        LogModelAttempt("text-improvement", modelResourceName, $"http-{(int)response.StatusCode}", attemptTimer.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"{modelResourceName}: {ex.Message}";
                    LogModelAttempt("text-improvement", modelResourceName, "exception", attemptTimer.ElapsedMilliseconds);
                }
            }

            // Otomatik Yedek: Tüm modeller hata verirse veya 503 olursa ücretsiz kural motoru devreye girsin
            try
            {
                string freeFallback = FreeTextImprover.Improve(originalText);
                if (originalText.Trim() != freeFallback.Trim())
                {
                    return freeFallback;
                }
            }
            catch { }

            return $"Hiçbir model yanıt vermedi.\n\nSon Hata:\n{lastError}";
        }

        public static async Task<string> ImproveTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string prompt = "You are a professional text editor. Analyze the language of the following text. " +
                "Improve its grammar, spelling, punctuation, word order, and general flow while STRICTLY preserving its original language and meaning. " +
                "Do NOT translate it. Return ONLY the improved text without explanations, notes, intros, or markdown quotes.\n\n" +
                $"Text:\n\"\"\"\n{text}\n\"\"\"";

            return await CallGeminiAsync(prompt, text);
        }

        public static async Task<string> TranslateTextAsync(string text, string targetLanguage = "en")
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Tek kelime seçildiyse → otomatik sözlük modu (web sürümüyle aynı davranış).
            // Cümleler normal çeviriye gider; tek kelimeler kök analizi + anlam içerir.
            string? singleWord = DictionaryService.ExtractSingleWord(text);
            if (singleWord != null)
            {
                DictionaryEntry entry = await DictionaryService.LookupAsync(singleWord, targetLanguage);
                return DictionaryService.FormatEntry(entry);
            }

            bool isFrench = targetLanguage.Equals("fr", StringComparison.OrdinalIgnoreCase);
            bool isDutch = targetLanguage.Equals("nl", StringComparison.OrdinalIgnoreCase);

            string provider = SettingsManager.Current.TranslationProvider ?? "free";
            if (provider.Equals("free", StringComparison.OrdinalIgnoreCase))
            {
                return await FreeTranslationService.TranslateAsync(text, isDutch ? "nl" : (isFrench ? "fr" : "en"));
            }

            string targetLangName = isDutch ? "Dutch (Flemish / Nederlands)" : (isFrench ? "French" : "English");
            string prompt = $"You are a translator. Analyze the language of the text between triple quotes below.\n" +
                $"- If the text is in Turkish, translate it into natural, fluent {targetLangName}.\n" +
                $"- If the text is in {targetLangName} (or any other language), translate it into natural, fluent Turkish.\n" +
                "Rules: Preserve the original meaning, tone and line breaks. Do not add explanations, notes or quotes. Return ONLY the translated text.\n" +
                $"Text:\n\"\"\"\n{text}\n\"\"\"";

            return await CallGeminiAsync(prompt, text);
        }

        public static Task<string> TranslateFrenchTextAsync(string text) => TranslateTextAsync(text, "fr");
        public static Task<string> TranslateDutchTextAsync(string text) => TranslateTextAsync(text, "nl");
    }
}
