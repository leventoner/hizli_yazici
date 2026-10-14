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
  hasGeminiKey?: boolean;
}

export interface ToastMessage {
  id: string;
  title: string;
  message: string;
  color: string;
  timestamp: number;
}

export interface HistoryItem {
  id: string;
  timestamp: Date;
  action: 'deasciify' | 'improve' | 'translate_en' | 'translate_fr' | 'stt';
  originalText: string;
  resultText: string;
  changed: boolean;
  provider?: string;
}
