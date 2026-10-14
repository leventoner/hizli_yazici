import rawPatterns from '../data/turkishPatterns.json';

const TurkishPatternTable: Record<string, Record<string, number>> = rawPatterns as Record<string, Record<string, number>>;

const ContextSize = 10;

const TurkishAsciifyTable: Record<string, string> = {
  'ç': 'c', 'Ç': 'C',
  'ğ': 'g', 'Ğ': 'G',
  'ö': 'o', 'Ö': 'O',
  'ü': 'u', 'Ü': 'U',
  'ı': 'i', 'İ': 'I',
  'ş': 's', 'Ş': 'S',
};

const TurkishToggleAccentTable: Record<string, string> = {
  // ASCII -> Turkish
  'c': 'ç', 'C': 'Ç',
  'g': 'ğ', 'G': 'Ğ',
  'o': 'ö', 'O': 'Ö',
  'u': 'ü', 'U': 'Ü',
  'i': 'ı', 'I': 'İ',
  's': 'ş', 'S': 'Ş',
  // Turkish -> ASCII
  'ç': 'c', 'Ç': 'C',
  'ğ': 'g', 'Ğ': 'G',
  'ö': 'o', 'Ö': 'O',
  'ü': 'u', 'Ü': 'U',
  'ı': 'i', 'İ': 'I',
  'ş': 's', 'Ş': 'S',
};

const TurkishDowncaseAsciifyTable: Record<string, string> = {};
const TurkishUpcaseAccentsTable: Record<string, string> = {};

// Initialize downcase asciify table
for (let c = 65; c <= 90; c++) {
  const upper = String.fromCharCode(c);
  const lower = upper.toLowerCase();
  TurkishDowncaseAsciifyTable[upper] = lower;
  TurkishDowncaseAsciifyTable[lower] = lower;
}
TurkishDowncaseAsciifyTable['ç'] = 'c';
TurkishDowncaseAsciifyTable['Ç'] = 'c';
TurkishDowncaseAsciifyTable['ğ'] = 'g';
TurkishDowncaseAsciifyTable['Ğ'] = 'g';
TurkishDowncaseAsciifyTable['ö'] = 'o';
TurkishDowncaseAsciifyTable['Ö'] = 'o';
TurkishDowncaseAsciifyTable['ü'] = 'u';
TurkishDowncaseAsciifyTable['Ü'] = 'u';
TurkishDowncaseAsciifyTable['ı'] = 'i';
TurkishDowncaseAsciifyTable['İ'] = 'i';
TurkishDowncaseAsciifyTable['ş'] = 's';
TurkishDowncaseAsciifyTable['Ş'] = 's';

// Initialize upcase accents table
for (let c = 65; c <= 90; c++) {
  const upper = String.fromCharCode(c);
  const lower = upper.toLowerCase();
  TurkishUpcaseAccentsTable[upper] = lower;
  TurkishUpcaseAccentsTable[lower] = lower;
}
TurkishUpcaseAccentsTable['ç'] = 'C';
TurkishUpcaseAccentsTable['Ç'] = 'C';
TurkishUpcaseAccentsTable['ğ'] = 'G';
TurkishUpcaseAccentsTable['Ğ'] = 'G';
TurkishUpcaseAccentsTable['ö'] = 'O';
TurkishUpcaseAccentsTable['Ö'] = 'O';
TurkishUpcaseAccentsTable['ü'] = 'U';
TurkishUpcaseAccentsTable['Ü'] = 'U';
TurkishUpcaseAccentsTable['ı'] = 'I';
TurkishUpcaseAccentsTable['İ'] = 'I';
TurkishUpcaseAccentsTable['ş'] = 'S';
TurkishUpcaseAccentsTable['Ş'] = 'S';

export class Deasciifier {
  private asciiString: string;
  private convertedString: string;

  constructor(input: string) {
    this.asciiString = input;
    this.convertedString = input;
  }

  public convertToTurkish(): string {
    const chars = this.convertedString.split('');
    for (let index = 0; index < chars.length; index++) {
      const ch = chars[index];
      if (this.turkishNeedCorrection(ch, index)) {
        chars[index] = this.turkishToggleAccent(ch);
        this.convertedString = chars.join('');
      }
    }
    return this.convertedString;
  }

  private turkishToggleAccent(c: string): string {
    return TurkishToggleAccentTable[c] || c;
  }

  private turkishNeedCorrection(ch: string, point: number): boolean {
    const tr = TurkishAsciifyTable[ch] || ch;
    const trLower = tr.toLowerCase();

    let m = false;
    const pl = TurkishPatternTable[trLower];
    if (pl) {
      m = this.turkishMatchPattern(pl, point);
    }

    if (tr === 'I') {
      return ch === tr ? !m : m;
    } else {
      return ch === tr ? m : !m;
    }
  }

  private turkishMatchPattern(dlist: Record<string, number>, point: number): boolean {
    let rank = 2 * Object.keys(dlist).length;
    const context = this.turkishGetContext(ContextSize, point);

    let start = 0;
    const contextLen = context.length;

    while (start <= ContextSize) {
      let end = 1 + ContextSize;
      while (end <= contextLen) {
        const s = context.substring(start, end);
        if (s in dlist) {
          const r = dlist[s];
          if (Math.abs(r) < Math.abs(rank)) {
            rank = r;
          }
        }
        end++;
      }
      start++;
    }

    return rank > 0;
  }

  private turkishGetContext(size: number, point: number): string {
    const s: string[] = new Array(1 + (2 * size)).fill(' ');
    s[size] = 'X';

    let i = size + 1;
    let space = false;
    let index = point + 1;

    // Right context
    while (i < s.length && !space && index < this.asciiString.length) {
      const currentChar = this.convertedString[index];
      const x = TurkishDowncaseAsciifyTable[currentChar];
      if (x) {
        s[i] = x[0];
        i++;
        space = false;
      } else {
        if (!space) {
          s[i] = ' ';
          i++;
          space = true;
        }
      }
      index++;
    }

    const rightEnd = i;

    // Left context
    index = point - 1;
    i = size - 1;
    space = false;

    while (i >= 0 && index >= 0) {
      const currentChar = this.convertedString[index];
      const x = TurkishUpcaseAccentsTable[currentChar];
      if (x) {
        s[i] = x[0];
        i--;
        space = false;
      } else {
        if (!space) {
          s[i] = ' ';
          i--;
          space = true;
        }
      }
      index--;
    }

    return s.slice(0, rightEnd).join('');
  }

  public static deasciify(text: string): string {
    if (!text || !text.trim()) return text;
    const instance = new Deasciifier(text);
    return instance.convertToTurkish();
  }
}
