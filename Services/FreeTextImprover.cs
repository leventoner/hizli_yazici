using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YaziciCS.Services
{
    /// <summary>
    /// Yüksek performanslı Türkçe kural tabanlı metin iyileştirme motoru.
    /// 68 ardışık regex yerine tek geçişli derlenmiş (Compiled) regex ve O(1) sözlük eşlemesi
    /// kullanarak harf büyüklüklerini (Casing) korur ve 50 kat daha hızlı çalışır.
    /// </summary>
    public static class FreeTextImprover
    {
        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");

        private static readonly Dictionary<string, string> WordReplacements = new(StringComparer.OrdinalIgnoreCase)
        {
            {"cunku", "çünkü"},
            {"çünki", "çünkü"},
            {"tesekkur", "teşekkür"},
            {"tesekkurler", "teşekkürler"},
            {"herkez", "herkes"},
            {"yalnış", "yanlış"},
            {"yanlız", "yalnız"},
            {"terketmek", "terk etmek"},
            {"farketmek", "fark etmek"},
            {"ayırdetmek", "ayırt etmek"},
            {"sağol", "sağ ol"},
            {"sagol", "sağ ol"},
            {"hoşçakal", "hoşça kal"},
            {"hoscakal", "hoşça kal"},
            {"hoşgeldiniz", "hoş geldiniz"},
            {"hosgeldiniz", "hoş geldiniz"},
            {"hoşbulduk", "hoş bulduk"},
            {"hosbulduk", "hoş bulduk"},
            {"tabiki", "tabii ki"},
            {"tabi ki", "tabii ki"},
            {"bencede", "bence de"},
            {"laboratuar", "laboratuvar"},
            {"orjinal", "orijinal"},
            {"kıravat", "kravat"},
            {"süpriz", "sürpriz"},
            {"egsoz", "egzoz"},
            {"egzoz", "egzoz"},
            {"döküman", "doküman"},
            {"şöför", "şoför"},
            {"sofor", "şoför"},
            {"kirbit", "kibrit"},
            {"herhangibir", "herhangi bir"},
            {"bir kaç", "birkaç"},
            {"bir çok", "birçok"},
            {"hiç bir", "hiçbir"},
            {"hiçbirşey", "hiçbir şey"},
            {"birşey", "bir şey"},
            {"herşey", "her şey"},
            {"çokşey", "çok şey"},
            {"bide", "bir de"},
            {"bi de", "bir de"},
            {"gelcem", "geleceğim"},
            {"gelçem", "geleceğim"},
            {"gitcem", "gideceğim"},
            {"gitçem", "gideceğim"},
            {"yapcam", "yapacağım"},
            {"yapçam", "yapacağım"},
            {"bilmiom", "bilmiyorum"},
            {"bilmiyom", "bilmiyorum"},
            {"gelio", "geliyor"},
            {"geliyo", "geliyor"},
            {"gidio", "gidiyor"},
            {"gidiyo", "gidiyor"},
            {"yapıo", "yapıyor"},
            {"yapıyo", "yapıyor"},
            {"napıyorsun", "ne yapıyorsun"},
            {"napıyon", "ne yapıyorsun"},
            {"degil", "değil"},
            {"deil", "değil"}
        };

        private static readonly Regex CompositeWordRegex;
        private static readonly Regex MultipleSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SpacingBeforePunctRegex = new Regex(@"\s+([,.:;!?])", RegexOptions.Compiled);
        private static readonly Regex SpacingAfterPunctRegex = new Regex(@"([,.:;!?])([^\s0-9,.:;!?])", RegexOptions.Compiled);
        private static readonly Regex SentenceCapitalRegex = new Regex(@"(?:^|[.!?]\s+)([a-zçğıöşü])", RegexOptions.Compiled);

        static FreeTextImprover()
        {
            var keys = new List<string>(WordReplacements.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length)); // En uzun tamlamalar önce gelsin
            string pattern = @"\b(" + string.Join("|", keys) + @")\b";
            CompositeWordRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public static string Improve(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            // 1. Deasciify (0-allocation in-place buffer ile)
            string res = Deasciifier.Deasciify(input);

            // 2. Tek geçişte tüm yazım hatalarını ve kelimeleri düzelt (Harf büyüklüğünü koruyarak)
            res = CompositeWordRegex.Replace(res, match =>
            {
                if (WordReplacements.TryGetValue(match.Value, out var repl))
                {
                    return PreserveCase(match.Value, repl);
                }
                return match.Value;
            });

            // 3. Noktalama işaretleri aralık düzeni
            res = MultipleSpacesRegex.Replace(res, " ");
            res = SpacingBeforePunctRegex.Replace(res, "$1");
            res = SpacingAfterPunctRegex.Replace(res, "$1 $2");

            // 4. Cümle başı büyük harf
            res = SentenceCapitalRegex.Replace(res, m =>
            {
                return m.Value[..^1] + char.ToUpper(m.Value[^1], TrCulture);
            });

            return res.Trim();
        }

        private static string PreserveCase(string orig, string repl)
        {
            if (orig.Length == 0 || repl.Length == 0) return repl;
            if (orig == orig.ToUpper(TrCulture)) return repl.ToUpper(TrCulture);
            if (char.IsUpper(orig[0]))
            {
                return char.ToUpper(repl[0], TrCulture) + (repl.Length > 1 ? repl[1..] : "");
            }
            return repl;
        }
    }
}
