import { Deasciifier } from './deasciifier.js';

// Common Turkish spelling mistakes, typos, and colloquial expressions to correct
const COMMON_TURKISH_CORRECTIONS: [RegExp, string][] = [
  // Deasciified essentials if missed
  [/\bcunku\b/gi, 'çünkü'],
  [/\bçünki\b/gi, 'çünkü'],
  [/\btesekkur\b/gi, 'teşekkür'],
  [/\btesekkurler\b/gi, 'teşekkürler'],
  [/\bherkez\b/gi, 'herkes'],
  [/\byalnış\b/gi, 'yanlış'],
  [/\byanlız\b/gi, 'yalnız'],
  [/\bterk etmek\b/gi, 'terk etmek'],
  [/\bterketmek\b/gi, 'terk etmek'],
  [/\bfark etmek\b/gi, 'fark etmek'],
  [/\bfarketmek\b/gi, 'fark etmek'],
  [/\bayırt etmek\b/gi, 'ayırt etmek'],
  [/\bayırdetmek\b/gi, 'ayırt etmek'],
  [/\bsağol\b/gi, 'sağ ol'],
  [/\bsagol\b/gi, 'sağ ol'],
  [/\bhoşçakal\b/gi, 'hoşça kal'],
  [/\bhoscakal\b/gi, 'hoşça kal'],
  [/\bhoşgeldiniz\b/gi, 'hoş geldiniz'],
  [/\bhosgeldiniz\b/gi, 'hoş geldiniz'],
  [/\bhoşbulduk\b/gi, 'hoş bulduk'],
  [/\bhosbulduk\b/gi, 'hoş bulduk'],
  [/\btabiki\b/gi, 'tabii ki'],
  [/\btabi ki\b/gi, 'tabii ki'],
  [/\btabiki de\b/gi, 'tabii ki de'],
  [/\bbencede\b/gi, 'bence de'],
  [/\bsendede\b/gi, 'sen de de'],
  [/\bbendede\b/gi, 'ben de de'],
  [/\blaboratuar\b/gi, 'laboratuvar'],
  [/\borjinal\b/gi, 'orijinal'],
  [/\bkıravat\b/gi, 'kravat'],
  [/\bsüpriz\b/gi, 'sürpriz'],
  [/\begsoz\b/gi, 'egzoz'],
  [/\begzoz\b/gi, 'egzoz'],
  [/\begsozt\b/gi, 'egzoz'],
  [/\bdöküman\b/gi, 'doküman'],
  [/\bşöför\b/gi, 'şoför'],
  [/\bsofor\b/gi, 'şoför'],
  [/\bkirbit\b/gi, 'kibrit'],
  [/\bkirpik\b/gi, 'kirpik'],
  [/\bkiprik\b/gi, 'kirpik'],
  [/\bparlemento\b/gi, 'parlamento'],
  [/\bünvan\b/gi, 'unvan'],
  [/\bherhangibir\b/gi, 'herhangi bir'],
  [/\bbir kaç\b/gi, 'birkaç'],
  [/\bbir çok\b/gi, 'birçok'],
  [/\bhiç bir\b/gi, 'hiçbir'],
  [/\bhiç birşey\b/gi, 'hiçbir şey'],
  [/\bhiçbirşey\b/gi, 'hiçbir şey'],
  [/\bbirşey\b/gi, 'bir şey'],
  [/\bherşey\b/gi, 'her şey'],
  [/\bçokşey\b/gi, 'çok şey'],
  [/\bşeyler\b/gi, 'şeyler'],
  // Colloquial to formal Turkish
  [/\bbide\b/gi, 'bir de'],
  [/\bbi de\b/gi, 'bir de'],
  [/\bgelcem\b/gi, 'geleceğim'],
  [/\bgelçem\b/gi, 'geleceğim'],
  [/\bgitcem\b/gi, 'gideceğim'],
  [/\bgitçem\b/gi, 'gideceğim'],
  [/\byapcam\b/gi, 'yapacağım'],
  [/\byapçam\b/gi, 'yapacağım'],
  [/\bbakcam\b/gi, 'bakacağım'],
  [/\bbakçam\b/gi, 'bakacağım'],
  [/\balcam\b/gi, 'alacağım'],
  [/\balçam\b/gi, 'alacağım'],
  [/\bvercem\b/gi, 'vereceğim'],
  [/\bverçem\b/gi, 'vereceğim'],
  [/\bgelicem\b/gi, 'geleceğim'],
  [/\bgidicem\b/gi, 'gideceğim'],
  [/\byapıcam\b/gi, 'yapacağım'],
  [/\bbakıcam\b/gi, 'bakacağım'],
  [/\balıcam\b/gi, 'alacağım'],
  [/\bvericem\b/gi, 'vereceğim'],
  [/\bbilmiom\b/gi, 'bilmiyorum'],
  [/\bbilmiyom\b/gi, 'bilmiyorum'],
  [/\bistemiom\b/gi, 'istemiyorum'],
  [/\bistemiyom\b/gi, 'istemiyorum'],
  [/\bgelio\b/gi, 'geliyor'],
  [/\bgeliyo\b/gi, 'geliyor'],
  [/\bgidio\b/gi, 'gidiyor'],
  [/\bgidiyo\b/gi, 'gidiyor'],
  [/\byapıo\b/gi, 'yapıyor'],
  [/\byapıyo\b/gi, 'yapıyor'],
  [/\bnapıyorsun\b/gi, 'ne yapıyorsun'],
  [/\bnapıyon\b/gi, 'ne yapıyorsun'],
  [/\bnasılsın\b/gi, 'nasılsın'],
  [/\biym\b/gi, 'iyiyim'],
  [/\biiyim\b/gi, 'iyiyim'],
  [/\bii\b/gi, 'iyi'],
  [/\baynen oyle\b/gi, 'aynen öyle'],
  [/\bproblem deil\b/gi, 'problem değil'],
  [/\bsorun deil\b/gi, 'sorun değil'],
  [/\bdegil\b/gi, 'değil'],
  [/\bdeil\b/gi, 'değil'],
];

