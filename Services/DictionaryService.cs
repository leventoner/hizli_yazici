using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YaziciCS.Managers;

namespace YaziciCS.Services
{
    public class DictionaryExample
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
    }

    public class DictionaryMeaning
    {
        public string Meaning { get; set; } = "";
        public string PartOfSpeech { get; set; } = "";
        public string Definition { get; set; } = "";
        public List<DictionaryExample> Examples { get; set; } = new();
    }

    public class DictionaryEntry
    {
        public string OriginalWord { get; set; } = "";
        public string RootWord { get; set; } = "";
        public string PartOfSpeech { get; set; } = "";
        public string? Phonetic { get; set; }
        public string? GrammaticalNote { get; set; }
        public List<DictionaryMeaning> Meanings { get; set; } = new();
        public List<string> Synonyms { get; set; } = new();
        public string TargetLang { get; set; } = "en";
        public string SourceLang { get; set; } = "";
        public bool IsFromAi { get; set; }
    }

    /// <summary>
    /// Web uygulamasındaki (src/services/dictionaryService.ts) sözlük mantığının
    /// WPF karşılığı. Tek kelimeler için Gemini ile yapılandırılmış sözlük girişi
    /// üretir; Gemini yoksa/hata verirse ücretsiz ek (suffix) analizi + ücretsiz
    /// çeviri ile yedek sonuç döner.
    /// </summary>
    public static class DictionaryService
    {
        /// <summary>
        /// Bir metnin tek kelimeye uygun olup olmadığını kontrol eder.
        /// Noktalama, tırnak ve parantezleri temizler; boşluk varsa null döndürür.
        /// </summary>
        public static string? ExtractSingleWord(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string cleaned = Regex.Replace(
                text.Trim(),
                @"^[\s""'«»„“”‘’\(\)\[\]\{\}\.,;:!?\-_—–]+|[\s""'«»„“”‘’\(\)\[\]\{\}\.,;:!?\-_—–]+$",
                "");
            if (string.IsNullOrEmpty(cleaned)) return null;
            if (Regex.IsMatch(cleaned, @"\s")) return null;
            return cleaned;
        }

        /// <summary>
        /// Sözlük girişini üretir. API anahtarı varsa Gemini 1. öncelik; yoksa veya
        /// hata alınırsa ücretsiz ek analizi + ücretsiz çeviri yedeği kullanılır.
        /// </summary>
        public static async Task<DictionaryEntry> LookupAsync(string word, string targetLanguage = "en")
        {
            string lang = (targetLanguage ?? "en").ToLowerInvariant();
            if (lang != "fr" && lang != "nl") lang = "en";
            string cleanWord = ExtractSingleWord(word) ?? word.Trim();

            // 1. Gemini (öncelikli) — API anahtarı varsa
            if (!string.IsNullOrEmpty(SettingsManager.Current.GeminiApiKey))
            {
                string langName = LangDisplayName(lang);
                string prompt = BuildGeminiPrompt(cleanWord, langName);

                try
                {
                    string json = await TextService.CallGeminiRawAsync(prompt, "dictionary", 15);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        DictionaryEntry? parsed = TryParseEntry(json, cleanWord, lang);
                        if (parsed != null) return parsed;
                    }
                }
                catch { /* sessizce yedeğe düş */ }
            }

            // 2. Ücretsiz yedek: ek analizi + ücretsiz çeviri
            return BuildFallbackEntry(cleanWord, lang);
        }

        private static string LangDisplayName(string lang) => lang switch
        {
            "fr" => "French (Fransızca)",
            "nl" => "Dutch / Flemish (Hollandaca / Flamanca)",
            _ => "English (İngilizce)"
        };

        private static string BuildGeminiPrompt(string word, string targetLangName) =>
            "You are a bilingual Turkish lexicographer.\n" +
            $"Analyze the word between triple quotes below. It may be in Turkish or in {targetLangName}.\n" +
            "1. Detect the language of the input word.\n" +
            "2. Find the fundamental root/lemma (kök/yalın hali) of this word (e.g. \"filmi\" -> root \"film\"; \"koşarken\" -> root \"koşmak\"; \"went\" -> root \"go\"; \"kijken\" -> root \"kijken\").\n" +
            "3. If the word has inflectional suffixes (hal eki, zaman eki, iyelik eki vb.), explain it in 'grammaticalNote' (e.g. \"-i belirtme durumu (accusative case)\").\n" +
            "4. Determine part of speech (isim / noun, fiil / verb, sıfat / adjective, zarf / adverb, etc.).\n" +
            $"5. Provide accurate translations and meanings of the ROOT word into {targetLangName} (if input is Turkish, explain in {targetLangName}; if input is {targetLangName}, explain in Turkish).\n" +
            "6. Provide 1 to 3 realistic example sentences demonstrating usage of the word with natural, fluent bilingual translations.\n" +
            "7. Include relevant synonyms if available.\n\n" +
            "Return ONLY valid JSON with this structure:\n" +
            "{\n" +
            "  \"originalWord\": \"string\",\n" +
            "  \"rootWord\": \"string\",\n" +
            "  \"partOfSpeech\": \"string\",\n" +
            "  \"phonetic\": \"string\",\n" +
            "  \"sourceLang\": \"tr|en|fr|nl\",\n" +
            "  \"grammaticalNote\": \"string\",\n" +
            "  \"meanings\": [\n" +
            "    {\n" +
            "      \"meaning\": \"primary translations separated by commas\",\n" +
            "      \"partOfSpeech\": \"noun/verb/adjective etc.\",\n" +
            "      \"definition\": \"concise definition or context\",\n" +
            "      \"examples\": [\n" +
            "        { \"source\": \"example sentence in source language\", \"target\": \"translation in target language\" }\n" +
            "      ]\n" +
            "    }\n" +
            "  ],\n" +
            "  \"synonyms\": [\"string\"]\n" +
            "}\n" +
            $"Text:\n\"\"\"\n{word}\n\"\"\"";

        private static DictionaryEntry? TryParseEntry(string json, string cleanWord, string lang)
        {
            try
            {
                string trimmed = json.Trim();
                int start = trimmed.IndexOf('{');
                int end = trimmed.LastIndexOf('}');
                if (start < 0 || end <= start) return null;
                trimmed = trimmed.Substring(start, end - start + 1);

                using JsonDocument doc = JsonDocument.Parse(trimmed);
                JsonElement root = doc.RootElement;

                var entry = new DictionaryEntry
                {
                    OriginalWord = GetString(root, "originalWord") ?? cleanWord,
                    RootWord = GetString(root, "rootWord") ?? cleanWord,
                    PartOfSpeech = GetString(root, "partOfSpeech") ?? "Kelime",
                    Phonetic = GetString(root, "phonetic"),
                    GrammaticalNote = GetString(root, "grammaticalNote"),
                    SourceLang = GetString(root, "sourceLang") ?? "",
                    TargetLang = lang,
                    IsFromAi = true
                };

                if (root.TryGetProperty("meanings", out JsonElement meanings) &&
                    meanings.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement m in meanings.EnumerateArray())
                    {
                        var meaning = new DictionaryMeaning
                        {
                            Meaning = GetString(m, "meaning") ?? "",
                            PartOfSpeech = GetString(m, "partOfSpeech") ?? "",
                            Definition = GetString(m, "definition") ?? ""
                        };
                        if (m.TryGetProperty("examples", out JsonElement examples) &&
                            examples.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement ex in examples.EnumerateArray())
                            {
                                meaning.Examples.Add(new DictionaryExample
                                {
                                    Source = GetString(ex, "source") ?? "",
                                    Target = GetString(ex, "target") ?? ""
                                });
                            }
                        }
                        entry.Meanings.Add(meaning);
                    }
                }

                if (root.TryGetProperty("synonyms", out JsonElement syns) &&
                    syns.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement s in syns.EnumerateArray())
                    {
                        string? v = s.GetString();
                        if (!string.IsNullOrWhiteSpace(v)) entry.Synonyms.Add(v!);
                    }
                }

                return entry.Meanings.Count > 0 || !string.IsNullOrEmpty(entry.RootWord)
                    ? entry
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? GetString(JsonElement obj, string prop)
        {
            if (obj.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            return null;
        }

        /// <summary>
        /// Gemini kullanılamadığında kullanılan ücretsiz yedek: Türkçe ek (suffix)
        /// analizi + ücretsiz çeviri ile temel bir sözlük girişi kurar.
        /// </summary>
        private static DictionaryEntry BuildFallbackEntry(string word, string lang)
        {
            var (root, note) = HeuristicTurkishStem(word);
            string meaning = "";
            try
            {
                meaning = FreeTranslationService.TranslateAsync(root, lang).GetAwaiter().GetResult();
            }
            catch { meaning = word; }

            return new DictionaryEntry
            {
                OriginalWord = word,
                RootWord = root,
                PartOfSpeech = "Kelime / Sözcük",
                GrammaticalNote = note != "yalın hal (kök)" ? note : null,
                TargetLang = lang,
                IsFromAi = false,
                Meanings = new List<DictionaryMeaning>
                {
                    new DictionaryMeaning
                    {
                        Meaning = meaning,
                        Definition = $"\"{root}\" sözcüğünün {lang.ToUpperInvariant()} karşılığı."
                    }
                }
            };
        }

        /// <summary>
        /// Yaygın Türkçe ekleri (durum, çoğul, iyelik, yönelme vb.) sezgisel olarak
        /// ayırarak kökü tahmin eder. Web sürümündeki heuristicTurkishStem ile aynı.
        /// </summary>
        internal static (string root, string note) HeuristicTurkishStem(string word)
        {
            string lower = word.ToLowerInvariant();

            // Belirtme durumu (-i, -yi, -yı, -yu, -yü)
            if (Regex.IsMatch(lower, @"(?:[aeıioöuü]y[ıiuü]|[ıiuü])$") && lower.Length > 3)
            {
                if (lower.EndsWith("yi") || lower.EndsWith("yı") || lower.EndsWith("yu") || lower.EndsWith("yü"))
                    return (word[..^2], "-i hali (belirtme durumu)");
                return (word[..^1], "-i hali (belirtme durumu)");
            }

            // Yönelme durumu (-e, -a, -ye, -ya)
            if (Regex.IsMatch(lower, @"(?:ye|ya|[ea])$") && lower.Length > 3)
            {
                if (lower.EndsWith("ye") || lower.EndsWith("ya"))
                    return (word[..^2], "-e hali (yönelme durumu)");
                return (word[..^1], "-e hali (yönelme durumu)");
            }

            // Bulunma durumu (-de, -da, -te, -ta)
            if (Regex.IsMatch(lower, @"(?:de|da|te|ta)$") && lower.Length > 3)
                return (word[..^2], "-de hali (bulunma durumu)");

            // Ayrılma durumu (-den, -dan, -ten, -tan)
            if (Regex.IsMatch(lower, @"(?:den|dan|ten|tan)$") && lower.Length > 4)
                return (word[..^3], "-den hali (ayrılma durumu)");

            // Çoğul (-ler, -lar)
            if (Regex.IsMatch(lower, @"(?:ler|lar)$") && lower.Length > 4)
                return (word[..^3], "çoğul eki (-ler/-lar)");

            // Tamlayan (-in, -ın, -un, -ün, -nin, -nın, -nun, -nün)
            if (Regex.IsMatch(lower, @"(?:[ıiuü]n|n[ıiuü]n)$") && lower.Length > 4)
            {
                if (lower.EndsWith("nin") || lower.EndsWith("nın") || lower.EndsWith("nun") || lower.EndsWith("nün"))
                    return (word[..^3], "tamlayan / ilgi eki (-in/-nin)");
                return (word[..^2], "tamlayan / ilgi eki (-in)");
            }

            return (word, "yalın hal (kök)");
        }

        /// <summary>
        /// Sözlük girişini seçilebilir, kopyalanabilir zengin bir metne çevirir.
        /// TranslationResultWindow içinde gösterilir.
        /// </summary>
        public static string FormatEntry(DictionaryEntry entry)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"📖 {entry.OriginalWord}");
            if (!string.IsNullOrEmpty(entry.Phonetic)) sb.AppendLine($"🔊 {entry.Phonetic}");
            sb.AppendLine();

            sb.AppendLine($"Kök (root): {entry.RootWord}");
            if (!string.IsNullOrEmpty(entry.GrammaticalNote))
                sb.AppendLine($"Ek analizi: {entry.GrammaticalNote}");
            sb.AppendLine($"Sözcük türü: {entry.PartOfSpeech}");
            string src = string.IsNullOrEmpty(entry.SourceLang)
                ? ""
                : $" ({entry.SourceLang.ToUpperInvariant()} → {entry.TargetLang.ToUpperInvariant()})";
            sb.AppendLine($"Karşılık{src}:");
            sb.AppendLine();

            foreach (var meaning in entry.Meanings)
            {
                if (!string.IsNullOrEmpty(meaning.PartOfSpeech))
                    sb.AppendLine($"  • [{meaning.PartOfSpeech}] {meaning.Meaning}");
                else
                    sb.AppendLine($"  • {meaning.Meaning}");

                if (!string.IsNullOrEmpty(meaning.Definition))
                    sb.AppendLine($"    {meaning.Definition}");

                foreach (var ex in meaning.Examples)
                {
                    sb.AppendLine($"    Örnek: {ex.Source}");
                    sb.AppendLine($"           {ex.Target}");
                }
                sb.AppendLine();
            }

            if (entry.Synonyms.Count > 0)
                sb.AppendLine("Eş anlamlılar: " + string.Join(", ", entry.Synonyms));

            sb.AppendLine();
            sb.Append(entry.IsFromAi
                ? "🤖 Gemini AI ile üretildi"
                : "⚠️ Ücretsiz yedek analiz (Gemini kullanılamadı)");
            return sb.ToString().TrimEnd();
        }
    }
}