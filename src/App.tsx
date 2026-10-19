import React, { useState, useEffect, useRef } from 'react';
import {
  Pencil,
  Sparkles,
  Globe,
  Mic,
  Copy,
  RotateCcw,
  Trash2,
  Settings,
  History,
  Check,
  Zap,
  Info,
  ChevronRight,
  Layers,
  ArrowRightLeft,
  Volume2,
} from 'lucide-react';
import { AppSettings, ToastMessage, HistoryItem, TranslationModalState } from './types';
import { Deasciifier } from './services/deasciifier';
import { improveTurkishTextFree } from './services/freeTextImprover';
import { FloatingMenu } from './components/FloatingMenu';
import { SettingsModal } from './components/SettingsModal';
import { NotificationToast } from './components/NotificationToast';
import { AudioRecorderModal } from './components/AudioRecorderModal';
import { DiffViewer } from './components/DiffViewer';
import { TranslationResultModal } from './components/TranslationResultModal';
import { extractSingleWord } from './services/dictionaryService';

const PRESETS = [
  {
    label: 'Eksik Türkçe Karakterler',
    description: 'guzel, sirket, agac, cocuk, tesekkurler',
    badge: 'Karakter Düzelt',
    color: '#10b981',
    text: 'Bu aksam guzel bir sirket yemeginde arkadaslarimizla toplandik. Ogretmenimiz ve calisma arkadaslarimiz cok icten tesekkur ettiler. Agaclardan elma topladik, cocuklar cok eglendi.',
  },
  {
    label: 'Dilbilgisi ve Anlatım Bozukluğu',
    description: 'Akıcılık, noktalama ve yazım iyileştirme',
    badge: 'AI İyileştirme',
    color: '#8b5cf6',
    text: 'Ben dun aksam saat sekizde sinemaya gittim cunku cok guzel film vardi vizyonda ama sonra aniden yagmur basladi ve eve gec gitmek mecburiyetinde kalmis oldum.',
  },
  {
    label: 'Türkçe ➔ İngilizce Çeviri',
    description: 'İş ve günlük konuşma çevirisi',
    badge: 'TR ↔ EN',
    color: '#3b82f6',
    text: 'Gelecek hafta düzenlenecek uluslararası yazılım konferansında yapacağımız sunumun taslağını ekibimizle paylaştım.',
  },
  {
    label: 'İngilizce ➔ Türkçe Çeviri',
    description: 'Yabancı dilden Türkçeye otomatik tespit',
    badge: 'EN ↔ TR',
    color: '#06b6d4',
    text: 'Our team is developing modern artificial intelligence tools to empower developers and content creators worldwide.',
  },
  {
    label: 'Türkçe ➔ Fransızca Çeviri',
    description: 'Fransızca iki yönlü çeviri',
    badge: 'TR ↔ FR',
    color: '#ef4444',
    text: 'İstanbul, Asya ile Avrupa kıtalarını birleştiren büyüleyici tarihi ve eşsiz boğaz manzarasıyla tanınır.',
  },
  {
    label: 'Flamanca (NL) ➔ Türkçe',
    description: 'Hollandaca / Flamanca iki yönlü çeviri',
    badge: 'TR ↔ NL',
    color: '#f97316',
    text: 'Goedemorgen, het weer in Amsterdam is vandaag erg aangenaam en zonnig. We gaan samen een wandeling maken.',
  },
  {
    label: 'Sözlük Modu (Örn: "filmi")',
    description: 'Tek bir sözcük seçildiğinde çalışan akıllı sözlük',
    badge: '📖 Sözlük',
    color: '#10b981',
    text: 'Dün akşam arkadaşlarla bu harika filmi izledim ve hikayesinden çok etkilendim.',
  },
];

