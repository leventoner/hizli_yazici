import express from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import { GoogleGenAI } from '@google/genai';
import { Deasciifier } from './src/services/deasciifier.js';
import { translateFree } from './src/services/freeTranslation.js';
import { improveTurkishTextFree } from './src/services/freeTextImprover.js';
import { extractSingleWord, lookupDictionary } from './src/services/dictionaryService.js';

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = parseInt(process.env.PORT || '3000', 10);
const HOST = '0.0.0.0';

app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.urlencoded({ extended: true, limit: '50mb' }));

// Initial settings
const SETTINGS_FILE = path.join(__dirname, 'settings.json');
let settings = {
  program_name: 'Yazıcı',
  hotkey: 'ctrl+c',
  stt_hotkey: 'ctrl+shift+y',
  stt_duration: 10,
  cooldown: 0.5,
  enable_character_fix: true,
  enable_ai_improve: true,
  enable_speech_to_text: true,
  notify_on_no_change: true,
  color_theme: '#3d5afe',
  floating_menu_duration: 10,
  translation_provider: 'free',
  translation_email: '',
  improve_provider: 'auto', // 'auto' | 'free' | 'gemini'
  auto_copy_on_action: true,
};

if (fs.existsSync(SETTINGS_FILE)) {
  try {
    const raw = fs.readFileSync(SETTINGS_FILE, 'utf-8');
    settings = { ...settings, ...JSON.parse(raw) };
  } catch (err) {
    console.warn('Could not read settings.json, using defaults:', err);
  }
}

async function saveSettingsToFile() {
  try {
    await fs.promises.writeFile(SETTINGS_FILE, JSON.stringify(settings, null, 2), 'utf-8');
  } catch (err) {
    console.warn('Could not save settings.json:', err);
  }
}

// Gemini Client initialization helper
function getGeminiClient() {
  const apiKey = process.env.GEMINI_API_KEY || '';
  if (!apiKey) {
    throw new Error('GEMINI_API_KEY bulunamadı. Lütfen çevre değişkenlerini kontrol edin.');
  }
  return new GoogleGenAI({ apiKey });
}

// ---------------- API ROUTES ----------------

// Health check
app.get('/api/health', (req, res) => {
  res.json({
    status: 'ok',
    hasGeminiKey: Boolean(process.env.GEMINI_API_KEY),
    name: settings.program_name,
    improve_provider: settings.improve_provider,
  });
});

// Settings GET & POST
app.get('/api/settings', (req, res) => {
  res.json({
    ...settings,
    hasGeminiKey: Boolean(process.env.GEMINI_API_KEY),
  });
});

app.post('/api/settings', async (req, res) => {
  const updates = req.body || {};
  settings = { ...settings, ...updates };
  await saveSettingsToFile();
  res.json({ success: true, settings });
});

// Deasciify (Türkçe Karakter Düzeltme)
app.post('/api/deasciify', (req, res) => {
  try {
    const { text } = req.body;
    if (!text || typeof text !== 'string') {
      return res.status(400).json({ error: 'Metin parametresi zorunludur.' });
    }
    const corrected = Deasciifier.deasciify(text);
    const changed = text.trim() !== corrected.trim();
    res.json({
      original: text,
      corrected,
      changed,
    });
  } catch (err: any) {
    res.status(500).json({ error: err.message || 'Düzeltme sırasında hata oluştu.' });
  }
});

// Metin İyileştirme (Tamamen Yerel ve Ücretsiz Kural Motoru - LLM Bağımlılığı Kaldırıldı)
app.post('/api/improve', (req, res) => {
  try {
    const { text } = req.body;
    if (!text || typeof text !== 'string') {
      return res.status(400).json({ error: 'Metin parametresi zorunludur.' });
    }

    const improved = improveTurkishTextFree(text);
    return res.json({
      original: text,
      improved,
      changed: text.trim() !== improved.trim(),
      provider: 'local-free',
      message: 'Tamamen yerel kural motoruyla anında iyileştirildi.',
    });
  } catch (err: any) {
    res.status(500).json({ error: err.message || 'Metin iyileştirme sırasında hata oluştu.' });
  }
});

