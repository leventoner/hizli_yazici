import React, { useState, useEffect, useRef } from 'react';
import { BookOpen, Search, ArrowRightLeft, Copy, Check, X, Loader2 } from 'lucide-react';
import { AppSettings } from '../types';
import { translateDirectFree } from '../services/freeTranslation';

interface DictionaryModalProps {
  isOpen: boolean;
  onClose: () => void;
  settings: AppSettings;
  onUpdateSettings: (newSettings: AppSettings) => void;
  initialQuery?: string;
}

const LANGUAGES = [
  { code: 'tr', label: '🇹🇷 Türkçe (TR)' },
  { code: 'en', label: '🇬🇧 İngilizce (EN)' },
  { code: 'fr', label: '🇫🇷 Fransızca (FR)' },
  { code: 'nl', label: '🇳🇱 Hollandaca / Flamanca (NL)' },
];

export const DictionaryModal: React.FC<DictionaryModalProps> = ({
  isOpen,
  onClose,
  settings,
  onUpdateSettings,
  initialQuery = '',
}) => {
  const [sourceLang, setSourceLang] = useState<string>(settings.dictionary_source_lang || 'tr');
  const [targetLang, setTargetLang] = useState<string>(settings.dictionary_target_lang || 'en');
  const [query, setQuery] = useState<string>(initialQuery);
  const [result, setResult] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [copied, setCopied] = useState<boolean>(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Sync saved languages from settings
  useEffect(() => {
    if (settings.dictionary_source_lang) setSourceLang(settings.dictionary_source_lang);
    if (settings.dictionary_target_lang) setTargetLang(settings.dictionary_target_lang);
  }, [settings.dictionary_source_lang, settings.dictionary_target_lang]);

  // Autofocus input when modal opens
  useEffect(() => {
    if (isOpen) {
      setTimeout(() => inputRef.current?.focus(), 80);
      if (initialQuery && initialQuery.trim()) {
        setQuery(initialQuery);
        handleSearch(initialQuery);
      }
    }
  }, [isOpen]);

  // Handle ESC key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  const handleSourceLangChange = (newSource: string) => {
    setSourceLang(newSource);
    const updated = { ...settings, dictionary_source_lang: newSource };
    onUpdateSettings(updated);
  };

  const handleTargetLangChange = (newTarget: string) => {
    setTargetLang(newTarget);
    const updated = { ...settings, dictionary_target_lang: newTarget };
    onUpdateSettings(updated);
  };

  const handleSwap = () => {
    const nextSource = targetLang;
    const nextTarget = sourceLang;
    setSourceLang(nextSource);
    setTargetLang(nextTarget);

    const updated = {
      ...settings,
      dictionary_source_lang: nextSource,
      dictionary_target_lang: nextTarget,
    };
    onUpdateSettings(updated);

    if (query.trim()) {
      handleSearch(query, nextSource, nextTarget);
    }
  };

  const handleSearch = async (
    searchWord = query,
    src = sourceLang,
    tgt = targetLang
  ) => {
    const trimmed = searchWord.trim();
    if (!trimmed) {
      setResult('Lütfen aramak istediğiniz bir kelime veya cümle yazın.');
      return;
    }

    if (src === tgt) {
      setResult('Kaynak ve hedef diller aynı seçildi. Lütfen farklı diller seçin.');
      return;
    }

    setIsLoading(true);
    try {
      const resp = await fetch('/api/translate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          text: trimmed,
          sourceLanguage: src,
          targetLanguage: tgt,
        }),
      });

      if (resp.ok) {
        const data = await resp.json();
        setResult(data.translated || 'Sonuç bulunamadı.');
      } else {
        const fallback = await translateDirectFree(trimmed, src, tgt, settings.translation_email);
        setResult(fallback || 'Sonuç bulunamadı.');
      }
    } catch {
      try {
        const fallback = await translateDirectFree(trimmed, src, tgt, settings.translation_email);
        setResult(fallback || 'Sonuç bulunamadı.');
      } catch (err: any) {
        setResult(`Arama sırasında hata oluştu: ${err.message || 'Bilinmeyen hata'}`);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopy = () => {
    if (!result.trim()) return;
    navigator.clipboard?.writeText(result).catch(() => {});
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  if (!isOpen) return null;

  const charCount = result.length;
  const wordCount = result.trim() ? result.trim().split(/\s+/).length : 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-in fade-in duration-150">
      <div
        className="w-full max-w-xl bg-slate-900 border border-slate-700/80 rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]"
        style={{
          boxShadow: `0 0 35px ${settings.color_theme || '#3b82f6'}22`,
        }}
      >
        {/* Header */}
        <div className="px-5 py-3.5 bg-slate-800/80 border-b border-slate-700/60 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <span className="text-xl">📖</span>
            <h3 className="font-bold text-slate-100 text-base">Sözlük &amp; Çeviri</h3>
            <span className="text-[11px] font-semibold bg-blue-600/30 text-blue-300 border border-blue-500/30 rounded-md px-2 py-0.5">
              4 Dil Destekli
            </span>
          </div>

          <button
            onClick={onClose}
            className="p-1 rounded-lg text-slate-400 hover:text-slate-100 hover:bg-slate-700/60 transition-colors"
            title="Kapat (ESC)"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Language Selection Row */}
        <div className="px-5 py-3 bg-slate-950/60 border-b border-slate-800 flex items-center justify-between gap-3">
          {/* Source Lang */}
          <div className="flex-1">
            <label className="block text-[11px] font-medium text-slate-400 mb-1">Kaynak Dil</label>
            <select
              value={sourceLang}
              onChange={(e) => handleSourceLangChange(e.target.value)}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-blue-500 cursor-pointer"
            >
              {LANGUAGES.map((l) => (
                <option key={l.code} value={l.code}>
                  {l.label}
                </option>
              ))}
            </select>
          </div>

          {/* Swap Button */}
          <button
            type="button"
            onClick={handleSwap}
            title="Dilleri Değiştir (⇄)"
            className="mt-4 p-2 rounded-lg bg-slate-800 hover:bg-slate-700 border border-slate-700 text-sky-400 hover:text-sky-300 transition-all active:scale-95 flex items-center justify-center"
          >
            <ArrowRightLeft className="w-4 h-4" />
          </button>

          {/* Target Lang */}
          <div className="flex-1">
            <label className="block text-[11px] font-medium text-slate-400 mb-1">Hedef Dil</label>
            <select
              value={targetLang}
              onChange={(e) => handleTargetLangChange(e.target.value)}
              className="w-full bg-slate-800 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-100 focus:outline-none focus:border-blue-500 cursor-pointer"
            >
              {LANGUAGES.map((l) => (
                <option key={l.code} value={l.code}>
                  {l.label}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Search Row */}
        <div className="p-5 pb-3">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              handleSearch();
            }}
            className="flex items-center gap-2"
          >
            <div className="relative flex-1">
              <input
                ref={inputRef}
                type="text"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Sözlükte aranacak kelime veya terimi yazın..."
                className="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
              />
              {query && (
                <button
                  type="button"
                  onClick={() => setQuery('')}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-200"
                >
                  <X className="w-4 h-4" />
                </button>
              )}
            </div>

            <button
              type="submit"
              disabled={isLoading || !query.trim()}
              className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-500 text-white font-medium text-sm flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-blue-600/30"
            >
              {isLoading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Search className="w-4 h-4" />
              )}
              <span>Bul</span>
            </button>
          </form>
        </div>

        {/* Result Area */}
        <div className="px-5 pb-4 flex-1 overflow-y-auto">
          <div className="min-h-[140px] max-h-[300px] p-4 bg-slate-950/70 border border-slate-800 rounded-xl overflow-y-auto">
            {isLoading ? (
              <div className="flex flex-col items-center justify-center h-32 gap-2 text-sky-400">
                <Loader2 className="w-6 h-6 animate-spin" />
                <span className="text-xs">Sözlükte aranıyor...</span>
              </div>
            ) : result ? (
              <div className="text-slate-100 text-sm md:text-base leading-relaxed whitespace-pre-wrap select-text">
                {result}
              </div>
            ) : (
              <div className="text-slate-500 text-xs italic flex items-center justify-center h-32">
                Kelimeyi yazıp 'Bul' butonuna veya Enter tuşuna basın.
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-5 py-3 bg-slate-950/80 border-t border-slate-800 flex items-center justify-between text-xs text-slate-400">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleCopy}
              disabled={!result || isLoading}
              className="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 border border-slate-700 text-slate-200 hover:text-white flex items-center gap-1.5 transition-all active:scale-95 disabled:opacity-40"
            >
              {copied ? (
                <>
                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                  <span className="text-emerald-400 font-semibold">Kopyalandı!</span>
                </>
              ) : (
                <>
                  <Copy className="w-3.5 h-3.5" />
                  <span>Kopyala</span>
                </>
              )}
            </button>

            {result && !isLoading && (
              <span className="font-mono text-[11px] text-slate-500">
                {charCount} karakter • {wordCount} kelime
              </span>
            )}
          </div>

          <span className="text-[11px] text-slate-500">ESC: Kapat | Enter: Ara</span>
        </div>
      </div>
    </div>
  );
};
