export interface AppSettings {
  program_name: string;
  hotkey: string;
  stt_hotkey: string;
  stt_duration: number;
  cooldown: number;
  enable_character_fix: boolean;
  enable_ai_improve: boolean;
  enable_speech_to_text: boolean;
  notify_on_no_change: boolean;
  color_theme: string;
  floating_menu_duration: number;
  translation_provider: 'free' | 'gemini';
  translation_email: string;
  improve_provider: 'auto' | 'free' | 'gemini';
  auto_copy_on_action: boolean;
  hasGeminiKey?: boolean;
}

export interface ToastMessage {
  id: string;
  title: string;
  message: string;
  color: string;
  timestamp: number;
}

export interface DictionaryExample {
  source: string;
  target: string;
}

export interface DictionaryMeaning {
  meaning: string;
  partOfSpeech?: string;
  definition?: string;
  examples?: DictionaryExample[];
}

export interface DictionaryEntry {
  originalWord: string;
  rootWord: string;
  partOfSpeech: string;
  phonetic?: string;
  grammaticalNote?: string;
  meanings: DictionaryMeaning[];
  synonyms?: string[];
  targetLang: 'en' | 'fr' | 'nl';
  sourceLang?: string;
}

export interface HistoryItem {
  id: string;
  timestamp: Date;
  action: 'deasciify' | 'improve' | 'translate_en' | 'translate_fr' | 'translate_nl' | 'stt';
  originalText: string;
  resultText: string;
  changed: boolean;
  provider?: string;
  isDictionary?: boolean;
}

export interface TranslationModalState {
  isOpen: boolean;
  originalText: string;
  translatedText: string;
  targetLang: 'en' | 'fr' | 'nl';
  provider?: string;
  isDictionary?: boolean;
  dictionaryData?: DictionaryEntry | null;
}