// Metin İyileştirme - SSE Akış Uç Noktası (Yerel motor tek seferde anında döndürür)
app.post('/api/improve/stream', (req, res) => {
  try {
    const { text } = req.body;
    if (!text || typeof text !== 'string') {
      return res.status(400).json({ error: 'Metin parametresi zorunludur.' });
    }

    res.setHeader('Content-Type', 'text/event-stream');
    res.setHeader('Cache-Control', 'no-cache');
    res.setHeader('Connection', 'keep-alive');

    const improved = improveTurkishTextFree(text);
    res.write(`data: ${JSON.stringify({ chunk: improved, done: true, provider: 'local-free' })}\n\n`);
    res.end();
  } catch (err: any) {
    res.status(500).json({ error: err.message || 'Stream hatası oluştu.' });
  }
});

// Translation (İki yönlü Türkçe ↔ EN, FR veya NL/Flamanca)
app.post('/api/translate', async (req, res) => {
  try {
    const { text, targetLanguage = 'en', provider = settings.translation_provider } = req.body;
    if (!text || typeof text !== 'string') {
      return res.status(400).json({ error: 'Metin parametresi zorunludur.' });
    }

    const lang = targetLanguage.toLowerCase();
    const targetLangName =
      lang === 'nl'
        ? 'Dutch (Flemish / Nederlands)'
        : lang === 'fr'
        ? 'French'
        : 'English';

    // 1. Check if input is a single word -> Trigger Dictionary Mode
    const singleWord = extractSingleWord(text);
    if (singleWord) {
      let geminiAi: GoogleGenAI | undefined;
      if (process.env.GEMINI_API_KEY) {
        try {
          geminiAi = getGeminiClient();
        } catch (err) {
          console.warn('Gemini client init failed for dictionary:', err);
        }
      }

      const dictEntry = await lookupDictionary(
        singleWord,
        (lang === 'fr' || lang === 'nl' ? lang : 'en') as 'en' | 'fr' | 'nl',
        geminiAi,
        settings.translation_email
      );

      const primaryTranslation = dictEntry.meanings[0]?.meaning || dictEntry.rootWord;

      return res.json({
        original: text,
        translated: primaryTranslation,
        targetLanguage: lang,
        provider: geminiAi ? 'gemini' : 'free',
        isDictionary: true,
        dictionary: dictEntry,
      });
    }

    // 2. Multi-word phrases or sentences -> Normal Translation
    if (provider === 'gemini') {
      const ai = getGeminiClient();
      const prompt =
        `You are a translator. Analyze the language of the text between triple quotes below.\n` +
        `- If the text is in Turkish, translate it into natural, fluent ${targetLangName}.\n` +
        `- If the text is in ${targetLangName} (or any other language), translate it into natural, fluent Turkish.\n` +
        'Rules: Preserve the original meaning, tone and line breaks. Do not add explanations, notes or quotes. Return ONLY the translated text.\n' +
        `Text:\n"""\n${text}\n"""`;

      let translated = '';
      try {
        const response = await ai.models.generateContent({
          model: 'gemini-2.5-flash',
          contents: prompt,
        });
        translated = response.text?.trim() || '';
      } catch (geminiErr) {
        console.warn('Gemini translate failed, falling back to free translation:', geminiErr);
        translated = await translateFree(text, lang, settings.translation_email);
      }

      if (translated.startsWith('"""') && translated.endsWith('"""')) {
        translated = translated.slice(3, -3).trim();
      } else if (translated.startsWith('"') && translated.endsWith('"')) {
        translated = translated.slice(1, -1).trim();
      }

      return res.json({
        original: text,
        translated,
        targetLanguage: lang,
        provider: 'gemini',
        isDictionary: false,
      });
    }

    // Default to free translation
    try {
      const translated = await translateFree(text, lang, settings.translation_email);
      return res.json({
        original: text,
        translated,
        targetLanguage: lang,
        provider: 'free',
        isDictionary: false,
      });
    } catch (freeErr: any) {
      if (process.env.GEMINI_API_KEY) {
        const ai = getGeminiClient();
        const prompt =
          `You are a translator. Analyze the language of the text between triple quotes below.\n` +
          `- If the text is in Turkish, translate it into natural, fluent ${targetLangName}.\n` +
          `- If the text is in ${targetLangName} (or any other language), translate it into natural, fluent Turkish.\n` +
          'Rules: Preserve the original meaning, tone and line breaks. Do not add explanations, notes or quotes. Return ONLY the translated text.\n' +
          `Text:\n"""\n${text}\n"""`;

        const response = await ai.models.generateContent({
          model: 'gemini-2.5-flash',
          contents: prompt,
        });
        const translated = response.text?.trim() || '';
        return res.json({
          original: text,
          translated,
          targetLanguage: lang,
          provider: 'gemini-fallback',
          isDictionary: false,
        });
      }
      throw freeErr;
    }
  } catch (err: any) {
    console.error('Translate error:', err);
    res.status(500).json({ error: err.message || 'Çeviri sırasında hata oluştu.' });
  }
});

