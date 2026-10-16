import { GoogleGenAI } from '@google/genai';
import { DictionaryEntry } from '../types';
import { translateFree } from './freeTranslation';

/**
 * Checks if a given text represents a single word suitable for dictionary lookup.
 * Strips punctuation, quotes, brackets, and whitespace.
 */
export function extractSingleWord(text: string): string | null {
  if (!text) return null;
  const trimmed = text.trim();
  // Strip common surrounding punctuation, quotes, and brackets
  const cleaned = trimmed.replace(/^[\s"'«»„“"‘’\(\)\[\]\{\}\.,;:!?\-_—–]+|[\s"'«»„“"‘’\(\)\[\]\{\}\.,;:!?\-_—–]+$/g, '');
  if (!cleaned) return null;
  // If there are internal spaces or line breaks, it's a phrase or sentence, not a single word
  if (/\s/.test(cleaned)) return null;
  return cleaned;
}

/**
 * Common Turkish suffix patterns to derive lemma/root as fallback
 */
function heuristicTurkishStem(word: string): { root: string; note: string } {
  const lower = word.toLowerCase();

  // Accusative (-i, -ı, -u, -ü, -yi, -yı, -yu, -yü)
  if (/(?:[aeıioöuü]y[ıiuü]|[ıiuü])$/.test(lower) && lower.length > 3) {
    if (lower.endsWith('yi') || lower.endsWith('yı') || lower.endsWith('yu') || lower.endsWith('yü')) {
      return { root: word.slice(0, -2), note: '-i hali (belirtme durumu)' };
    }
    // E.g., filmi -> film, evi -> ev
    return { root: word.slice(0, -1), note: '-i hali (belirtme durumu)' };
  }

  // Dative (-e, -a, -ye, -ya)
  if (/(?:ye|ya|[ea])$/.test(lower) && lower.length > 3) {
    if (lower.endsWith('ye') || lower.endsWith('ya')) {
      return { root: word.slice(0, -2), note: '-e hali (yönelme durumu)' };
    }
    return { root: word.slice(0, -1), note: '-e hali (yönelme durumu)' };
  }

  // Locative (-de, -da, -te, -ta)
  if (/(?:de|da|te|ta)$/.test(lower) && lower.length > 3) {
    return { root: word.slice(0, -2), note: '-de hali (bulunma durumu)' };
  }

  // Ablative (-den, -dan, -ten, -tan)
  if (/(?:den|dan|ten|tan)$/.test(lower) && lower.length > 4) {
    return { root: word.slice(0, -3), note: '-den hali (ayrılma durumu)' };
  }

  // Plural (-ler, -lar)
  if (/(?:ler|lar)$/.test(lower) && lower.length > 4) {
    return { root: word.slice(0, -3), note: 'çoğul eki (-ler/-lar)' };
  }

  // Genitive (-in, -ın, -un, -ün, -nin, -nın, -nun, -nün)
  if (/(?:[ıiuü]n|n[ıiuü]n)$/.test(lower) && lower.length > 4) {
    if (lower.endsWith('nin') || lower.endsWith('nın') || lower.endsWith('nun') || lower.endsWith('nün')) {
      return { root: word.slice(0, -3), note: 'tamlayan / ilgi eki (-in/-nin)' };
    }
    return { root: word.slice(0, -2), note: 'tamlayan / ilgi eki (-in)' };
  }

  return { root: word, note: 'yalın hal (kök)' };
}

/**
 * Look up dictionary details using Gemini AI (structured bilingual lexicography),
 * with a high-fidelity rule-based fallback if offline.
 */
