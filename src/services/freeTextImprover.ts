import { Deasciifier } from './deasciifier.js';

// Common Turkish spelling mistakes, typos, and colloquial expressions to correct
const WORD_REPLACEMENTS: Record<string, string> = {
  cunku: 'çünkü',
  çünki: 'çünkü',
  tesekkur: 'teşekkür',
  tesekkurler: 'teşekkürler',
  herkez: 'herkes',
  yalnış: 'yanlış',
  yanlız: 'yalnız',
  terketmek: 'terk etmek',
  farketmek: 'fark etmek',
  ayırdetmek: 'ayırt etmek',
  sağol: 'sağ ol',
  sagol: 'sağ ol',
  hoşçakal: 'hoşça kal',
  hoscakal: 'hoşça kal',
  hoşgeldiniz: 'hoş geldiniz',
  hosgeldiniz: 'hoş geldiniz',
  hoşbulduk: 'hoş bulduk',
  hosbulduk: 'hoş bulduk',
  tabiki: 'tabii ki',
  'tabi ki': 'tabii ki',
  'tabiki de': 'tabii ki de',
  bencede: 'bence de',
  sendede: 'sen de de',
  bendede: 'ben de de',
  laboratuar: 'laboratuvar',
  orjinal: 'orijinal',
  kıravat: 'kravat',
  süpriz: 'sürpriz',
  egsoz: 'egzoz',
  egzoz: 'egzoz',
  egsozt: 'egzoz',
  döküman: 'doküman',
  şöför: 'şoför',
  sofor: 'şoför',
  kirbit: 'kibrit',
  kiprik: 'kirpik',
  parlemento: 'parlamento',
  ünvan: 'unvan',
  herhangibir: 'herhangi bir',
  'bir kaç': 'birkaç',
  'bir çok': 'birçok',
  'hiç bir': 'hiçbir',
  'hiç birşey': 'hiçbir şey',
  hiçbirşey: 'hiçbir şey',
  birşey: 'bir şey',
  herşey: 'her şey',
  çokşey: 'çok şey',
  bide: 'bir de',
  'bi de': 'bir de',
  gelcem: 'geleceğim',
  gelçem: 'geleceğim',
  gitcem: 'gideceğim',
  gitçem: 'gideceğim',
  yapcam: 'yapacağım',
  yapçam: 'yapacağım',
  bilmiom: 'bilmiyorum',
  bilmiyom: 'bilmiyorum',
  gelio: 'geliyor',
  geliyo: 'geliyor',
  gidio: 'gidiyor',
  gidiyo: 'gidiyor',
  yapıo: 'yapıyor',
  yapıyo: 'yapıyor',
  napıyorsun: 'ne yapıyorsun',
  napıyon: 'ne yapıyorsun',
  degil: 'değil',
  deil: 'değil',
};

// Sort longest phrases first to prevent sub-string clashes
const sortedKeys = Object.keys(WORD_REPLACEMENTS).sort((a, b) => b.length - a.length);
const COMPOSITE_REGEX = new RegExp(`\\b(${sortedKeys.join('|')})\\b`, 'gi');

function preserveCase(original: string, replacement: string): string {
  if (!original || !replacement) return replacement;
  if (original === original.toLocaleUpperCase('tr-TR')) {
    return replacement.toLocaleUpperCase('tr-TR');
  }
  const firstChar = original[0];
  if (firstChar === firstChar.toLocaleUpperCase('tr-TR')) {
    return replacement[0].toLocaleUpperCase('tr-TR') + replacement.slice(1);
  }
  return replacement;
}

function normalizePunctuationAndSpacing(input: string): string {
  let res = input;

  // Collapse excessive spaces
  res = res.replace(/[ \t]+/g, ' ');

  // Clean spacing around punctuation: no space before, one space after
  res = res.replace(/\s+([,.:;!?])/g, '$1');
  res = res.replace(/([,.:;!?])([^\s0-9,.:;!?])/g, '$1 $2');

  // Fix multiple punctuation marks
  res = res.replace(/,{2,}/g, ',');
  res = res.replace(/;{2,}/g, ';');
  res = res.replace(/:{2,}/g, ':');
  res = res.replace(/\.{4,}/g, '...');

  // Normalize Turkish apostrophe
  res = res.replace(/([a-zA-ZçğıöşüÇĞİÖŞÜ])\s*['’`´]\s*([a-zA-ZçğıöşüÇĞİÖŞÜ])/g, "$1'$2");

  // Capitalize first letter of every sentence
  res = res.replace(/(?:^|[.!?]\s+)([a-zçğıöşü])/g, (match, char) => {
    return match.slice(0, -1) + char.toLocaleUpperCase('tr-TR');
  });

  // Ensure first character of entire text is capitalized
  if (res.length > 0) {
    const firstChar = res[0];
    if (/[a-zçğıöşü]/.test(firstChar)) {
      res = firstChar.toLocaleUpperCase('tr-TR') + res.slice(1);
    }
  }

  // If text looks like a complete sentence without ending punctuation, append period
  const trimmed = res.trim();
  if (
    trimmed.length > 5 &&
    !/[.!?…:;]$/.test(trimmed) &&
    !trimmed.includes('\n')
  ) {
    res = trimmed + '.';
  }

  return res;
}

/**
 * Completely free, local Turkish text improvement and grammar check engine.
 * Requires NO API key, runs in a single pass, and preserves casing.
 */
export function improveTurkishTextFree(input: string): string {
  if (!input || !input.trim()) return input;

  // Step 1: Deasciify (recover ç, ğ, ı, ö, ş, ü accurately with 13,463 rules)
  let text = Deasciifier.deasciify(input);

  // Step 2: Single-pass composite regex replacement with casing preservation
  text = text.replace(COMPOSITE_REGEX, (match) => {
    const lowerKey = match.toLowerCase();
    const replacement = WORD_REPLACEMENTS[lowerKey];
    return replacement ? preserveCase(match, replacement) : match;
  });

  // Step 3: Normalize punctuation, spaces, and capitalization
  text = normalizePunctuationAndSpacing(text);

  return text;
}
