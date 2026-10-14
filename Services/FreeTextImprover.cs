using System;
using System.Text.RegularExpressions;

namespace YaziciCS.Services
{
    public static class FreeTextImprover
    {
        private static readonly (Regex pattern, string replacement)[] CommonCorrections = new[]
        {
            (new Regex(@"\bcunku\b", RegexOptions.IgnoreCase), "çünkü"),
            (new Regex(@"\bçünki\b", RegexOptions.IgnoreCase), "çünkü"),
            (new Regex(@"\btesekkur\b", RegexOptions.IgnoreCase), "teşekkür"),
            (new Regex(@"\btesekkurler\b", RegexOptions.IgnoreCase), "teşekkürler"),
            (new Regex(@"\bherkez\b", RegexOptions.IgnoreCase), "herkes"),
            (new Regex(@"\byalnış\b", RegexOptions.IgnoreCase), "yanlış"),
            (new Regex(@"\byanlız\b", RegexOptions.IgnoreCase), "yalnız"),
            (new Regex(@"\bterketmek\b", RegexOptions.IgnoreCase), "terk etmek"),
            (new Regex(@"\bfarketmek\b", RegexOptions.IgnoreCase), "fark etmek"),
            (new Regex(@"\bayırdetmek\b", RegexOptions.IgnoreCase), "ayırt etmek"),
            (new Regex(@"\bsağol\b", RegexOptions.IgnoreCase), "sağ ol"),
            (new Regex(@"\bsagol\b", RegexOptions.IgnoreCase), "sağ ol"),
            (new Regex(@"\bhoşçakal\b", RegexOptions.IgnoreCase), "hoşça kal"),
            (new Regex(@"\bhoscakal\b", RegexOptions.IgnoreCase), "hoşça kal"),
            (new Regex(@"\bhoşgeldiniz\b", RegexOptions.IgnoreCase), "hoş geldiniz"),
            (new Regex(@"\bhosgeldiniz\b", RegexOptions.IgnoreCase), "hoş geldiniz"),
            (new Regex(@"\bhoşbulduk\b", RegexOptions.IgnoreCase), "hoş bulduk"),
            (new Regex(@"\bhosbulduk\b", RegexOptions.IgnoreCase), "hoş bulduk"),
            (new Regex(@"\btabiki\b", RegexOptions.IgnoreCase), "tabii ki"),
            (new Regex(@"\btabi ki\b", RegexOptions.IgnoreCase), "tabii ki"),
            (new Regex(@"\bbencede\b", RegexOptions.IgnoreCase), "bence de"),
            (new Regex(@"\blaboratuar\b", RegexOptions.IgnoreCase), "laboratuvar"),
            (new Regex(@"\borjinal\b", RegexOptions.IgnoreCase), "orijinal"),
            (new Regex(@"\bkıravat\b", RegexOptions.IgnoreCase), "kravat"),
            (new Regex(@"\bsüpriz\b", RegexOptions.IgnoreCase), "sürpriz"),
            (new Regex(@"\begsoz\b", RegexOptions.IgnoreCase), "egzoz"),
            (new Regex(@"\begzoz\b", RegexOptions.IgnoreCase), "egzoz"),
            (new Regex(@"\bdöküman\b", RegexOptions.IgnoreCase), "doküman"),
            (new Regex(@"\bşöför\b", RegexOptions.IgnoreCase), "şoför"),
            (new Regex(@"\bsofor\b", RegexOptions.IgnoreCase), "şoför"),
            (new Regex(@"\bkirbit\b", RegexOptions.IgnoreCase), "kibrit"),
            (new Regex(@"\bherhangibir\b", RegexOptions.IgnoreCase), "herhangi bir"),
            (new Regex(@"\bbir kaç\b", RegexOptions.IgnoreCase), "birkaç"),
            (new Regex(@"\bbir çok\b", RegexOptions.IgnoreCase), "birçok"),
            (new Regex(@"\bhiç bir\b", RegexOptions.IgnoreCase), "hiçbir"),
            (new Regex(@"\bhiçbirşey\b", RegexOptions.IgnoreCase), "hiçbir şey"),
            (new Regex(@"\bbirşey\b", RegexOptions.IgnoreCase), "bir şey"),
            (new Regex(@"\bherşey\b", RegexOptions.IgnoreCase), "her şey"),
            (new Regex(@"\bçokşey\b", RegexOptions.IgnoreCase), "çok şey"),
            (new Regex(@"\bbide\b", RegexOptions.IgnoreCase), "bir de"),
            (new Regex(@"\bbi de\b", RegexOptions.IgnoreCase), "bir de"),
            (new Regex(@"\bgelcem\b", RegexOptions.IgnoreCase), "geleceğim"),
            (new Regex(@"\bgelçem\b", RegexOptions.IgnoreCase), "geleceğim"),
            (new Regex(@"\bgitcem\b", RegexOptions.IgnoreCase), "gideceğim"),
            (new Regex(@"\bgitçem\b", RegexOptions.IgnoreCase), "gideceğim"),
            (new Regex(@"\byapcam\b", RegexOptions.IgnoreCase), "yapacağım"),
            (new Regex(@"\byapçam\b", RegexOptions.IgnoreCase), "yapacağım"),
            (new Regex(@"\bbilmiom\b", RegexOptions.IgnoreCase), "bilmiyorum"),
            (new Regex(@"\bbilmiyom\b", RegexOptions.IgnoreCase), "bilmiyorum"),
            (new Regex(@"\bgelio\b", RegexOptions.IgnoreCase), "geliyor"),
            (new Regex(@"\bgeliyo\b", RegexOptions.IgnoreCase), "geliyor"),
            (new Regex(@"\bgidio\b", RegexOptions.IgnoreCase), "gidiyor"),
            (new Regex(@"\bgidiyo\b", RegexOptions.IgnoreCase), "gidiyor"),
            (new Regex(@"\byapıo\b", RegexOptions.IgnoreCase), "yapıyor"),
            (new Regex(@"\byapıyo\b", RegexOptions.IgnoreCase), "yapıyor"),
            (new Regex(@"\bnapıyorsun\b", RegexOptions.IgnoreCase), "ne yapıyorsun"),
            (new Regex(@"\bnapıyon\b", RegexOptions.IgnoreCase), "ne yapıyorsun"),
            (new Regex(@"\bdegil\b", RegexOptions.IgnoreCase), "değil"),
            (new Regex(@"\bdeil\b", RegexOptions.IgnoreCase), "değil")
        };

        public static string Improve(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            // 1. Deasciify
            string res = Deasciifier.Deasciify(input);

            // 2. Common spelling and typo fixes
            foreach (var (pattern, replacement) in CommonCorrections)
            {
                res = pattern.Replace(res, replacement);
            }

            // 3. Punctuation spacing
            res = Regex.Replace(res, @"\s+", " ");
            res = Regex.Replace(res, @"\s+([,.:;!?])", "$1");
            res = Regex.Replace(res, @"([,.:;!?])([^\s0-9,.:;!?])", "$1 $2");

            // 4. Capitalize first letter of sentences
            res = Regex.Replace(res, @"(?:^|[.!?]\s+)([a-zçğıöşü])", m =>
            {
                return m.Value[..^1] + char.ToUpper(m.Value[^1], new System.Globalization.CultureInfo("tr-TR"));
            });

            return res.Trim();
        }
    }
}
