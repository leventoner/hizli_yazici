using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace YaziciCS.Services
{
    public static class Deasciifier
    {
        // Simple word-based mapping for very common errors
        private static readonly Dictionary<string, string> CommonWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"merhum", "merhum"},
            {"umit", "ümit"},
            {"yazdigim", "yazdığım"},
            {"yazi", "yazı"},
            {"usttekini", "üsttekini"},
            {"nasil", "nasıl"},
            {"kizdirdiysa", "kızdırdıysa"},
            {"gelip", "gelip"},
            {"yazmis", "yazmış"},
            {"kizim", "kızım"},
            {"senin", "senin"},
            {"hocanin", "hocanın"},
            {"kocanin", "kocanın"},
            {"babanin", "babanın"},
            {"arkadasi", "arkadaşı"},
            {"degilim", "değilim"},
            {"birak", "bırak"},
            {"muptelasi", "müptelası"},
            {"ergenliginden", "ergenliğinden"},
            {"dusmanim", "düşmanım"},
            {"olmayi", "olmayı"},
            {"oyle", "öyle"},
            {"kiymetli", "kıymetli"},
            {"umarim", "umarım"},
            {"basarilar", "başarılar"},
            {"gormek", "görmek"},
            {"calismalar", "çalışmalar"},
            {"tesekkur", "teşekkür"},
            {"simdi", "şimdi"},
            {"cok", "çok"},
            {"is", "iş"},
            {"icin", "için"},
            {"birsey", "bir şey"},
            {"hersey", "her şey"},
            {"ornek", "örnek"},
            {"gun", "gün"},
            {"guzel", "güzel"},
            {"bugun", "bugün"},
            {"aksam", "akşam"},
            {"oglen", "öğlen"},
            {"ogleden", "öğleden"},
            {"merhaba", "merhaba"},
            {"ulasmak", "ulaşmak"},
            {"yapmak", "yapmak"},
            {"ediyoruz", "ediyoruz"},
            {"tesekkurler", "teşekkürler"},
            {"lutfen", "lütfen"},
            {"donus", "dönüş"}
        };

        // Heuristic rules for common Turkish patterns
        private static readonly List<(string Pattern, string Replacement)> SuffixRules = new List<(string, string)>
        {
            (@"([a-z])digim\b", "$1dığım"),
            (@"([a-z])digin\b", "$1dığın"),
            (@"([a-z])digi\b", "$1dığı"),
            (@"([a-z])digimiz\b", "$1dığımız"),
            (@"([a-z])diginiz\b", "$1dığınız"),
            (@"([a-z])diklari\b", "$1dıkları"),
            (@"([a-z])acagim\b", "$1acağım"),
            (@"([a-z])acaksin\b", "$1acaksın"),
            (@"([a-z])acak\b", "$1acak"),
            (@"\bkiz([a-z]*)\b", "kız$1"),
            (@"\byazi([a-z]*)\b", "yazı$1"),
            (@"\bhoca([a-z]+)in\b", "hoca$1ın"),
            (@"\bkoca([a-z]+)in\b", "koca$1ın"),
            (@"\bbaba([a-z]+)in\b", "baba$1ın"),
            (@"\barkadas([a-z]*)\b", "arkadaş$1"),
            (@"\bdegil([a-z]*)\b", "değil$1"),
            (@"\bmusait\b", "müsait"),
            (@"\bolmayi\b", "olmayı"),
            (@"\bergenliginden\b", "ergenliğinden"),
            (@"\bdusmanim\b", "düşmanım")
        };

        public static string Deasciify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string result = text;

            // 1. Dictionary-based whole word replacement
            string wordPattern = @"\b[A-Za-zçğıöşüÇĞİÖŞÜ]+\b";
            result = Regex.Replace(result, wordPattern, match =>
            {
                string word = match.Value;
                string wordLower = word.ToLowerInvariant();

                if (CommonWords.ContainsKey(wordLower))
                {
                    return MatchCase(word, CommonWords[wordLower]);
                }
                return word;
            });

            // 2. Rule-based suffix/pattern replacement
            foreach (var rule in SuffixRules)
            {
                result = Regex.Replace(result, rule.Pattern, match => 
                {
                    string replaced = Regex.Replace(match.Value, rule.Pattern, rule.Replacement);
                    return MatchCase(match.Value, replaced);
                }, RegexOptions.IgnoreCase);
            }

            // 3. Final Heuristic Pass for missing characters (Aggressive for Turkish-specific letters)
            // This is a safety net for words not in dictionary or rules.
            // We target common isolated letters like 'muptelasi' -> 'müptelası'
            result = Regex.Replace(result, @"\b[A-Za-zçğıöşüÇĞİÖŞÜ]+\b", match => 
            {
                string w = match.Value;
                string wLower = w.ToLowerInvariant();
                
                // If it's already Corrected, skip
                // Custom check for known leftovers
                if (wLower == "muptelasi") return MatchCase(w, "müptelası");
                if (wLower == "birak") return MatchCase(w, "bırak");
                
                return w;
            });

            return result;
        }

        private static string MatchCase(string original, string corrected)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(corrected)) return corrected;

            if (char.IsUpper(original[0]))
            {
                if (original.Length > 1 && char.IsUpper(original[1]))
                {
                    // UPPERCASE
                    return corrected.ToUpperInvariant();
                }
                else
                {
                    // Title Case
                    if (corrected.Length > 1)
                        return char.ToUpperInvariant(corrected[0]) + corrected.Substring(1).ToLowerInvariant();
                    else 
                        return corrected.ToUpperInvariant();
                }
            }
            return corrected; // lowercase
        }
    }
}