// Explicit Dictionary endpoint
app.post('/api/dictionary', async (req, res) => {
  try {
    const { word, targetLanguage = 'en' } = req.body;
    if (!word || typeof word !== 'string') {
      return res.status(400).json({ error: 'Kelime parametresi zorunludur.' });
    }

    const lang = targetLanguage.toLowerCase() as 'en' | 'fr' | 'nl';
    let geminiAi: GoogleGenAI | undefined;
    if (process.env.GEMINI_API_KEY) {
      try {
        geminiAi = getGeminiClient();
      } catch {}
    }

    const dictEntry = await lookupDictionary(
      word,
      lang === 'fr' || lang === 'nl' ? lang : 'en',
      geminiAi,
      settings.translation_email
    );

    res.json({
      success: true,
      entry: dictEntry,
    });
  } catch (err: any) {
    res.status(500).json({ error: err.message || 'Sözlük araması sırasında hata oluştu.' });
  }
});

// Speech to text / Audio transcription
app.post('/api/transcribe', async (req, res) => {
  try {
    const { audioData, mimeType = 'audio/webm' } = req.body;
    if (!audioData) {
      return res.status(400).json({ error: 'audioData base64 parametresi zorunludur.' });
    }

    // Clean mimeType: strip codec parameters like ;codecs=opus which Gemini API rejects
    const cleanMime = (mimeType || 'audio/webm').split(';')[0].trim().toLowerCase();
    const base64 = audioData.includes('base64,') ? audioData.split('base64,')[1] : audioData;

    const candidateModels = [
      'gemini-2.5-flash',
      'gemini-2.0-flash',
      'models/gemini-3.8-flash',
      'models/gemini-3.6-flash',
      'gemini-1.5-flash',
    ];

    let transcription = '';
    let lastError: any = null;

    if (process.env.GEMINI_API_KEY) {
      const ai = getGeminiClient();

      for (const modelName of candidateModels) {
        try {
          const timeoutPromise = new Promise((_, reject) =>
            setTimeout(() => reject(new Error(`Transcribe ${modelName} zaman aşımı (8s)`)), 8000)
          );

          const generatePromise = ai.models.generateContent({
            model: modelName,
            contents: [
              {
                role: 'user',
                parts: [
                  {
                    inlineData: {
                      mimeType: cleanMime,
                      data: base64,
                    },
                  },
                  {
                    text: 'Bu ses kaydını dinle ve içindeki konuşmayı tam ve eksiksiz olarak metne çevir. Sadece konuşulan metni döndür, ekleme veya açıklama yapma. Konuşma dilini olduğu gibi koru, dil muhtemelen Türkçedir.',
                  },
                ],
              },
            ],
          });

          const response: any = await Promise.race([generatePromise, timeoutPromise]);
          const text = response.text?.trim();
          if (text) {
            transcription = text;
            break;
          }
        } catch (mErr: any) {
          console.warn(`Transcribe model ${modelName} hatası:`, mErr.status || mErr.message);
          lastError = mErr;
        }
      }
    } else {
      lastError = new Error('GEMINI_API_KEY bulunamadı.');
    }

    if (transcription) {
      return res.json({
        transcription,
        success: true,
      });
    }

    throw new Error(lastError?.message || 'Ses transkripsiyonu tamamlanamadı.');
  } catch (err: any) {
    console.error('Transcribe error:', err);
    res.status(500).json({ error: err.message || 'Ses metne çevrilirken hata oluştu.' });
  }
});

// ---------------- VITE MIDDLEWARE / STATIC SERVING ----------------

async function setupServer() {
  const isProd = process.env.NODE_ENV === 'production';

  if (!isProd) {
    const { createServer: createViteServer } = await import('vite');
    const vite = await createViteServer({
      server: { middlewareMode: true },
      appType: 'spa',
    });
    app.use(vite.middlewares);
  } else {
    const distPath = path.join(__dirname, 'dist');
    if (fs.existsSync(distPath)) {
      app.use(express.static(distPath));
      app.get('*', (req, res) => {
        res.sendFile(path.join(distPath, 'index.html'));
      });
    }
  }

  app.listen(PORT, HOST, () => {
    console.log(`🚀 Hizli Yazici server running on http://${HOST}:${PORT}`);
  });
}

setupServer().catch((err) => {
  console.error('Failed to start server:', err);
  process.exit(1);
});
