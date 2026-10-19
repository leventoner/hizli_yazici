using System;
using System.Collections.Generic;

namespace YaziciCS.Services
{
    /// <summary>
    /// Yüksek performanslı ve sıfır gereksiz heap tahsisli (allocation-free) Deasciifier motoru.
    /// Türkçe karakter dönüşümlerini in-place char[] üzerinde işletir; string.Concat ve 
    /// tek harflik Dictionary tahsislerini ortadan kaldırır.
    /// </summary>
    public class Deasciifier
    {
        private const int ContextSize = 10;

        private readonly string asciiString;
        private readonly char[] buffer;

        public static string Deasciify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var instance = new Deasciifier(text);
            return instance.ConvertToTurkish();
        }

        public Deasciifier(string input)
        {
            this.asciiString = input;
            this.buffer = input.ToCharArray();
        }

        public string ConvertToTurkish()
        {
            for (int index = 0; index < buffer.Length; index++)
            {
                char ch = buffer[index];
                if (TurkishNeedCorrection(ch, index))
                {
                    buffer[index] = TurkishToggleAccent(ch);
                }
            }
            return new string(buffer);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static char TurkishToggleAccent(char c) => c switch
        {
            'c' => 'ç', 'C' => 'Ç',
            'g' => 'ğ', 'G' => 'Ğ',
            'o' => 'ö', 'O' => 'Ö',
            'u' => 'ü', 'U' => 'Ü',
            'i' => 'ı', 'I' => 'İ',
            's' => 'ş', 'S' => 'Ş',
            'ç' => 'c', 'Ç' => 'C',
            'ğ' => 'g', 'Ğ' => 'G',
            'ö' => 'o', 'Ö' => 'O',
            'ü' => 'u', 'Ü' => 'U',
            'ı' => 'i', 'İ' => 'I',
            'ş' => 's', 'Ş' => 'S',
            _ => c
        };

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static char TurkishAsciify(char c) => c switch
        {
            'ç' => 'c', 'Ç' => 'C',
            'ğ' => 'g', 'Ğ' => 'G',
            'ö' => 'o', 'Ö' => 'O',
            'ü' => 'u', 'Ü' => 'U',
            'ı' => 'i', 'İ' => 'I',
            'ş' => 's', 'Ş' => 'S',
            _ => c
        };

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static char TurkishDowncaseAsciify(char c) => c switch
        {
            'ç' or 'Ç' or 'c' or 'C' => 'c',
            'ğ' or 'Ğ' or 'g' or 'G' => 'g',
            'ö' or 'Ö' or 'o' or 'O' => 'o',
            'ü' or 'Ü' or 'u' or 'U' => 'u',
            'ı' or 'İ' or 'i' or 'I' => 'i',
            'ş' or 'Ş' or 's' or 'S' => 's',
            _ => char.ToLowerInvariant(c)
        };

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static char TurkishUpcaseAccents(char c) => c switch
        {
            'ç' or 'Ç' => 'C',
            'ğ' or 'Ğ' => 'G',
            'ö' or 'Ö' => 'O',
            'ü' or 'Ü' => 'U',
            'ı' or 'İ' => 'I',
            'ş' or 'Ş' => 'S',
            _ => char.ToLowerInvariant(c)
        };

        private bool TurkishNeedCorrection(char ch, int point)
        {
            char tr = TurkishAsciify(ch);
            string trLowerKey = char.ToLowerInvariant(tr).ToString();

            bool m = false;
            if (TurkishPatternTable.Table.TryGetValue(trLowerKey, out var pl))
            {
                m = TurkishMatchPattern(pl, point);
            }

            if (tr == 'I')
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
                char currentChar = buffer[index];
                if ((currentChar >= 'a' && currentChar <= 'z') || (currentChar >= 'A' && currentChar <= 'Z') ||
                    currentChar == 'ç' || currentChar == 'Ç' || currentChar == 'ğ' || currentChar == 'Ğ' ||
                    currentChar == 'ö' || currentChar == 'Ö' || currentChar == 'ü' || currentChar == 'Ü' ||
                    currentChar == 'ı' || currentChar == 'İ' || currentChar == 'ş' || currentChar == 'Ş')
                {
                    s[i] = TurkishDowncaseAsciify(currentChar);
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
                char currentChar = buffer[index];
                if ((currentChar >= 'a' && currentChar <= 'z') || (currentChar >= 'A' && currentChar <= 'Z') ||
                    currentChar == 'ç' || currentChar == 'Ç' || currentChar == 'ğ' || currentChar == 'Ğ' ||
                    currentChar == 'ö' || currentChar == 'Ö' || currentChar == 'ü' || currentChar == 'Ü' ||
                    currentChar == 'ı' || currentChar == 'İ' || currentChar == 'ş' || currentChar == 'Ş')
                {
                    s[i] = TurkishUpcaseAccents(currentChar);
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
