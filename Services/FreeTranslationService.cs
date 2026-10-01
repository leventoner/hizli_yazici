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
    /// <summary>
    /// API anahtarı gerektirmeyen (ücretsiz) çeviri sağlayıcıları.
    /// 1) Google dahili "clients5" uç noktası (anahtarsız, otomatik dil algılama)
    /// 2) MyMemory (anahtarsız, dokümante edilmiş; anonim 5000 karakter/gün, tek istekte 500 byte)
    /// Yönlendirme mantığı: metin Türkçe ise İngilizceye, aksi halde Türkçeye çevirir.
    /// </summary>
    public static class FreeTranslationService
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private const int MyMemoryMaxChunkBytes = 450; // 500 byte limitine güvenli mesafe

        public static async Task<string> TranslateAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string target = DetectTargetLanguage(text);

            string? result = await TryGoogleKeylessAsync(text, target);
            if (!string.IsNullOrEmpty(result)) return result!;

            result = await TryMyMemoryAsync(text, target);
            if (!string.IsNullOrEmpty(result)) return result!;

            return "Hata: Ücretsiz çeviri servislerine ulaşılamadı. İnternet bağlantınızı kontrol edin " +
                   "veya Ayarlar'dan çeviri sağlayıcısını Gemini (API anahtarlı) olarak seçin.";
        }

        /// <summary>
        /// Metin Türkçe ise hedef İngilizce ("en"), aksi halde hedef Türkçe ("tr").
        /// </summary>
        internal static string DetectTargetLanguage(string text)
        {
            return LooksTurkish(text) ? "en" : "tr";
        }

        /// <summary>
        /// Basit sezgisel Türkçe tespiti: Türkçeye özgü harfler veya yaygın Türkçe kelimeler.
        /// </summary>
        private static readonly Regex TurkishCharsRegex =
            new Regex("[çğıöşüÇĞİÖŞÜ]", RegexOptions.Compiled);

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

        /// <summary>
        /// Google'ın Chrome sözlük eklentisinin kullandığı, API anahtarı gerektirmeyen
        /// dahili uç noktası. sl=auto ile kaynak dili otomatik algılar.
        /// </summary>
        private static async Task<string?> TryGoogleKeylessAsync(string text, string target)
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
                string? translated = ParseGoogleKeylessResponse(json);
                return string.IsNullOrWhiteSpace(translated) ? null : translated!.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// clients5 yanıtı ya {"sentences":[{"trans":...}]} ya da uzun metinde
        /// [["çeviri1","src"],["çeviri2","src"]] biçiminde bir dizi olabilir.
        /// </summary>
        internal static string? ParseGoogleKeylessResponse(string json)
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
                    return sb.ToString();
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Array &&
                            element.GetArrayLength() > 0 &&
                            element[0].ValueKind == JsonValueKind.String)
                        {
                            sb.Append(element[0].GetString());
                        }
                        else if (element.ValueKind == JsonValueKind.String)
                        {
                            sb.Append(element.GetString());
                        }
                    }
                    return sb.ToString();
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// MyMemory yedeği. Tek istekte en fazla 500 byte kabul edildiğinden metni
        /// cümle/boşluk sınırlarında parçalayıp sırayla çevirir.
        /// </summary>
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
                    // MyMemory bazı durumlarda hata mesajını translatedText içinde döndürür.
                    if (value.StartsWith("MYMEMORY", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("QUERY LENGTH LIMIT", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                    return value;
                }
            }
            catch (JsonException)
            {
            }
            return null;
        }

        /// <summary>
        /// Metni, UTF-8 byte sayısı limiti aşmadan cümle/boşluk sınırlarında parçalara böler.
        /// </summary>
        internal static List<string> SplitByBytes(string text, int maxBytes)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            string[] sentences = Regex.Split(text, @"(?<=[\.\!\?\n])\s+");
            var current = new StringBuilder();

            foreach (string sentence in sentences)
            {
                foreach (string piece in BreakTokenByBytes(sentence, maxBytes))
                {
                    int candidateBytes = Encoding.UTF8.GetByteCount(
                        current.Length == 0 ? piece : current + " " + piece);

                    if (candidateBytes <= maxBytes)
                    {
                        if (current.Length > 0) current.Append(' ');
                        current.Append(piece);
                    }
                    else
                    {
                        if (current.Length > 0)
                        {
                            chunks.Add(current.ToString());
                            current.Clear();
                        }
                        current.Append(piece);
                    }
                }
            }

            if (current.Length > 0) chunks.Add(current.ToString());
            return chunks;
        }

        private static IEnumerable<string> BreakTokenByBytes(string token, int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(token) <= maxBytes)
            {
                yield return token;
                yield break;
            }

            var part = new StringBuilder();
            foreach (char c in token)
            {
                if (Encoding.UTF8.GetByteCount(part.ToString() + c) > maxBytes)
                {
                    if (part.Length > 0) yield return part.ToString();
                    part.Clear();
                }
                part.Append(c);
            }
            if (part.Length > 0) yield return part.ToString();
        }
    }
}
