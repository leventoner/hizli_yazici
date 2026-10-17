using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
        public string? UsedModel { get; set; }
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
                    (string json, string? usedModel) = await TextService.CallGeminiRawAsync(prompt, "dictionary", 15);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        DictionaryEntry? parsed = TryParseEntry(json, cleanWord, lang);
                        if (parsed != null)
                        {
                            parsed.UsedModel = usedModel;
                            LastEntry = parsed;
                            return parsed;
                        }
                    }
                }
                catch { /* sessizce yedeğe düş */ }
            }

            // 2. Ücretsiz yedek: ek analizi + ücretsiz çeviri
            var fallback = BuildFallbackEntry(cleanWord, lang);
            LastEntry = fallback;
            return fallback;
        }

        /// <summary>
        /// En son üretilen sözlük girişi. TranslationResultWindow, tek kelime
        /// algıladığında bu kaydı alıp renkli FlowDocument'ı tekrar API'ye
        /// başvurmadan (çift çağrı olmadan) çizer.
        /// </summary>
        public static DictionaryEntry? LastEntry { get; private set; }

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
                ? (string.IsNullOrEmpty(entry.UsedModel)
                    ? "🤖 Gemini AI ile üretildi"
                    : $"🤖 Gemini AI ile üretildi ({NormalizeModelName(entry.UsedModel)})")
                : "⚠️ Ücretsiz yedek analiz (Gemini kullanılamadı)");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Model adını sunuma uygun kısa biçime çevirir (ör. "models/gemini-3.8-flash" → "Gemini 3.8 Flash").
        /// </summary>
        internal static string NormalizeModelName(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return "";
            string m = model.StartsWith("models/") ? model["models/".Length..] : model;
            m = m.Replace("gemini-", "", StringComparison.OrdinalIgnoreCase);
            m = m.Replace("-", " ");
            return "Gemini " + m;
        }

        // ---- Renkli akış (FlowDocument) için palet ----
        private static readonly Color ColorHeading = Color.FromRgb(0x93, 0xC5, 0xFD);   // açık mavi başlık
        private static readonly Color ColorLabel = Color.FromRgb(0x7D, 0xD3, 0xFC);      // cyan etiket
        private static readonly Color ColorValue = Color.FromRgb(0xF8, 0xFA, 0xFC);      // beyaz değer
        private static readonly Color ColorBadgeText = Color.FromRgb(0x0A, 0x0F, 0x1E);  // rozet içi koyu
        private static readonly Color ColorBadgeBg = Color.FromRgb(0xBA, 0xE6, 0xFD);    // rozet zemini
        private static readonly Color ColorBody = Color.FromRgb(0xE2, 0xE8, 0xF0);       // gövde
        private static readonly Color ColorExample = Color.FromRgb(0x86, 0xEF, 0xAC);    // yeşil hedef-dil örneği
        private static readonly Color ColorMuted = Color.FromRgb(0x94, 0xA3, 0xB8);      // soluk not
        private static readonly Color ColorWarn = Color.FromRgb(0xFB, 0xBF, 0x24);       // sarı uyarı

        private static Run MakeRun(string text, double size, Color color, FontWeight? weight = null, bool italic = false)
        {
            var run = new Run(text)
            {
                FontSize = size,
                Foreground = new SolidColorBrush(color),
                FontFamily = new FontFamily("Segoe UI")
            };
            if (weight.HasValue) run.FontWeight = weight.Value;
            if (italic) run.FontStyle = FontStyles.Italic;
            return run;
        }

        private static Paragraph MakeParagraph(double spaceBefore = 0, double spaceAfter = 4)
        {
            return new Paragraph
            {
                Margin = new Thickness(0, spaceBefore, 0, spaceAfter),
                LineHeight = 21
            };
        }

        private static void AddBadge(Paragraph p, string text, double size = 12.5)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(ColorBadgeBg),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 2, 0),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = size,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(ColorBadgeText),
                    FontFamily = new FontFamily("Segoe UI")
                }
            };
            p.Inlines.Add(new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Bottom });
        }

        /// <summary>
        /// Sözlük girişini başlıkları ve kelime türü rozetleri renklendirilmiş,
        /// seçilebilir bir FlowDocument olarak üretir (TranslationResultWindow için).
        /// </summary>
        public static FlowDocument BuildFlowDocument(DictionaryEntry entry)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14.5,
                Foreground = new SolidColorBrush(ColorBody),
                PagePadding = new Thickness(0),
                LineHeight = 21
            };

            // Başlık: kelime + telaffuz
            var head = MakeParagraph(spaceAfter: 2);
            head.Inlines.Add(MakeRun("📖  ", 18, ColorLabel));
            head.Inlines.Add(MakeRun(entry.OriginalWord, 20, ColorHeading, FontWeights.Bold));
            if (!string.IsNullOrEmpty(entry.Phonetic))
                head.Inlines.Add(MakeRun("   " + entry.Phonetic, 13.5, ColorMuted, italic: true));
            doc.Blocks.Add(head);

            void AddInfoLine(string label, string value, Color valueColor, FontWeight? valueWeight = null, bool italic = false)
            {
                var p = MakeParagraph(spaceAfter: 3);
                p.Inlines.Add(MakeRun(label + ": ", 13.5, ColorLabel, FontWeights.SemiBold));
                p.Inlines.Add(MakeRun(value, 14.5, valueColor, valueWeight, italic));
                doc.Blocks.Add(p);
            }

            AddInfoLine("Kök (root)", entry.RootWord, ColorValue, FontWeights.Bold);
            if (!string.IsNullOrEmpty(entry.GrammaticalNote))
                AddInfoLine("Ek analizi", entry.GrammaticalNote, ColorMuted, italic: true);
            AddInfoLine("Sözcük türü", entry.PartOfSpeech, ColorValue);

            string src = string.IsNullOrEmpty(entry.SourceLang)
                ? entry.TargetLang.ToUpperInvariant()
                : $"{entry.SourceLang.ToUpperInvariant()} → {entry.TargetLang.ToUpperInvariant()}";
            var pairLine = MakeParagraph(spaceBefore: 6, spaceAfter: 6);
            pairLine.Inlines.Add(MakeRun("Karşılık ", 13.5, ColorLabel, FontWeights.SemiBold));
            pairLine.Inlines.Add(MakeRun($"({src})", 13.5, ColorValue, FontWeights.Bold));
            pairLine.Inlines.Add(MakeRun(":", 13.5, ColorLabel, FontWeights.SemiBold));
            doc.Blocks.Add(pairLine);

            // Anlamlar (numara + kelime türü rozeti + anlam)
            int meaningNo = 1;
            foreach (var meaning in entry.Meanings)
            {
                var m = MakeParagraph(spaceBefore: 5, spaceAfter: 3);
                m.Margin = new Thickness(0, 5, 0, 3);
                m.Inlines.Add(MakeRun($"{meaningNo}. ", 14, ColorLabel, FontWeights.Bold));
                if (!string.IsNullOrEmpty(meaning.PartOfSpeech))
                    AddBadge(m, meaning.PartOfSpeech);
                m.Inlines.Add(MakeRun("  " + meaning.Meaning, 15, ColorValue, FontWeights.SemiBold));
                doc.Blocks.Add(m);

                if (!string.IsNullOrEmpty(meaning.Definition))
                {
                    var d = MakeParagraph(spaceAfter: 4);
                    d.Margin = new Thickness(24, 0, 0, 4);
                    d.Inlines.Add(MakeRun(meaning.Definition, 13.5, ColorMuted, italic: true));
                    doc.Blocks.Add(d);
                }

                foreach (var ex in meaning.Examples)
                {
                    var exSrc = MakeParagraph(spaceAfter: 1);
                    exSrc.Margin = new Thickness(24, 2, 0, 1);
                    exSrc.Inlines.Add(MakeRun("TR: ", 12.5, ColorLabel, FontWeights.SemiBold));
                    exSrc.Inlines.Add(MakeRun(ex.Source, 13.5, ColorBody));
                    doc.Blocks.Add(exSrc);

                    var exTgt = MakeParagraph(spaceAfter: 6);
                    exTgt.Margin = new Thickness(24, 0, 0, 6);
                    exTgt.Inlines.Add(MakeRun($"{entry.TargetLang.ToUpperInvariant()}: ", 12.5, ColorExample, FontWeights.SemiBold));
                    exTgt.Inlines.Add(MakeRun(ex.Target, 13.5, ColorExample));
                    doc.Blocks.Add(exTgt);
                }

                meaningNo++;
            }

            // Eş anlamlılar
            if (entry.Synonyms.Count > 0)
            {
                var syn = MakeParagraph(spaceBefore: 8, spaceAfter: 4);
                syn.Inlines.Add(MakeRun("Eş anlamlılar: ", 13.5, ColorLabel, FontWeights.SemiBold));
                syn.Inlines.Add(MakeRun(string.Join("  •  ", entry.Synonyms), 14, ColorValue));
                doc.Blocks.Add(syn);
            }

            // Kaynak bilgisi (model adıyla)
            var srcLine = MakeParagraph(spaceBefore: 10, spaceAfter: 0);
            if (entry.IsFromAi)
            {
                string modelLabel = string.IsNullOrEmpty(entry.UsedModel) ? "" : $" ({NormalizeModelName(entry.UsedModel)})";
                srcLine.Inlines.Add(MakeRun($"🤖 Gemini AI ile üretildi{modelLabel}", 13, ColorLabel, FontWeights.SemiBold));
            }
            else
            {
                srcLine.Inlines.Add(MakeRun("⚠️ Ücretsiz yedek analiz (Gemini kullanılamadı)", 13, ColorWarn, FontWeights.SemiBold));
            }
            doc.Blocks.Add(srcLine);

            return doc;
        }
    }
}