export const App: React.FC = () => {
  const [text, setText] = useState(PRESETS[0].text);
  const [settings, setSettings] = useState<AppSettings>({
    program_name: 'Hızlı Yazıcı',
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
    improve_provider: 'auto',
    auto_copy_on_action: true,
    hasGeminiKey: false,
  });

  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isAudioModalOpen, setIsAudioModalOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [loadingAction, setLoadingAction] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  // Selection floating menu state
  const [selectionMenuPos, setSelectionMenuPos] = useState<{ top: number; left: number } | null>(null);
  const [selectedRange, setSelectedRange] = useState<{ start: number; end: number } | null>(null);

  // History & Diffs
  const [history, setHistory] = useState<HistoryItem[]>([]);
  const [showHistory, setShowHistory] = useState(false);
  const [lastDiff, setLastDiff] = useState<{ original: string; modified: string; title: string } | null>(null);

  // Translation result floating window modal
  const [translationModal, setTranslationModal] = useState<TranslationModalState>({
    isOpen: false,
    originalText: '',
    translatedText: '',
    targetLang: 'nl',
    provider: '',
  });

  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Load settings on mount
  useEffect(() => {
    fetch('/api/settings')
      .then((res) => res.json())
      .then((data) => {
        if (data) setSettings((prev) => ({ ...prev, ...data }));
      })
      .catch((err) => console.warn('Could not load settings:', err));
  }, []);

  const addToast = (title: string, message: string, color: string) => {
    const newToast: ToastMessage = {
      id: Math.random().toString(36).substring(2, 9),
      title,
      message,
      color,
      timestamp: Date.now(),
    };
    setToasts((prev) => [...prev, newToast]);

    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== newToast.id));
    }, (settings.floating_menu_duration || 8) * 1000);
  };

  const dismissToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  // Keyboard shortcut listener (Ctrl+C double-tap / Ctrl+Shift+Y simulator)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ctrl+Shift+Y for voice
      if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 'y') {
        e.preventDefault();
        if (settings.enable_speech_to_text) {
          setIsAudioModalOpen(true);
        }
      }
      // Ctrl+Shift+D for quick deasciify
      if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 'd') {
        e.preventDefault();
        handleFixCharacters();
      }
      // Ctrl+Shift+I for AI improve
      if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 'i') {
        e.preventDefault();
        handleImproveText();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [settings, text, selectedRange]);

  // Handle text selection inside textarea to position floating menu
  const handleSelectText = () => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;

    if (start !== end && end - start > 1) {
      setSelectedRange({ start, end });
      const rect = textarea.getBoundingClientRect();
      // approximate position near middle of selection
      setSelectionMenuPos({
        top: Math.max(80, rect.top + 20),
        left: rect.left + rect.width / 2,
      });
    } else {
      setSelectedRange(null);
      setSelectionMenuPos(null);
    }
  };

  // 1. Karakter Düzelt (Local Deasciifier - zero network delay)
  const handleFixCharacters = () => {
    if (!settings.enable_character_fix) {
      addToast('Özellik Kapalı', 'Karakter düzeltme özelliği ayarlardan devre dışı bırakılmış.', '#e74c3c');
      return;
    }

    const targetText = selectedRange
      ? text.substring(selectedRange.start, selectedRange.end)
      : text;

    if (!targetText.trim()) {
      addToast('Hata', 'Lütfen metin girin veya bir bölüm seçin.', '#e74c3c');
      return;
    }

    const corrected = Deasciifier.deasciify(targetText);
    const changed = targetText.trim() !== corrected.trim();

    if (changed) {
      let newFullText = corrected;
      if (selectedRange) {
        newFullText =
          text.substring(0, selectedRange.start) +
          corrected +
          text.substring(selectedRange.end);
      }
      setText(newFullText);
      setLastDiff({ original: targetText, modified: corrected, title: 'Karakter Düzeltme' });
      setHistory((prev) => [
        {
          id: Math.random().toString(),
          timestamp: new Date(),
          action: 'deasciify',
          originalText: targetText,
          resultText: corrected,
          changed: true,
        },
        ...prev.slice(0, 20),
      ]);

      if (settings.auto_copy_on_action) {
        navigator.clipboard?.writeText(corrected).catch(() => {});
      }
      addToast(
        'Karakterler Düzeltildi!',
        settings.auto_copy_on_action ? `${corrected} (Panoya Kopyalandı)` : corrected,
        '#2ecc71'
      );
    } else if (settings.notify_on_no_change) {
      addToast('Düzeltme Gerekmedi', 'Metin zaten düzgün görünüyor.', '#3498db');
    }

    setSelectionMenuPos(null);
  };

  // 2. Metni İyileştir (Tamamen Yerel Kural Motoru - Kotasız & Anında)
  const handleImproveText = () => {
    if (!settings.enable_ai_improve) {
      addToast('Özellik Kapalı', 'Metin iyileştirme özelliği ayarlardan kapatılmış.', '#e74c3c');
      return;
    }

    const targetText = selectedRange
      ? text.substring(selectedRange.start, selectedRange.end)
      : text;

    if (!targetText.trim()) {
      addToast('Hata', 'Lütfen metin girin veya bir bölüm seçin.', '#e74c3c');
      return;
    }

    const improved = improveTurkishTextFree(targetText);
    const changed = targetText.trim() !== improved.trim();

    if (changed) {
      let newFullText = improved;
      if (selectedRange) {
        newFullText =
          text.substring(0, selectedRange.start) +
          improved +
          text.substring(selectedRange.end);
      }
      setText(newFullText);
      setLastDiff({ original: targetText, modified: improved, title: 'Metin İyileştirme (Yerel Kural Motoru)' });
      setHistory((prev) => [
        {
          id: Math.random().toString(),
          timestamp: new Date(),
          action: 'improve',
          originalText: targetText,
          resultText: improved,
          changed: true,
          provider: 'Yerel Motor (Ücretsiz)',
        },
        ...prev.slice(0, 20),
      ]);

      if (settings.auto_copy_on_action) {
        navigator.clipboard?.writeText(improved).catch(() => {});
      }

      addToast(
        'Metin İyileştirildi!',
        settings.auto_copy_on_action ? `${improved} (Panoya Kopyalandı)` : improved,
        '#9b59b6'
      );
    } else if (settings.notify_on_no_change) {
      addToast('Düzeltme Gerekmedi', 'Metin zaten düzgün görünüyor.', '#3498db');
    }

    setSelectionMenuPos(null);
  };

  // 3. Çeviri (TR ↔ EN)
  const handleTranslateEn = async () => {
    await handleTranslate('en');
  };

  // 4. Çeviri (TR ↔ FR)
  const handleTranslateFr = async () => {
    await handleTranslate('fr');
  };

  // 5. Çeviri (TR ↔ NL / Flamanca - Hollandaca)
  const handleTranslateNl = async () => {
    await handleTranslate('nl');
  };

  const handleTranslate = async (targetLang: 'en' | 'fr' | 'nl') => {
    const targetText = selectedRange
      ? text.substring(selectedRange.start, selectedRange.end)
      : text;

    if (!targetText.trim()) {
      addToast('Hata', 'Lütfen çevrilecek metni girin veya seçin.', '#e74c3c');
      return;
    }

    const langName =
      targetLang === 'nl'
        ? 'Flamanca'
        : targetLang === 'fr'
        ? 'Fransızca'
        : 'İngilizce';

    setIsLoading(true);
    setLoadingAction(`translate_${targetLang}`);

    try {
      const res = await fetch('/api/translate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          text: targetText,
          targetLanguage: targetLang,
          provider: settings.translation_provider,
        }),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || 'Çeviri başarısız oldu.');
      }

      const data = await res.json();
      const translated = data.translated;
      const isDict = Boolean(data.isDictionary);
      const dictData = data.dictionary || null;

      // Update history
      const actionType: 'translate_en' | 'translate_fr' | 'translate_nl' =
        targetLang === 'nl'
          ? 'translate_nl'
          : targetLang === 'fr'
          ? 'translate_fr'
          : 'translate_en';

      setHistory((prev) => [
        {
          id: Math.random().toString(),
          timestamp: new Date(),
          action: actionType,
          originalText: targetText,
          resultText: isDict && dictData?.rootWord ? `${dictData.rootWord}: ${translated}` : translated,
          changed: true,
          provider: data.provider,
          isDictionary: isDict,
        },
        ...prev.slice(0, 20),
      ]);

      if (settings.auto_copy_on_action) {
        navigator.clipboard?.writeText(translated).catch(() => {});
      }

      // Open centered adaptive floating window with the translated text / dictionary analysis
      // Replacing the tiny bottom-right toast per user request
      setTranslationModal({
        isOpen: true,
        originalText: targetText,
        translatedText: translated,
        targetLang,
        provider: data.provider,
        isDictionary: isDict,
        dictionaryData: dictData,
      });

    } catch (err: any) {
      addToast('Çeviri Hatası', err.message || 'Çeviri gerçekleştirilemedi.', '#e74c3c');
    } finally {
      setIsLoading(false);
      setLoadingAction(null);
      setSelectionMenuPos(null);
    }
  };

  // Metin editörüne çeviriyi aktarma
  const handleApplyTranslationToEditor = (translatedText: string) => {
    let newFullText = translatedText;
    if (selectedRange) {
      newFullText =
        text.substring(0, selectedRange.start) +
        translatedText +
        text.substring(selectedRange.end);
    }
    setText(newFullText);
    setLastDiff({
      original: translationModal.originalText,
      modified: translatedText,
      title: `Çeviri (${translationModal.targetLang.toUpperCase()})`,
    });
    addToast('Editöre Aktarıldı', 'Çevrilen metin yazı alanına aktarıldı.', settings.color_theme);
  };

  // 5. Sesle Yazma Tamamlandığında
  const handleTranscriptionComplete = (transcribed: string) => {
    if (!transcribed.trim()) return;

    let newFullText = text ? `${text} ${transcribed}` : transcribed;
    if (selectedRange) {
      newFullText =
        text.substring(0, selectedRange.start) +
        transcribed +
        text.substring(selectedRange.end);
    }
    setText(newFullText);
    navigator.clipboard?.writeText(transcribed).catch(() => {});
    addToast('Ses Metne Çevrildi!', transcribed, '#2ecc71');
    setHistory((prev) => [
      {
        id: Math.random().toString(),
        timestamp: new Date(),
        action: 'stt',
        originalText: '(Ses Kaydı)',
        resultText: transcribed,
        changed: true,
      },
      ...prev.slice(0, 20),
    ]);
  };

  // 6. Panoya Kopyalama
  const handleCopy = async () => {
    const textToCopy = selectedRange
      ? text.substring(selectedRange.start, selectedRange.end)
      : text;

    if (!textToCopy) return;

    try {
      await navigator.clipboard.writeText(textToCopy);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
      addToast('Panoya Kopyalandı!', textToCopy, settings.color_theme);
    } catch {
      addToast('Kopyalama Başarısız', 'Pano erişimi engellendi.', '#e74c3c');
    }
  };

  // Settings Save
  const handleSaveSettings = async (newSettings: AppSettings) => {
    setSettings(newSettings);
    try {
      await fetch('/api/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newSettings),
      });
      addToast('Ayarlar Kaydedildi', 'Uygulama tercihleri güncellendi.', newSettings.color_theme);
    } catch {
      addToast('Hata', 'Ayarlar sunucuya kaydedilemedi.', '#e74c3c');
    }
  };

  const wordCount = text.trim() ? text.trim().split(/\s+/).length : 0;
  const charCount = text.length;

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans">
      {/* Top Application Bar */}
      <header className="border-b border-slate-800/80 bg-slate-900/60 backdrop-blur-md sticky top-0 z-30 px-4 lg:px-8 py-3.5">
        <div className="max-w-7xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div
              className="w-10 h-10 rounded-xl flex items-center justify-center shadow-lg transition-transform hover:scale-105"
              style={{
                backgroundColor: settings.color_theme,
                boxShadow: `0 0 20px ${settings.color_theme}40`,
              }}
            >
              <Zap className="w-5 h-5 text-white" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-lg font-bold tracking-tight text-white">
                  {settings.program_name}
                </h1>
                <span className="text-[10px] uppercase font-semibold px-2 py-0.5 rounded-full bg-emerald-500/15 text-emerald-400 border border-emerald-500/30">
                  Tepside Aktif
                </span>
              </div>
              <p className="text-xs text-slate-400">
                Akıllı Türkçe karakter düzeltme, AI metin iyileştirme ve çeviri
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 sm:gap-3">
            <button
              onClick={() => setShowHistory(!showHistory)}
              className={`p-2 rounded-xl text-xs font-medium border flex items-center gap-1.5 transition-colors ${
                showHistory
                  ? 'bg-slate-800 border-indigo-500/50 text-indigo-300'
                  : 'bg-slate-900 border-slate-800 text-slate-300 hover:bg-slate-800 hover:text-white'
              }`}
              title="İşlem Geçmişi"
            >
              <History className="w-4 h-4" />
              <span className="hidden sm:inline">Geçmiş ({history.length})</span>
            </button>

            <button
              onClick={() => setIsSettingsOpen(true)}
              className="p-2 rounded-xl text-xs font-medium bg-slate-900 border border-slate-800 text-slate-300 hover:bg-slate-800 hover:text-white flex items-center gap-1.5 transition-colors"
              title="Ayarlar"
            >
              <Settings className="w-4 h-4" />
              <span className="hidden sm:inline">Ayarlar</span>
            </button>
          </div>
        </div>
      </header>

      {/* Main Container */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-4 lg:p-8 grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Column: Editor & Controls (8 cols on large) */}
        <div className="lg:col-span-8 flex flex-col gap-6">
          {/* Preset Buttons */}
          <div className="space-y-2">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span className="font-semibold uppercase tracking-wider text-[11px] text-slate-400">
                Hızlı Örnekler ile Deneyin:
              </span>
              <span className="text-[11px] text-slate-500 hidden sm:inline">
                Tıklayarak metni editöre yükleyin
              </span>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2">
              {PRESETS.map((preset, idx) => (
                <button
                  key={idx}
                  onClick={() => {
                    setText(preset.text);
                    setSelectedRange(null);
                    setSelectionMenuPos(null);
                    setLastDiff(null);
                  }}
                  className="p-2.5 rounded-xl bg-slate-900/80 hover:bg-slate-800/90 border border-slate-800 text-left transition-all hover:border-slate-700 group flex flex-col justify-between"
                >
                  <div className="flex items-center justify-between gap-1 mb-1">
                    <span className="text-xs font-medium text-slate-200 group-hover:text-white line-clamp-1">
                      {preset.label}
                    </span>
                    <span
                      className="text-[9px] font-bold px-1.5 py-0.5 rounded shrink-0"
                      style={{
                        backgroundColor: `${preset.color}20`,
                        color: preset.color,
                      }}
                    >
                      {preset.badge}
                    </span>
                  </div>
                  <span className="text-[10px] text-slate-500 group-hover:text-slate-400 line-clamp-1">
                    {preset.description}
                  </span>
                </button>
              ))}
            </div>
          </div>

          {/* Text Editor Box */}
          <div className="relative flex flex-col rounded-2xl bg-slate-900/90 border border-slate-800/90 shadow-2xl overflow-hidden focus-within:border-indigo-500/50 transition-colors">
            {/* Editor Top Bar */}
            <div className="px-4 py-2.5 bg-slate-950/60 border-b border-slate-800 flex items-center justify-between text-xs text-slate-400">
              <div className="flex items-center gap-3">
                <span className="font-medium text-slate-300">Yazı Editörü & Pano Alanı</span>
                {selectedRange && (
                  <span className="px-2 py-0.5 rounded-md bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 text-[11px]">
                    Seçili Bölüm ({selectedRange.end - selectedRange.start} karakter)
                  </span>
                )}
              </div>
              <div className="flex items-center gap-2">
                {/* Quick Provider Mode Selector */}
                <button
                  type="button"
                  onClick={() => {
                    const nextMode: 'auto' | 'free' | 'gemini' =
                      settings.improve_provider === 'auto'
                        ? 'free'
                        : settings.improve_provider === 'free'
                        ? 'gemini'
                        : 'auto';
                    const updated: AppSettings = { ...settings, improve_provider: nextMode };
                    setSettings(updated);
                    handleSaveSettings(updated);
                  }}
                  title="İyileştirme Motorunu Değiştir (Tıkla)"
                  className={`px-2 py-1 rounded-lg text-[11px] font-medium border flex items-center gap-1.5 transition-all ${
                    settings.improve_provider === 'free'
                      ? 'bg-emerald-950/60 border-emerald-500/40 text-emerald-300 hover:bg-emerald-900/60'
                      : settings.improve_provider === 'gemini'
                      ? 'bg-purple-950/60 border-purple-500/40 text-purple-300 hover:bg-purple-900/60'
                      : 'bg-indigo-950/60 border-indigo-500/40 text-indigo-300 hover:bg-indigo-900/60'
                  }`}
                >
                  <Sparkles className="w-3 h-3" />
                  <span>
                    {settings.improve_provider === 'free'
                      ? 'Motor: Ücretsiz'
                      : settings.improve_provider === 'gemini'
                      ? 'Motor: Gemini AI'
                      : 'Motor: Otomatik (Gemini + Ücretsiz)'}
                  </span>
                </button>

                <button
                  onClick={() => setText('')}
                  title="Metni Temizle"
                  className="p-1 hover:text-rose-400 rounded transition-colors"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            {/* Textarea */}
            <div className="relative p-4">
              <textarea
                ref={textareaRef}
                value={text}
                onChange={(e) => setText(e.target.value)}
                onSelect={handleSelectText}
                placeholder="Metninizi buraya yazın veya yapıştırın. Belirli bir kısmı seçtiğinizde yüzer menü otomatik açılacaktır..."
                rows={10}
                className="w-full bg-transparent resize-y text-slate-100 text-sm md:text-base leading-relaxed placeholder:text-slate-600 focus:outline-none"
              />

              {/* Selection-based Floating Menu */}
              {selectionMenuPos && (
                <FloatingMenu
                  themeColor={settings.color_theme}
                  onFix={handleFixCharacters}
                  onImprove={handleImproveText}
                  onTranslateEn={handleTranslateEn}
                  onTranslateFr={handleTranslateFr}
                  onTranslateNl={handleTranslateNl}
                  onRecordAudio={() => setIsAudioModalOpen(true)}
                  onCopy={handleCopy}
                  position={selectionMenuPos}
                  isFloating={true}
                  isLoading={isLoading}
                  copied={copied}
                  isSingleWord={Boolean(
                    extractSingleWord(
                      selectedRange ? text.substring(selectedRange.start, selectedRange.end) : text
                    )
                  )}
                />
              )}
            </div>

            {/* Editor Bottom Bar: Stats & Fixed Toolbar */}
            <div className="p-3 bg-slate-950/80 border-t border-slate-800 flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-4 text-xs font-mono text-slate-400">
                <span>{charCount} karakter</span>
                <span>•</span>
                <span>{wordCount} kelime</span>
              </div>

              {/* Fixed Desktop App Actions Toolbar */}
              <div className="flex items-center gap-1.5">
                <button
                  onClick={handleFixCharacters}
                  disabled={isLoading}
                  className="px-3 py-1.5 rounded-xl bg-emerald-600/20 hover:bg-emerald-600/30 border border-emerald-500/40 text-emerald-300 text-xs font-semibold flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
                  title="Türkçe Karakter Düzelt (ç, ğ, ı, ö, ş, ü)"
                >
                  <Pencil className="w-3.5 h-3.5" />
                  <span>Karakter Düzelt</span>
                </button>

                <button
                  onClick={handleImproveText}
                  disabled={isLoading}
                  className="px-3 py-1.5 rounded-xl bg-amber-500/20 hover:bg-amber-500/30 border border-amber-500/40 text-amber-300 text-xs font-semibold flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
                  title="Yapay Zeka ile Metni İyileştir"
                >
                  <Sparkles className="w-3.5 h-3.5" />
                  <span>İyileştir</span>
                </button>

                <button
                  onClick={handleTranslateEn}
                  disabled={isLoading}
                  className="px-3 py-1.5 rounded-xl bg-blue-500/20 hover:bg-blue-500/30 border border-blue-500/40 text-blue-300 text-xs font-semibold flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
                  title="İngilizceye / Türkçeye Çevir"
                >
                  <Globe className="w-3.5 h-3.5" />
                  <span>Çevir (EN)</span>
                </button>

                <button
                  onClick={handleTranslateFr}
                  disabled={isLoading}
                  className="px-3 py-1.5 rounded-xl bg-rose-500/20 hover:bg-rose-500/30 border border-rose-500/40 text-rose-300 text-xs font-semibold flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
                  title="Fransızcaya / Türkçeye Çevir"
                >
                  <Globe className="w-3.5 h-3.5" />
                  <span>Çevir (FR)</span>
                </button>

                <button
                  onClick={handleTranslateNl}
                  disabled={isLoading}
                  className="px-3 py-1.5 rounded-xl bg-orange-500/20 hover:bg-orange-500/30 border border-orange-500/40 text-orange-300 text-xs font-semibold flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
                  title="Flamancaya / Türkçeye Çevir (Hollandaca)"
                >
                  <Globe className="w-3.5 h-3.5" />
                  <span>Çevir (NL)</span>
                </button>

                <button
                  onClick={() => setIsAudioModalOpen(true)}
                  disabled={isLoading}
                  className="px-3 py-1.5 rounded-xl bg-purple-500/20 hover:bg-purple-500/30 border border-purple-500/40 text-purple-300 text-xs font-semibold flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
                  title="Sesle Yazma"
                >
                  <Mic className="w-3.5 h-3.5" />
                  <span className="hidden sm:inline">Sesle Yaz</span>
                </button>

                <button
                  onClick={handleCopy}
                  className="p-1.5 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white border border-slate-700 transition-all active:scale-95"
                  title="Panoya Kopyala"
                >
                  {copied ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>
            </div>
          </div>

          {/* Diff Viewer when an operation completed */}
          {lastDiff && (
            <DiffViewer
              original={lastDiff.original}
              modified={lastDiff.modified}
              actionTitle={lastDiff.title}
            />
          )}

          {/* Desktop App Experience Simulation Banner */}
          <div className="p-4 rounded-2xl bg-gradient-to-r from-slate-900 to-indigo-950/40 border border-indigo-900/40 flex items-start gap-3.5 shadow-lg">
            <div
              className="p-2.5 rounded-xl shrink-0 mt-0.5"
              style={{ backgroundColor: `${settings.color_theme}25`, color: settings.color_theme }}
            >
              <Zap className="w-5 h-5" />
            </div>
            <div className="space-y-1 text-xs">
              <h4 className="font-semibold text-white flex items-center gap-2">
                Masaüstü & Sistem Tepsisi Entegrasyonu
                <span className="px-1.5 py-0.5 text-[10px] font-normal bg-indigo-500/20 text-indigo-300 rounded border border-indigo-500/30">
                  {settings.hotkey} &bull; {settings.stt_hotkey}
                </span>
              </h4>
              <p className="text-slate-300 leading-relaxed">
                Bu uygulama orijinal C# WPF masaüstü mimarisinden taşınmıştır. Metin seçildiğinde beliren yüzer menü, yerel Deniz Yüret algoritmalı 13.463 kurallı Deasciifier, iki yönlü Google/Gemini çeviri ve ses transkripsiyon servisleri tarayıcı ortamında tam kapasite çalışır.
              </p>
            </div>
          </div>
        </div>

        {/* Right Column: Features Guide & History (4 cols on large) */}
        <div className="lg:col-span-4 flex flex-col gap-6">
          {/* Features Highlights Card */}
          <div className="p-5 rounded-2xl bg-slate-900/80 border border-slate-800/90 shadow-xl space-y-4">
            <h3 className="text-sm font-bold text-white flex items-center gap-2">
              <Layers className="w-4 h-4 text-indigo-400" />
              Temel Fonksiyonlar
            </h3>

            <div className="space-y-3 text-xs">
              <div className="p-3 rounded-xl bg-slate-800/50 border border-slate-700/50 space-y-1">
                <div className="flex items-center gap-2 font-semibold text-emerald-400">
                  <Pencil className="w-3.5 h-3.5" />
                  <span>Karakter Düzeltme (Yerel)</span>
                </div>
                <p className="text-slate-300">
                  İnternet veya API gerektirmeden eksik Türkçe harfleri (<code className="text-emerald-300">ç, ğ, ı, ö, ş, ü</code>) anında tamamlar.
                </p>
              </div>

              <div className="p-3 rounded-xl bg-slate-800/50 border border-slate-700/50 space-y-1">
                <div className="flex items-center gap-2 font-semibold text-amber-400">
                  <Sparkles className="w-3.5 h-3.5" />
                  <span>AI Metin İyileştirme</span>
                </div>
                <p className="text-slate-300">
                  Gemini API kullanarak metnin dilini kesinlikle değiştirmeden dilbilgisi, yazım, noktalama ve akıcılığı mükemmelleştirir.
                </p>
              </div>

              <div className="p-3 rounded-xl bg-slate-800/50 border border-slate-700/50 space-y-1">
                <div className="flex items-center gap-2 font-semibold text-blue-400">
                  <ArrowRightLeft className="w-3.5 h-3.5" />
                  <span>Akıllı İki Yönlü Çeviri</span>
                </div>
                <p className="text-slate-300">
                  Metin Türkçe ise İngilizceye/Fransızcaya, yabancı dilde ise otomatik olarak Türkçeye çevrilir. (Google Keyless veya Gemini).
                </p>
              </div>

              <div className="p-3 rounded-xl bg-slate-800/50 border border-slate-700/50 space-y-1">
                <div className="flex items-center gap-2 font-semibold text-purple-400">
                  <Volume2 className="w-3.5 h-3.5" />
                  <span>Sesle Yazma (Mikrofon)</span>
                </div>
                <p className="text-slate-300">
                  {settings.stt_duration} saniyelik mikrofon kaydını Gemini yapay zekasıyla dinleyip tam metne dönüştürür.
                </p>
              </div>
            </div>
          </div>

          {/* History Panel */}
          <div className="p-5 rounded-2xl bg-slate-900/80 border border-slate-800/90 shadow-xl flex-1 flex flex-col space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <History className="w-4 h-4 text-slate-400" />
                Son İşlemler
              </h3>
              {history.length > 0 && (
                <button
                  onClick={() => setHistory([])}
                  className="text-[11px] text-slate-400 hover:text-rose-400 transition-colors"
                >
                  Geçmişi Temizle
                </button>
              )}
            </div>

            {history.length === 0 ? (
              <div className="flex-1 flex flex-col items-center justify-center p-6 text-center text-slate-500 text-xs">
                <History className="w-8 h-8 mb-2 opacity-30" />
                <span>Henüz bir işlem yapılmadı. Yukarıdaki butonları kullanarak ilk dönüşümünüzü gerçekleştirin.</span>
              </div>
            ) : (
              <div className="space-y-2.5 overflow-y-auto max-h-80 pr-1">
                {history.map((item) => (
                  <div
                    key={item.id}
                    className="p-3 rounded-xl bg-slate-800/40 border border-slate-700/40 text-xs space-y-1.5 hover:bg-slate-800/70 transition-colors"
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-semibold text-white capitalize">
                        {item.action === 'deasciify'
                          ? 'Karakter Düzeltme'
                          : item.action === 'improve'
                          ? 'AI İyileştirme'
                          : item.action === 'translate_en'
                          ? 'Çeviri (EN)'
                          : item.action === 'translate_fr'
                          ? 'Çeviri (FR)'
                          : item.action === 'translate_nl'
                          ? 'Çeviri (NL)'
                          : 'Sesle Yazma'}
                      </span>
                      <span className="text-[10px] text-slate-500 font-mono">
                        {item.timestamp.toLocaleTimeString()}
                      </span>
                    </div>
                    <p className="text-slate-300 line-clamp-2 font-mono text-[11px]">
                      {item.resultText}
                    </p>
                    <div className="flex items-center justify-end gap-2 pt-1 border-t border-slate-700/30">
                      <button
                        onClick={() => {
                          setText(item.resultText);
                          addToast('Yüklendi', 'Metin editöre aktarıldı.', settings.color_theme);
                        }}
                        className="text-[10px] text-indigo-400 hover:text-indigo-300 font-medium"
                      >
                        Editöre Yükle
                      </button>
                      <button
                        onClick={() => {
                          navigator.clipboard.writeText(item.resultText);
                          addToast('Kopyalandı', item.resultText, settings.color_theme);
                        }}
                        className="text-[10px] text-slate-400 hover:text-white"
                      >
                        Kopyala
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </main>

      {/* Centered Floating Translation Result Window */}
      <TranslationResultModal
        data={translationModal}
        themeColor={settings.color_theme}
        onClose={() => setTranslationModal((prev) => ({ ...prev, isOpen: false }))}
        onApplyToEditor={handleApplyTranslationToEditor}
      />

      {/* Floating Notifications mimicking Windows WPF NotificationWindow */}
      <NotificationToast toasts={toasts} onDismiss={dismissToast} />

      {/* Settings Modal */}
      <SettingsModal
        settings={settings}
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        onSave={handleSaveSettings}
      />

      {/* Voice to Text Recording Modal */}
      <AudioRecorderModal
        isOpen={isAudioModalOpen}
        durationSeconds={settings.stt_duration}
        themeColor={settings.color_theme}
        onClose={() => setIsAudioModalOpen(false)}
        onTranscriptionComplete={handleTranscriptionComplete}
        onError={(errMsg) => addToast('Ses Hatası', errMsg, '#e74c3c')}
      />
    </div>
  );
};