/**
 * Normalizes punctuation, spacing, and sentence capitalization.
 */
function normalizePunctuationAndSpacing(input: string): string {
  let res = input;

  // Normalize multiple spaces and tabs to single space
  res = res.replace(/[ \t]+/g, ' ');

  // Fix spaces before punctuation (e.g. "evet , cunku ." -> "evet, cunku.")
  res = res.replace(/\s+([,.:;!?])/g, '$1');

  // Fix missing space after punctuation (e.g. "evet,cunku" -> "evet, cunku")
  res = res.replace(/([,.:;!?])([^\s0-9,.:;!?)"'»’])/g, '$1 $2');

  // Fix spaces around quotes and brackets
  res = res.replace(/\(\s+/g, '(').replace(/\s+\)/g, ')');
  res = res.replace(/\[\s+/g, '[').replace(/\s+\]/g, ']');

  // Fix multiple punctuation marks (except ..., ?!, etc.)
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
 * Requires NO API key, runs instantaneously, and never fails with 503 or quota errors.
 */
export function improveTurkishTextFree(input: string): string {
  if (!input || !input.trim()) return input;

  // Step 1: Deasciify (recover ç, ğ, ı, ö, ş, ü accurately with 13,463 rules)
  let text = Deasciifier.deasciify(input);

  // Step 2: Apply common spelling and grammar corrections
  for (const [regex, replacement] of COMMON_TURKISH_CORRECTIONS) {
    text = text.replace(regex, replacement);
  }

  // Step 3: Normalize punctuation, spaces, and capitalization
  text = normalizePunctuationAndSpacing(text);

  return text;
}
