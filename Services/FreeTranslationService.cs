using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YaziciCS.Managers;

namespace YaziciCS.Services
{
    public static class FreeTranslationService
    {
        private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(6),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        });
        private const int MyMemoryMaxChunkBytes = 450;

        public static async Task<string> TranslateAsync(string text, string foreignLang = "en")
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string target = DetectTargetLanguage(text, foreignLang);

            GoogleTranslationResult? googleResult = await TryGoogleKeylessAsync(text, target);

            string? preferredTarget = GetPreferredTargetLanguage(target, googleResult, foreignLang);
            if (preferredTarget != null)
            {
                target = preferredTarget;
                googleResult = await TryGoogleKeylessAsync(text, target);
            }

            if (!string.IsNullOrEmpty(googleResult?.TranslatedText)) return googleResult.TranslatedText;

            string? result = await TryMyMemoryAsync(text, target);
            if (!string.IsNullOrEmpty(result)) return result!;

            return "Hata: Ücretsiz çeviri servislerine ulaşılamadı. İnternet bağlantınızı kontrol edin " +
                   "veya Ayarlar'dan çeviri sağlayıcısını Gemini (API anahtarlı) olarak seçin.";
        }

        public static Task<string> TranslateFrenchAsync(string text) => TranslateAsync(text, "fr");
        public static Task<string> TranslateDutchAsync(string text) => TranslateAsync(text, "nl");

        internal static string DetectTargetLanguage(string text, string foreignLang = "en")
        {
            if (foreignLang.Equals("fr", StringComparison.OrdinalIgnoreCase))
            {
                if (TurkishDistinctCharsRegex.IsMatch(text)) return "fr";
                if (FrenchCharsRegex.IsMatch(text) || LooksFrench(text)) return "tr";
                return LooksTurkish(text) ? "fr" : "tr";
            }

            if (foreignLang.Equals("nl", StringComparison.OrdinalIgnoreCase))
            {
                if (TurkishDistinctCharsRegex.IsMatch(text)) return "nl";
                if (LooksDutch(text)) return "tr";
                return LooksTurkish(text) ? "nl" : "tr";
            }

            return LooksTurkish(text) ? foreignLang : "tr";
        }

        internal static string? GetPreferredTargetLanguage(
            string currentTarget,
            GoogleTranslationResult? googleResult,
            string foreignLang = "en")
        {
            string sourceLanguage = (googleResult?.DetectedSourceLanguage ?? "").Trim();
            if (sourceLanguage.Length == 0) return null;

            if (sourceLanguage.Equals("tr", StringComparison.OrdinalIgnoreCase))
            {
                return currentTarget.Equals(foreignLang, StringComparison.OrdinalIgnoreCase) ? null : foreignLang;
            }

            if (sourceLanguage.Equals(foreignLang, StringComparison.OrdinalIgnoreCase))
            {
                return currentTarget.Equals("tr", StringComparison.OrdinalIgnoreCase) ? null : "tr";
            }

            if (!currentTarget.Equals("tr", StringComparison.OrdinalIgnoreCase))
            {
                return "tr";
            }

            return null;
        }

        private static readonly Regex TurkishCharsRegex = new Regex("[çğıöşüÇĞİÖŞÜ]", RegexOptions.Compiled);
        private static readonly Regex TurkishDistinctCharsRegex = new Regex("[ğĞıİşŞ]", RegexOptions.Compiled);
        private static readonly Regex FrenchCharsRegex = new Regex("[éèêëàâîïôùûÉÈÊËÀÂÎÏÔÙÛœæŒÆ]", RegexOptions.Compiled);

        private static readonly HashSet<string> FrenchStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "le", "la", "les", "de", "des", "du", "un", "une", "et", "est", "en", "que", "qui",
            "dans", "pour", "pas", "sur", "ce", "cette", "ces", "avec", "vous", "nous", "ils", "elle", "il", "je", "tu"
        };

        private static readonly HashSet<string> DutchStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "de", "het", "een", "en", "van", "ik", "te", "dat", "die", "in", "is", "op", "niet", "met",
            "zijn", "voor", "maar", "om", "ook", "je", "er", "ze", "we", "als", "bij", "wij", "jij",
            "hebben", "naar", "over", "wat", "mijn", "geen", "dit", "uit", "wel", "hoe", "zo", "door"
        };

        internal static bool LooksFrench(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (FrenchCharsRegex.IsMatch(text)) return true;

            string[] words = Regex.Split(text.ToLowerInvariant(), "[^a-zçéèêëàâîïôùûœæ]+")
                                  .Where(w => w.Length > 0)
                                  .ToArray();
            if (words.Length == 0) return false;

            int hits = words.Count(w => FrenchStopWords.Contains(w));
            return hits >= 2 || (words.Length <= 3 && hits >= 1);
        }

        internal static bool LooksDutch(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            string lower = text.ToLowerInvariant();
            if (lower.Contains("ij") || lower.Contains("oe") || lower.Contains("sch"))
            {
                // Common Dutch digraphs
            }

            string[] words = Regex.Split(lower, "[^a-z]+")
                                  .Where(w => w.Length > 0)
                                  .ToArray();
            if (words.Length == 0) return false;

            int hits = words.Count(w => DutchStopWords.Contains(w));
            return hits >= 2 || (words.Length <= 3 && hits >= 1);
        }

        private static readonly HashSet<string> TurkishStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ve", "bir", "bu", "için", "ile", "ama", "fakat", "çok", "daha", "gibi", "olarak",
            "de", "da", "ki", "mi", "mı", "mu", "mü", "ne", "nasıl", "neden", "çünkü",
            "ben", "sen", "biz", "siz", "onlar", "değil", "var", "yok", "her", "kadar", "sonra"
        };

        internal static bool LooksTurkish(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (TurkishCharsRegex.IsMatch(text)) return true;

            string[] words = Regex.Split(text.ToLowerInvariant(), "[^a-zçğıöşü]+")
                                  .Where(w => w.Length > 0)
                                  .ToArray();
            if (words.Length == 0) return false;

            int hits = words.Count(w => TurkishStopWords.Contains(w));
            return hits >= 2 || (words.Length <= 3 && hits >= 1);
        }

        internal sealed class GoogleTranslationResult
        {
            public string? TranslatedText { get; init; }
            public string? DetectedSourceLanguage { get; init; }
        }

        /// <summary>
        /// Çeviri metnindeki control karakterleri ve nesne yer tutucularını temizler.
        /// Bu karakterler UI'da "OBJ" kutucukları olarak görünür.
        /// </summary>
        private static readonly Regex ControlCharsRegex = new Regex(
            "[\uFFFC\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F\u200B-\u200F\u2028-\u202E\u2060-\u2064\uFEFF]",
            RegexOptions.Compiled);

        private static string SanitizeTranslatedText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return ControlCharsRegex.Replace(text, "");
        }

        private static async Task<GoogleTranslationResult?> TryGoogleKeylessAsync(string text, string target)
        {
            try
            {
                string uri = "https://clients5.google.com/translate_a/t" +
                             "?client=dict-chrome-ex" +
                             "&sl=auto" +
                             "&tl=" + Uri.EscapeDataString(target) +
                             "&q=" + Uri.EscapeDataString(text);

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(12));
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");

                var response = await HttpClient.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                return ParseGoogleKeylessResult(json);
            }
            catch
            {
                return null;
            }
        }

        internal static GoogleTranslationResult? ParseGoogleKeylessResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("sentences", out var sentences) &&
                    sentences.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var sentence in sentences.EnumerateArray())
                    {
                        if (sentence.TryGetProperty("trans", out var trans) &&
                            trans.ValueKind == JsonValueKind.String)
                        {
                            sb.Append(trans.GetString());
                        }
                    }

                    return new GoogleTranslationResult
                    {
                        TranslatedText = SanitizeTranslatedText(sb.ToString().Trim()),
                        DetectedSourceLanguage = ReadDetectedSourceLanguage(root)
                    };
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    string? detectedSourceLanguage = null;

                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
                        {
                            if (element[0].ValueKind == JsonValueKind.String)
                                sb.Append(element[0].GetString());

                            if (element.GetArrayLength() > 1 && element[1].ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(detectedSourceLanguage))
                                detectedSourceLanguage = element[1].GetString();
                        }
                        else if (element.ValueKind == JsonValueKind.String)
                        {
                            sb.Append(element.GetString());
                        }
                    }

                    return new GoogleTranslationResult
                    {
                        TranslatedText = SanitizeTranslatedText(sb.ToString().Trim()),
                        DetectedSourceLanguage = detectedSourceLanguage ?? ReadDetectedSourceLanguage(root)
                    };
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string? ReadDetectedSourceLanguage(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("src", out var source) &&
                source.ValueKind == JsonValueKind.String)
            {
                return source.GetString();
            }

            if (root.ValueKind != JsonValueKind.Array) return null;

            foreach (var element in root.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String) continue;

                string value = element.GetString() ?? "";
                if (value.Equals("tr", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("fr", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("nl", StringComparison.OrdinalIgnoreCase))
                {
                    return value.ToLowerInvariant();
                }
            }

            return null;
        }

        private static async Task<string?> TryMyMemoryAsync(string text, string target)
        {
            try
            {
                string langPair = "autodetect|" + target;
                string email = SettingsManager.Current.TranslationEmail;

                List<string> chunks = SplitByBytes(text, MyMemoryMaxChunkBytes);
                var translatedParts = new List<string>(chunks.Count);

                foreach (string chunk in chunks)
                {
                    string uri = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(chunk) +
                                 "&langpair=" + Uri.EscapeDataString(langPair);
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        uri += "&de=" + Uri.EscapeDataString(email);
                    }

                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(12));
                    string json = await HttpClient.GetStringAsync(uri, cts.Token);

                    string? part = ParseMyMemoryResponse(json, chunk);
                    if (string.IsNullOrEmpty(part)) return null;

                    translatedParts.Add(part!);
                }

                return string.Join(" ", translatedParts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            catch
            {
                return null;
            }
        }

        internal static string? ParseMyMemoryResponse(string json, string originalChunk)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("responseData", out var data)) return null;

                if (data.TryGetProperty("translatedText", out var translated) &&
                    translated.ValueKind == JsonValueKind.String)
                {
                    string value = (translated.GetString() ?? "").Trim();
                    if (value.StartsWith("MYMEMORY", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("QUERY LENGTH LIMIT", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                    return SanitizeTranslatedText(value);
                }
            }
            catch { }
            return null;
        }

        internal static List<string> SplitByBytes(string text, int maxBytes)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            string[] sentences = Regex.Split(text, @"(?<=[\.\!\?\n])\s+");
            var current = new StringBuilder();

            foreach (string sentence in sentences)
            {
                if (Encoding.UTF8.GetByteCount(current.Length == 0 ? sentence : current + " " + sentence) <= maxBytes)
                {
                    if (current.Length > 0) current.Append(' ');
                    current.Append(sentence);
                }
                else
                {
                    if (current.Length > 0)
                    {
                        chunks.Add(current.ToString());
                        current.Clear();
                    }
                    current.Append(sentence);
                }
            }

            if (current.Length > 0) chunks.Add(current.ToString());
            return chunks;
        }
    }
}
