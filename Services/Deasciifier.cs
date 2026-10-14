using System;
using System.Collections.Generic;

namespace YaziciCS.Services
{
    public class Deasciifier
    {
        private const int ContextSize = 10;

        private string asciiString;
        private string convertedString;

        private static readonly Dictionary<string, string> TurkishAsciifyTable = new Dictionary<string, string>
        {
            {"ç", "c"}, {"Ç", "C"},
            {"ğ", "g"}, {"Ğ", "G"},
            {"ö", "o"}, {"Ö", "O"},
            {"ü", "u"}, {"Ü", "U"},
            {"ı", "i"}, {"İ", "I"},
            {"ş", "s"}, {"Ş", "S"}
        };

        private static readonly Dictionary<string, string> TurkishToggleAccentTable = new Dictionary<string, string>
        {
            {"c", "ç"}, {"C", "Ç"},
            {"g", "ğ"}, {"G", "Ğ"},
            {"o", "ö"}, {"O", "Ö"},
            {"u", "ü"}, {"U", "Ü"},
            {"i", "ı"}, {"I", "İ"},
            {"s", "ş"}, {"S", "Ş"},
            {"ç", "c"}, {"Ç", "C"},
            {"ğ", "g"}, {"Ğ", "G"},
            {"ö", "o"}, {"Ö", "O"},
            {"ü", "u"}, {"Ü", "U"},
            {"ı", "i"}, {"İ", "I"},
            {"ş", "s"}, {"Ş", "S"}
        };

        private static readonly Dictionary<string, string> TurkishDowncaseAsciifyTable;
        private static readonly Dictionary<string, string> TurkishUpcaseAccentsTable;

        static Deasciifier()
        {
            TurkishDowncaseAsciifyTable = new Dictionary<string, string>();
            for (char ch = 'A'; ch <= 'Z'; ch++)
            {
                string upper = ch.ToString();
                string lower = char.ToLowerInvariant(ch).ToString();
                TurkishDowncaseAsciifyTable[upper] = lower;
                TurkishDowncaseAsciifyTable[lower] = lower;
            }
            TurkishDowncaseAsciifyTable["ç"] = "c";
            TurkishDowncaseAsciifyTable["Ç"] = "c";
            TurkishDowncaseAsciifyTable["ğ"] = "g";
            TurkishDowncaseAsciifyTable["Ğ"] = "g";
            TurkishDowncaseAsciifyTable["ö"] = "o";
            TurkishDowncaseAsciifyTable["Ö"] = "o";
            TurkishDowncaseAsciifyTable["ü"] = "u";
            TurkishDowncaseAsciifyTable["Ü"] = "u";
            TurkishDowncaseAsciifyTable["ı"] = "i";
            TurkishDowncaseAsciifyTable["İ"] = "i";
            TurkishDowncaseAsciifyTable["ş"] = "s";
            TurkishDowncaseAsciifyTable["Ş"] = "s";

            TurkishUpcaseAccentsTable = new Dictionary<string, string>();
            for (char ch = 'A'; ch <= 'Z'; ch++)
            {
                string upper = ch.ToString();
                string lower = char.ToLowerInvariant(ch).ToString();
                TurkishUpcaseAccentsTable[upper] = lower;
                TurkishUpcaseAccentsTable[lower] = lower;
            }
            TurkishUpcaseAccentsTable["ç"] = "C";
            TurkishUpcaseAccentsTable["Ç"] = "C";
            TurkishUpcaseAccentsTable["ğ"] = "G";
            TurkishUpcaseAccentsTable["Ğ"] = "G";
            TurkishUpcaseAccentsTable["ö"] = "O";
            TurkishUpcaseAccentsTable["Ö"] = "O";
            TurkishUpcaseAccentsTable["ü"] = "U";
            TurkishUpcaseAccentsTable["Ü"] = "U";
            TurkishUpcaseAccentsTable["ı"] = "I";
            TurkishUpcaseAccentsTable["İ"] = "I";
            TurkishUpcaseAccentsTable["ş"] = "S";
            TurkishUpcaseAccentsTable["Ş"] = "S";
        }

        public static string Deasciify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var instance = new Deasciifier(text);
            return instance.ConvertToTurkish();
        }

        public Deasciifier(string input)
        {
            this.asciiString = input;
            this.convertedString = input;
        }

        public string ConvertToTurkish()
        {
            for (int index = 0; index < convertedString.Length; index++)
            {
                string ch = convertedString[index].ToString();
                if (TurkishNeedCorrection(ch, index))
                {
                    convertedString = SetCharAt(convertedString, index, TurkishToggleAccent(ch));
                }
            }
            return convertedString;
        }

        private static string SetCharAt(string str, int pos, string c)
        {
            return string.Concat(str.AsSpan(0, pos), c.AsSpan(), str.AsSpan(pos + 1));
        }

        private static string TurkishToggleAccent(string c)
        {
            return TurkishToggleAccentTable.TryGetValue(c, out string result) ? result : c;
        }

        private bool TurkishNeedCorrection(string ch, int point)
        {
            string tr = TurkishAsciifyTable.TryGetValue(ch, out string ascii) ? ascii : ch;
            string trLower = tr.ToLowerInvariant();

            bool m = false;
            if (TurkishPatternTable.Table.TryGetValue(trLower, out var pl))
            {
                m = TurkishMatchPattern(pl, point);
            }

            if (tr == "I")
            {
                return ch == tr ? !m : m;
            }
            else
            {
                return ch == tr ? m : !m;
            }
        }

        private bool TurkishMatchPattern(Dictionary<string, int> dlist, int point)
        {
            int rank = 2 * dlist.Count;
            string context = TurkishGetContext(ContextSize, point);

            int start = 0;
            int contextLen = context.Length;

            while (start <= ContextSize)
            {
                int end = 1 + ContextSize;
                while (end <= contextLen)
                {
                    string s = context.Substring(start, end - start);
                    if (dlist.TryGetValue(s, out int r))
                    {
                        if (Math.Abs(r) < Math.Abs(rank))
                        {
                            rank = r;
                        }
                    }
                    end++;
                }
                start++;
            }

            return rank > 0;
        }

        private string TurkishGetContext(int size, int point)
        {
            char[] s = new char[1 + (2 * size)];
            for (int j = 0; j < s.Length; j++) s[j] = ' ';
            s[size] = 'X';

            int i = size + 1;
            bool space = false;
            int index = point + 1;

            while (i < s.Length && !space && index < asciiString.Length)
            {
                string currentChar = convertedString[index].ToString();
                if (TurkishDowncaseAsciifyTable.TryGetValue(currentChar, out string x))
                {
                    s[i] = x[0];
                    i++;
                    space = false;
                }
                else
                {
                    if (!space)
                    {
                        i++;
                        space = true;
                    }
                }
                index++;
            }

            int rightEnd = i;

            index = point - 1;
            i = size - 1;
            space = false;

            while (i >= 0 && index >= 0)
            {
                string currentChar = convertedString[index].ToString();
                if (TurkishUpcaseAccentsTable.TryGetValue(currentChar, out string x))
                {
                    s[i] = x[0];
                    i--;
                    space = false;
                }
                else
                {
                    if (!space)
                    {
                        i--;
                        space = true;
                    }
                }
                index--;
            }

            return new string(s, 0, rightEnd);
        }
    }
}
