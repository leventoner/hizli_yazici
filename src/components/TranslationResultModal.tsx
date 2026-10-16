import React, { useState, useEffect, useRef } from 'react';
import { TranslationModalState } from '../types';
import {
  Globe,
  Copy,
  Check,
  X,
  ArrowRightLeft,
  Volume2,
  VolumeX,
  FileText,
  CornerDownLeft,
  Sparkles
} from 'lucide-react';

interface TranslationResultModalProps {
  data: TranslationModalState;
  themeColor: string;
  onClose: () => void;
  onApplyToEditor: (text: string) => void;
}

export const TranslationResultModal: React.FC<TranslationResultModalProps> = ({
  data,
  themeColor,
  onClose,
  onApplyToEditor,
}) => {
  const [copied, setCopied] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [applied, setApplied] = useState(false);
  const textContainerRef = useRef<HTMLDivElement>(null);

  const { isOpen, originalText, translatedText, targetLang, provider } = data;

  // Language info helpers
  const getLangDetails = () => {
    switch (targetLang) {
      case 'nl':
        return {
          name: 'Flamanca (Hollandaca)',
          code: 'NL',
          speechLang: 'nl-NL',
          bgBadge: 'bg-orange-500/20 text-orange-300 border-orange-500/40',
          dotColor: 'bg-orange-400',
        };
      case 'fr':
        return {
          name: 'Fransızca',
          code: 'FR',
          speechLang: 'fr-FR',
          bgBadge: 'bg-rose-500/20 text-rose-300 border-rose-500/40',
          dotColor: 'bg-rose-400',
        };
      case 'en':
      default:
        return {
          name: 'İngilizce',
          code: 'EN',
          speechLang: 'en-US',
          bgBadge: 'bg-blue-500/20 text-blue-300 border-blue-500/40',
          dotColor: 'bg-blue-400',
        };
    }
  };

  const langInfo = getLangDetails();

  // Escape key listener to close
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  // Stop TTS if modal closes
  useEffect(() => {
    return () => {
      if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
      }
    };
  }, [isOpen]);

  if (!isOpen) return null;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(translatedText);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Kopyalama hatası:', err);
    }
  };

  const handleSelectAll = () => {
    if (!textContainerRef.current) return;
    const range = document.createRange();
    range.selectNodeContents(textContainerRef.current);
    const selection = window.getSelection();
    if (selection) {
      selection.removeAllRanges();
      selection.addRange(range);
    }
  };

  const handleSpeak = () => {
    if (!('speechSynthesis' in window)) return;

    if (isSpeaking) {
      window.speechSynthesis.cancel();
      setIsSpeaking(false);
      return;
    }

    const utterance = new SpeechSynthesisUtterance(translatedText);
    utterance.lang = langInfo.speechLang;
    utterance.rate = 0.95;

    utterance.onend = () => setIsSpeaking(false);
    utterance.onerror = () => setIsSpeaking(false);

    setIsSpeaking(true);
    window.speechSynthesis.speak(utterance);
  };

  const handleApply = () => {
    onApplyToEditor(translatedText);
    setApplied(true);
    setTimeout(() => {
      setApplied(false);
      onClose();
    }, 600);
  };

  // Determine width adaptation according to text length
  const charLength = translatedText.length;
  const containerMaxWidth =
    charLength < 80
      ? 'max-w-md'
      : charLength < 250
      ? 'max-w-lg'
      : charLength < 600
      ? 'max-w-xl'
      : 'max-w-2xl';

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-black/70 backdrop-blur-md animate-in fade-in duration-200"
      onClick={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
    >
      <div
        className={`relative w-full ${containerMaxWidth} max-h-[88vh] flex flex-col rounded-3xl bg-slate-900/95 border border-slate-700/80 shadow-2xl shadow-black/80 overflow-hidden transition-all duration-300 scale-100 animate-in zoom-in-95`}
        style={{
          boxShadow: `0 20px 50px -10px rgba(0, 0, 0, 0.7), 0 0 25px ${themeColor}22`,
          borderColor: `${themeColor}44`,
        }}
      >
        {/* Top Header Bar */}
        <div className="px-5 py-4 bg-slate-950/70 border-b border-slate-800 flex items-center justify-between gap-3 shrink-0">
          <div className="flex items-center gap-2.5">
            <div
              className="w-9 h-9 rounded-xl flex items-center justify-center shadow-md"
              style={{
                backgroundColor: `${themeColor}25`,
                color: themeColor,
              }}
            >
              <Globe className="w-5 h-5" />
            </div>

            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-sm font-bold text-white tracking-tight">
                  Çeviri Sonucu
                </h3>
                <span
                  className={`text-[10px] font-bold px-2 py-0.5 rounded-full border uppercase tracking-wider flex items-center gap-1.5 ${langInfo.bgBadge}`}
                >
                  <span className={`w-1.5 h-1.5 rounded-full ${langInfo.dotColor}`} />
                  {langInfo.name} ({langInfo.code})
                </span>
              </div>
              <p className="text-[11px] text-slate-400 flex items-center gap-1.5 mt-0.5">
                <ArrowRightLeft className="w-3 h-3 text-slate-500" />
                <span>Türkçe ↔ {langInfo.name} Çevirisi</span>
                {provider && (
                  <>
                    <span>•</span>
                    <span className="text-slate-400 font-mono text-[10px]">
                      {provider === 'gemini' ? 'Gemini AI' : 'Ücretsiz Çeviri Servisi'}
                    </span>
                  </>
                )}
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded-xl transition-colors"
            title="Kapat (Esc)"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Scrollable Body */}
        <div className="p-5 sm:p-6 overflow-y-auto space-y-4 flex-1">
          {/* Main Translated Text Box - Selectable & Readable */}
          <div className="space-y-2">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span className="font-semibold text-slate-300 flex items-center gap-1.5">
                <Sparkles className="w-3.5 h-3.5 text-indigo-400" />
                Çevrilmiş Metin (Seçilebilir & Kopyalanabilir):
              </span>
              <button
                type="button"
                onClick={handleSelectAll}
                className="text-[11px] text-indigo-400 hover:text-indigo-300 transition-colors"
              >
                Tümünü Seç
              </button>
            </div>

            <div
              ref={textContainerRef}
              tabIndex={0}
              className="p-4 sm:p-5 rounded-2xl bg-slate-950/80 border border-slate-700/80 hover:border-slate-600/80 focus:border-indigo-500/60 focus:outline-none transition-all selection:bg-indigo-600 selection:text-white"
            >
              <p className="text-slate-100 text-base sm:text-lg leading-relaxed select-text cursor-text font-medium whitespace-pre-wrap break-words">
                {translatedText}
              </p>
            </div>
          </div>

          {/* Original Source Text (Context Reference) */}
          {originalText && (
            <div className="p-3.5 rounded-xl bg-slate-950/40 border border-slate-800/80 space-y-1">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-400 flex items-center gap-1">
                <FileText className="w-3 h-3 text-slate-400" />
                Kaynak Metin:
              </span>
              <p className="text-xs text-slate-300 leading-normal select-text line-clamp-3">
                {originalText}
              </p>
            </div>
          )}
        </div>

        {/* Bottom Actions Toolbar */}
        <div className="px-5 py-4 bg-slate-950/80 border-t border-slate-800 flex flex-wrap items-center justify-between gap-2.5 shrink-0">
          {/* Left Actions (TTS Audio Pronunciation) */}
          <div className="flex items-center gap-2">
            {'speechSynthesis' in window && (
              <button
                type="button"
                onClick={handleSpeak}
                className={`px-3 py-1.5 rounded-xl border text-xs font-medium flex items-center gap-1.5 transition-all ${
                  isSpeaking
                    ? 'bg-purple-600/30 border-purple-500/50 text-purple-300'
                    : 'bg-slate-900 border-slate-800 text-slate-300 hover:bg-slate-800 hover:text-white'
                }`}
                title="Sesli Dinle (Telaffuz)"
              >
                {isSpeaking ? (
                  <>
                    <VolumeX className="w-3.5 h-3.5 text-purple-400" />
                    <span>Durdur</span>
                  </>
                ) : (
                  <>
                    <Volume2 className="w-3.5 h-3.5 text-purple-400" />
                    <span>Sesli Dinle</span>
                  </>
                )}
              </button>
            )}
          </div>

          {/* Right Actions: Copy, Apply to Editor, Close */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleCopy}
              className={`px-4 py-2 rounded-xl text-xs font-semibold flex items-center gap-2 transition-all active:scale-95 shadow-md ${
                copied
                  ? 'bg-emerald-600 text-white shadow-emerald-900/40'
                  : 'bg-indigo-600 hover:bg-indigo-500 text-white shadow-indigo-900/40'
              }`}
              title="Çevrilen Metni Panoya Kopyala"
            >
              {copied ? (
                <>
                  <Check className="w-4 h-4 text-white" />
                  <span>Kopyalandı!</span>
                </>
              ) : (
                <>
                  <Copy className="w-4 h-4 text-white" />
                  <span>Panoya Kopyala</span>
                </>
              )}
            </button>

            <button
              type="button"
              onClick={handleApply}
              className="px-3.5 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 hover:text-white border border-slate-700 text-xs font-medium flex items-center gap-1.5 transition-all active:scale-95"
              title="Çevrilen metni ana yazı editörüne aktar"
            >
              {applied ? (
                <>
                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                  <span>Aktarıldı!</span>
                </>
              ) : (
                <>
                  <CornerDownLeft className="w-3.5 h-3.5 text-slate-300" />
                  <span>Editöre Aktar</span>
                </>
              )}
            </button>

            <button
              type="button"
              onClick={onClose}
              className="px-3 py-2 rounded-xl text-xs font-medium text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
            >
              Kapat
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