export async function lookupDictionary(
  word: string,
  targetLang: 'en' | 'fr' | 'nl',
  aiClient?: GoogleGenAI,
  email?: string
): Promise<DictionaryEntry> {
  const cleanWord = extractSingleWord(word) || word.trim();
  const targetLangNames: Record<'en' | 'fr' | 'nl', string> = {
    en: 'English (İngilizce)',
    fr: 'French (Fransızca)',
    nl: 'Dutch / Flemish (Hollandaca / Flamanca)',
  };

  const targetLangName = targetLangNames[targetLang] || 'English';

  // 1. Try Gemini AI for complete morphological analysis & examples
  if (aiClient) {
    try {
      const prompt =
        `You are a bilingual lexicographer and smart dictionary engine.\n` +
        `Analyze the single word: "${cleanWord}".\n` +
        `Target Language: ${targetLangName}.\n\n` +
        `Task:\n` +
        `1. Identify whether "${cleanWord}" is in Turkish or in ${targetLangName}.\n` +
        `2. Find the fundamental root/lemma (kök/yalın hali) of this word (e.g. "filmi" -> root "film"; "koşarken" -> root "koşmak"; "went" -> root "go"; "kijken" -> root "kijken").\n` +
        `3. If the word has inflectional suffixes (hal eki, zaman eki, iyelik eki vb.), explain it in 'grammaticalNote' (e.g. "-i belirtme durumu (accusative case)").\n` +
        `4. Determine part of speech (isim / noun, fiil / verb, sıfat / adjective, zarf / adverb, etc.).\n` +
        `5. Provide accurate translations and meanings of the ROOT word into the target language (if input is Turkish, explain in ${targetLangName}; if input is ${targetLangName}, explain in Turkish).\n` +
        `6. Provide 1 to 3 realistic example sentences demonstrating usage of the word with natural, fluent bilingual translations.\n` +
        `7. Include relevant synonyms if available.\n\n` +
        `Return ONLY valid JSON with this structure:\n` +
        `{\n` +
        `  "originalWord": "${cleanWord}",\n` +
        `  "rootWord": "string",\n` +
        `  "partOfSpeech": "string",\n` +
        `  "phonetic": "string",\n` +
        `  "grammaticalNote": "string",\n` +
        `  "meanings": [\n` +
        `    {\n` +
        `      "meaning": "primary translations separated by commas",\n` +
        `      "partOfSpeech": "noun/verb/adjective etc.",\n` +
        `      "definition": "concise definition or context",\n` +
        `      "examples": [\n` +
        `        {\n` +
        `          "source": "example sentence in source language",\n` +
        `          "target": "translation in target language"\n` +
        `        }\n` +
        `      ]\n` +
        `    }\n` +
        `  ],\n` +
        `  "synonyms": ["string"]\n` +
        `}`;

      const response = await aiClient.models.generateContent({
        model: 'gemini-2.5-flash',
        contents: prompt,
        config: {
          responseMimeType: 'application/json',
        },
      });

      const responseText = response.text?.trim() || '';
      if (responseText) {
        const parsed = JSON.parse(responseText);
        return {
          originalWord: parsed.originalWord || cleanWord,
          rootWord: parsed.rootWord || cleanWord,
          partOfSpeech: parsed.partOfSpeech || 'Kelime',
          phonetic: parsed.phonetic || undefined,
          grammaticalNote: parsed.grammaticalNote || undefined,
          meanings: Array.isArray(parsed.meanings) ? parsed.meanings : [],
          synonyms: Array.isArray(parsed.synonyms) ? parsed.synonyms : [],
          targetLang,
        };
      }
    } catch (aiErr) {
      console.warn('Gemini dictionary lookup failed, falling back to free stemmer & translator:', aiErr);
    }
  }

  // 2. Fallback: Free translation & heuristic stemming
  const { root, note } = heuristicTurkishStem(cleanWord);
  let translatedMeaning = '';
  try {
    translatedMeaning = await translateFree(root, targetLang, email);
  } catch {
    translatedMeaning = cleanWord;
  }

  return {
    originalWord: cleanWord,
    rootWord: root,
    partOfSpeech: 'Kelime / Sözcük',
    grammaticalNote: note !== 'yalın hal (kök)' ? note : undefined,
    meanings: [
      {
        meaning: translatedMeaning,
        definition: `"${root}" sözcüğünün ${targetLang.toUpperCase()} karşılığı.`,
        examples: [
          {
            source: `${cleanWord} kelimesini cümle içinde kullandım.`,
            target: `Translation for: ${cleanWord}`,
          },
        ],
      },
    ],
    synonyms: [],
    targetLang,
  };
}
