const TurkishCharsRegex = /[çğıöşüÇĞİÖŞÜ]/;
const TurkishDistinctCharsRegex = /[ğĞıİşŞ]/;
const FrenchCharsRegex = /[éèêëàâîïôùûÉÈÊËÀÂÎÏÔÙÛœæŒÆ]/;

const FrenchStopWords = new Set([
  'le', 'la', 'les', 'de', 'des', 'du', 'un', 'une', 'et', 'est', 'en', 'que', 'qui',
  'dans', 'pour', 'pas', 'sur', 'ce', 'cette', 'ces', 'avec', 'vous', 'nous', 'ils', 'elle', 'il', 'je', 'tu'
]);

const TurkishStopWords = new Set([
  've', 'bir', 'bu', 'için', 'ile', 'ama', 'fakat', 'çok', 'daha', 'gibi', 'olarak',
  'de', 'da', 'ki', 'mi', 'mı', 'mu', 'mü', 'ne', 'nasıl', 'neden', 'çünkü',
  'ben', 'sen', 'biz', 'siz', 'onlar', 'değil', 'var', 'yok', 'her', 'kadar', 'sonra'
]);

export function looksFrench(text: string): boolean {
  if (!text || !text.trim()) return false;
  if (FrenchCharsRegex.test(text)) return true;

  const words = text.toLowerCase().split(/[^a-zçéèêëàâîïôùûœæ]+/).filter(w => w.length > 0);
  if (words.length === 0) return false;

  const hits = words.filter(w => FrenchStopWords.has(w)).length;
  return hits >= 2 || (words.length <= 3 && hits >= 1);
}

export function looksTurkish(text: string): boolean {
  if (!text || !text.trim()) return false;
  if (TurkishCharsRegex.test(text)) return true;

  const words = text.toLowerCase().split(/[^a-zçğıöşü]+/).filter(w => w.length > 0);
  if (words.length === 0) return false;

  const hits = words.filter(w => TurkishStopWords.has(w)).length;
  return hits >= 2 || (words.length <= 3 && hits >= 1);
}

export function detectTargetLanguage(text: string, foreignLang = 'en'): string {
  if (foreignLang.toLowerCase() === 'fr') {
    if (TurkishDistinctCharsRegex.test(text)) return 'fr';
    if (FrenchCharsRegex.test(text) || looksFrench(text)) return 'tr';
    return looksTurkish(text) ? 'fr' : 'tr';
  }

  return looksTurkish(text) ? foreignLang : 'tr';
}

interface GoogleResult {
  translatedText: string;
  detectedSourceLanguage?: string;
}

async function tryGoogleKeyless(text: string, target: string): Promise<GoogleResult | null> {
  try {
    const uri = `https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=${encodeURIComponent(target)}&q=${encodeURIComponent(text)}`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 8000);

    const res = await fetch(uri, {
      signal: controller.signal,
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36',
      },
    });
    clearTimeout(timer);

    if (!res.ok) return null;
    const json = await res.json();
    return parseGoogleKeylessResult(json);
  } catch {
    return null;
  }
}

function parseGoogleKeylessResult(json: any): GoogleResult | null {
  if (!json) return null;

  try {
    if (json.sentences && Array.isArray(json.sentences)) {
      const translated = json.sentences.map((s: any) => s.trans || '').join('').trim();
      return {
        translatedText: translated,
        detectedSourceLanguage: json.src || undefined,
      };
    }

    if (Array.isArray(json)) {
      let translated = '';
      let detectedLang: string | undefined;

      for (const el of json) {
        if (Array.isArray(el) && el.length > 0) {
          if (typeof el[0] === 'string') translated += el[0];
          if (el.length > 1 && typeof el[1] === 'string' && !detectedLang) {
            detectedLang = el[1];
          }
        } else if (typeof el === 'string') {
          if (['tr', 'en', 'fr', 'de', 'es'].includes(el.toLowerCase()) && !detectedLang) {
            detectedLang = el.toLowerCase();
          } else {
            translated += el;
          }
        }
      }

      return {
        translatedText: translated.trim(),
        detectedSourceLanguage: detectedLang,
      };
    }
  } catch {
    return null;
  }

  return null;
}

function splitByBytes(text: string, maxBytes = 450): string[] {
  const chunks: string[] = [];
  if (!text) return chunks;

  const sentences = text.split(/(?<=[\.\!\?\n])\s+/);
  let current = '';

  for (const sentence of sentences) {
    const encoder = new TextEncoder();
    const candidate = current.length === 0 ? sentence : `${current} ${sentence}`;
    if (encoder.encode(candidate).length <= maxBytes) {
      current = candidate;
    } else {
      if (current.length > 0) {
        chunks.push(current);
        current = '';
      }
      if (encoder.encode(sentence).length <= maxBytes) {
        current = sentence;
      } else {
        // Break long sentence
        const words = sentence.split(' ');
        for (const word of words) {
          const wordCandidate = current.length === 0 ? word : `${current} ${word}`;
          if (encoder.encode(wordCandidate).length <= maxBytes) {
            current = wordCandidate;
          } else {
            if (current.length > 0) chunks.push(current);
            current = word;
          }
        }
      }
    }
  }

  if (current.length > 0) chunks.push(current);
  return chunks;
}

async function tryMyMemory(text: string, target: string, email?: string): Promise<string | null> {
  try {
    const langPair = `autodetect|${target}`;
    const chunks = splitByBytes(text, 450);
    const translatedParts: string[] = [];

    for (const chunk of chunks) {
      let uri = `https://api.mymemory.translated.net/get?q=${encodeURIComponent(chunk)}&langpair=${encodeURIComponent(langPair)}`;
      if (email && email.trim()) {
        uri += `&de=${encodeURIComponent(email.trim())}`;
      }

      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), 8000);
      const res = await fetch(uri, { signal: controller.signal });
      clearTimeout(timer);

      if (!res.ok) return null;
      const json = await res.json();
      const value = json?.responseData?.translatedText;
      if (
        !value ||
        typeof value !== 'string' ||
        value.toUpperCase().startsWith('MYMEMORY') ||
        value.toUpperCase().startsWith('QUERY LENGTH LIMIT')
      ) {
        return null;
      }
      translatedParts.push(value.trim());
    }

    return translatedParts.join(' ');
  } catch {
    return null;
  }
}

export async function translateFree(text: string, foreignLang = 'en', email?: string): Promise<string> {
  if (!text || !text.trim()) return text;

  let target = detectTargetLanguage(text, foreignLang);

  let googleResult = await tryGoogleKeyless(text, target);

  // If detected source language doesn't match heuristic expectation, refine target
  const detected = (googleResult?.detectedSourceLanguage || '').trim().toLowerCase();
  if (detected === 'tr' && target === 'tr') {
    target = foreignLang;
    googleResult = await tryGoogleKeyless(text, target);
  } else if (detected === foreignLang && target !== 'tr') {
    target = 'tr';
    googleResult = await tryGoogleKeyless(text, target);
  }

  if (googleResult && googleResult.translatedText) {
    return googleResult.translatedText;
  }

  const memoryResult = await tryMyMemory(text, target, email);
  if (memoryResult) {
    return memoryResult;
  }

  throw new Error('Ücretsiz çeviri servislerine ulaşılamadı. Lütfen bağlantınızı veya Gemini sağlayıcısını kontrol edin.');
}
