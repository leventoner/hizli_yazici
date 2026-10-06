using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Globalization;
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
            catch (Exception ex)
            {
                Console.WriteLine($"[MODEL LOG] Kayıt yazılamadı: {ex.Message}");
            }
        }

        internal static System.Collections.Generic.List<string> GetPrioritizedModels()
        {
            return new System.Collections.Generic.List<string>
            {
                "models/gemini-3.6-flash",
                "models/gemini-3.5-flash",
                "models/gemini-3.8-flash",
                "models/gemini-3.7-flash"
            };
        }

        internal static TimeSpan GetModelRetryDelay(int retryCount)
        {
            return TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(retryCount, 5))));
        }

        internal static TimeSpan GetModelRetryDelay(string responseBody, int retryCount)
        {
            TimeSpan retryDelay = GetModelRetryDelay(retryCount);

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("details", out var details))
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.TryGetProperty("@type", out var type) &&
                            type.GetString()?.EndsWith("RetryInfo", StringComparison.Ordinal) == true &&
                            detail.TryGetProperty("retryDelay", out var delayValue))
                        {
                            string? delayText = delayValue.GetString();
                            if (delayText?.EndsWith("s", StringComparison.Ordinal) == true &&
                                double.TryParse(delayText[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                            {
                                return TimeSpan.FromSeconds(Math.Max(seconds, retryDelay.TotalSeconds));
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }

            return retryDelay;
        }

        internal static bool IsDailyQuotaExceeded(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody)) return false;

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("details", out var details))
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (!detail.TryGetProperty("@type", out var type) ||
                            type.GetString()?.EndsWith("QuotaFailure", StringComparison.Ordinal) != true ||
                            !detail.TryGetProperty("violations", out var violations))
                            continue;

                        foreach (var violation in violations.EnumerateArray())
                        {
                            if (violation.TryGetProperty("quotaId", out var quotaId) &&
                                quotaId.GetString()?.Contains("PerDay", StringComparison.OrdinalIgnoreCase) == true)
                                return true;
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }

            return false;
        }

        internal static bool IsRetryableModelFailure(int? statusCode, Exception? exception, string? responseBody = null)
        {
            if (statusCode.HasValue)
            {
                if (statusCode.Value == 429 && IsDailyQuotaExceeded(responseBody)) return false;
                return statusCode.Value == 408 || statusCode.Value == 429 || statusCode.Value >= 500;
            }

            return exception is OperationCanceledException || exception is HttpRequestException;
        }

        // Retry the same model order when every model fails for a transient reason.
        private static async Task<string> CallGeminiAsync(string prompt, string originalText)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return originalText;

            string apiKey = SettingsManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Hata: GEMINI_API_KEY bulunamadı.";

            var prioritized = GetPrioritizedModels();

            LogDebug($"İşlem başlatıldı. Denenecek model sayısı: {prioritized.Count}; tüm hatalar geçiciyse tekrar denenecek.");
            string lastError = "Henüz hiçbir model denenmedi.";
            int pass = 1;
            bool dailyQuotaReached = false;

            while (true)
            {
                bool allFailuresRetryable = true;
                TimeSpan retryDelay = GetModelRetryDelay(pass);
                LogDebug($"Model turu {pass} başladı.");
                foreach (var modelResourceName in prioritized)
                {
                    LogDebug($"Deneniyor: {modelResourceName}");
                    var attemptTimer = System.Diagnostics.Stopwatch.StartNew();
                    string uri = $"https://generativelanguage.googleapis.com/v1beta/{modelResourceName}:generateContent?key={apiKey}";
                    var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

                    try
                    {
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                        var response = await HttpClient.PostAsync(uri, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"), cts.Token);
                        string responseString = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            using (JsonDocument doc = JsonDocument.Parse(responseString))
                            {
                                var textObj = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text");
                                LogModelAttempt("text-improvement", modelResourceName, "success", attemptTimer.ElapsedMilliseconds);
                                LogDebug($"BAŞARILI: {modelResourceName}");
                                return textObj.GetString()?.Trim() ?? originalText;
                            }
                        }
                        else
                        {
                            bool dailyQuotaExceeded = IsDailyQuotaExceeded(responseString);
                            bool retryable = IsRetryableModelFailure((int)response.StatusCode, null, responseString);
                            dailyQuotaReached |= dailyQuotaExceeded;
                            allFailuresRetryable &= retryable;
                            if (retryable)
                            {
                                TimeSpan modelRetryDelay = GetModelRetryDelay(responseString, pass);
                                if (modelRetryDelay > retryDelay) retryDelay = modelRetryDelay;
                            }
                            lastError = $"{modelResourceName}: {response.StatusCode}\n{responseString}";
                            LogModelAttempt("text-improvement", modelResourceName, dailyQuotaExceeded ? "quota-exhausted" : $"http-{(int)response.StatusCode}", attemptTimer.ElapsedMilliseconds);
                            LogDebug($"HATA ({modelResourceName}): {response.StatusCode} - {responseString}");
                        }
                    }
                    catch (Exception ex)
                    {
                        bool retryable = IsRetryableModelFailure(null, ex);
                        allFailuresRetryable &= retryable;
                        lastError = $"{modelResourceName} İstisna: {ex.Message}";
                        LogModelAttempt("text-improvement", modelResourceName, ex is OperationCanceledException ? "timeout" : retryable ? "network-error" : "exception", attemptTimer.ElapsedMilliseconds);
                        LogDebug($"İSTİSNA ({modelResourceName}): {ex.Message}");
                    }
                }

                if (!allFailuresRetryable) break;

                pass++;
                LogDebug($"Dört model de geçici hata verdi. {retryDelay.TotalSeconds:0} saniye sonra aynı sırayla yeniden denenecek.");
                await Task.Delay(retryDelay);
            }

            if (dailyQuotaReached)
                return "Hata: Gemini günlük ücretsiz istek kotası dolmuş. Kota yenilenince tekrar deneyin veya Google AI Studio'da ücretlendirmeyi kontrol edin.";

            return $"Hiçbir model yanıt vermedi.\n\nAyrıntılar için ana dizindeki ai_debug.txt dosyasına bakın.\n\nSon Hata:\n{lastError}";
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

        public static async Task<string> ImproveTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string prompt = "You are a professional text editor. Analyze the language of the following text. " +
                "Improve its grammar, spelling, punctuation, word order, and general flow while STRICTLY preserving its original language and meaning. " +
                "Do NOT translate it. Return ONLY the improved text without explanations, notes, intros, or markdown quotes.\n\n" +
                $"Text:\n\"\"\"\n{text}\n\"\"\"";

            return await CallGeminiAsync(prompt, text);
        }

        // Çeviri özelliği: Metin Türkçe ise hedef yabancı dile (varsayılan İngilizce),
        // yabancı dildeyse Türkçeye çevirir.
        // Ayarlardaki translation_provider "free" ise API anahtarı gerektirmeyen ücretsiz
        // servisler (Google keyless + MyMemory) kullanılır; "gemini" ise Gemini API kullanılır.
        public static async Task<string> TranslateTextAsync(string text, string targetLanguage = "en")
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            bool isFrench = targetLanguage.Equals("fr", StringComparison.OrdinalIgnoreCase);

            string provider = SettingsManager.Current.TranslationProvider ?? "free";
            if (provider.Equals("free", StringComparison.OrdinalIgnoreCase))
            {
                return await FreeTranslationService.TranslateAsync(text, isFrench ? "fr" : "en");
            }

            string targetLangName = isFrench ? "French" : "English";
            string prompt = $"You are a translator. Analyze the language of the text between triple quotes below.\n" +
                $"- If the text is in Turkish, translate it into natural, fluent {targetLangName}.\n" +
                $"- If the text is in {targetLangName} (or any other language), translate it into natural, fluent Turkish.\n" +
                "Rules: Preserve the original meaning, tone and line breaks. Do not add explanations, notes or quotes. Return ONLY the translated text.\n" +
                $"Text:\n\"\"\"\n{text}\n\"\"\"";

            return await CallGeminiAsync(prompt, text);
        }

        public static Task<string> TranslateFrenchTextAsync(string text) => TranslateTextAsync(text, "fr");
    }
}
