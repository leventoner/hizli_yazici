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
  Sparkles,
  BookOpen,
  List,
  Quote,
  Layers,
  ChevronRight,
  ExternalLink
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
  const [copiedExampleIndex, setCopiedExampleIndex] = useState<string | null>(null);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [applied, setApplied] = useState(false);
  const [viewMode, setViewMode] = useState<'dictionary' | 'plain'>('dictionary');
  const textContainerRef = useRef<HTMLDivElement>(null);

  const { isOpen, originalText, translatedText, targetLang, provider, isDictionary, dictionaryData } = data;

  // Sync initial viewMode when modal opens
  useEffect(() => {
    if (isOpen) {
      setViewMode(isDictionary && dictionaryData ? 'dictionary' : 'plain');
      setCopied(false);
      setApplied(false);
      setCopiedExampleIndex(null);
    }
  }, [isOpen, isDictionary, dictionaryData]);

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

  const handleCopyText = async (textToCopy: string) => {
    try {
      await navigator.clipboard.writeText(textToCopy);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Kopyalama hatası:', err);
    }
  };

  const handleCopyExample = async (id: string, textToCopy: string) => {
    try {
      await navigator.clipboard.writeText(textToCopy);
      setCopiedExampleIndex(id);
      setTimeout(() => setCopiedExampleIndex(null), 2000);
    } catch (err) {
      console.error('Örnek kopyalama hatası:', err);
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

  const handleSpeakText = (textToSpeak: string, lang = langInfo.speechLang) => {
    if (!('speechSynthesis' in window)) return;

    if (isSpeaking) {
      window.speechSynthesis.cancel();
      setIsSpeaking(false);
      return;
    }

    const utterance = new SpeechSynthesisUtterance(textToSpeak);
    utterance.lang = lang;
    utterance.rate = 0.92;

    utterance.onend = () => setIsSpeaking(false);
    utterance.onerror = () => setIsSpeaking(false);

    setIsSpeaking(true);
    window.speechSynthesis.speak(utterance);
  };

  const handleApply = (textToApply: string) => {
    onApplyToEditor(textToApply);
    setApplied(true);
    setTimeout(() => {
      setApplied(false);
      onClose();
    }, 600);
  };

  // Compile dictionary text for clipboard copy
  const getFullDictionaryCopyText = () => {
    if (!dictionaryData) return translatedText;
    let result = `${dictionaryData.rootWord} (${dictionaryData.partOfSpeech})\n`;
    if (dictionaryData.grammaticalNote) {
      result += `Ek Bilgisi: ${dictionaryData.grammaticalNote} (Seçilen: ${dictionaryData.originalWord})\n`;
    }
    result += `Anlamlar:\n`;
    dictionaryData.meanings.forEach((m, idx) => {
      result += `${idx + 1}. ${m.meaning}${m.definition ? ` - ${m.definition}` : ''}\n`;
      if (m.examples && m.examples.length > 0) {
        m.examples.forEach((ex) => {
          result += `   Örnek: "${ex.source}" ➔ "${ex.target}"\n`;
        });
      }
    });
    return result.trim();
  };

  // Determine width adaptation
  const isDictActive = viewMode === 'dictionary' && Boolean(dictionaryData);
  const containerMaxWidth = isDictActive
    ? 'max-w-2xl lg:max-w-3xl'
    : translatedText.length < 100
    ? 'max-w-md'
    : translatedText.length < 300
    ? 'max-w-lg'
    : 'max-w-2xl';

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6 bg-black/75 backdrop-blur-md animate-in fade-in duration-200"
      onClick={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
    >
      <div
        className={`relative w-full ${containerMaxWidth} max-h-[90vh] flex flex-col rounded-3xl bg-slate-900/95 border border-slate-700/80 shadow-2xl shadow-black/80 overflow-hidden transition-all duration-300 scale-100 animate-in zoom-in-95`}
        style={{
          boxShadow: `0 25px 60px -15px rgba(0, 0, 0, 0.8), 0 0 30px ${themeColor}25`,
          borderColor: `${themeColor}55`,
        }}
      >
        {/* Top Header Bar */}
        <div className="px-5 py-4 bg-slate-950/80 border-b border-slate-800 flex items-center justify-between gap-3 shrink-0">
          <div className="flex items-center gap-3">
            <div
              className="w-10 h-10 rounded-2xl flex items-center justify-center shadow-lg"
              style={{
                backgroundColor: isDictActive ? '#10b98125' : `${themeColor}25`,
                color: isDictActive ? '#34d399' : themeColor,
              }}
            >
              {isDictActive ? <BookOpen className="w-5 h-5" /> : <Globe className="w-5 h-5" />}
            </div>

            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-sm sm:text-base font-bold text-white tracking-tight flex items-center gap-2">
                  {isDictActive ? 'Sözlük & Anlam Analizi' : 'Çeviri Sonucu'}
                </h3>
                <span
                  className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border uppercase tracking-wider flex items-center gap-1.5 ${langInfo.bgBadge}`}
                >
                  <span className={`w-1.5 h-1.5 rounded-full ${langInfo.dotColor}`} />
                  {langInfo.name} ({langInfo.code})
                </span>
              </div>
              <p className="text-[11px] text-slate-400 flex items-center gap-1.5 mt-0.5">
                <ArrowRightLeft className="w-3 h-3 text-slate-500" />
                <span>
                  {isDictActive ? `Tek Kelime Sözlüğü (TR ↔ ${langInfo.code})` : `Türkçe ↔ ${langInfo.name} Çevirisi`}
                </span>
                {provider && (
                  <>
                    <span>•</span>
                    <span className="text-slate-400 font-mono text-[10px]">
                      {provider === 'gemini' ? 'Gemini AI Sözlük' : 'Sözlük Servisi'}
                    </span>
                  </>
                )}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {/* Toggle view if dictionary is available */}
            {dictionaryData && (
              <div className="hidden sm:flex items-center p-0.5 bg-slate-800/80 rounded-xl border border-slate-700/60 text-xs">
                <button
                  type="button"
                  onClick={() => setViewMode('dictionary')}
                  className={`px-2.5 py-1 rounded-lg font-medium transition-all flex items-center gap-1.5 ${
                    viewMode === 'dictionary'
                      ? 'bg-emerald-600 text-white shadow-sm'
                      : 'text-slate-400 hover:text-white'
                  }`}
                >
                  <BookOpen className="w-3.5 h-3.5" />
                  <span>Sözlük</span>
                </button>
                <button
                  type="button"
                  onClick={() => setViewMode('plain')}
                  className={`px-2.5 py-1 rounded-lg font-medium transition-all flex items-center gap-1.5 ${
                    viewMode === 'plain'
                      ? 'bg-indigo-600 text-white shadow-sm'
                      : 'text-slate-400 hover:text-white'
                  }`}
                >
                  <FileText className="w-3.5 h-3.5" />
                  <span>Düz Çeviri</span>
                </button>
              </div>
            )}

            <button
              onClick={onClose}
              className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded-xl transition-colors"
              title="Kapat (Esc)"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Modal Scrollable Body */}
        <div className="p-5 sm:p-6 overflow-y-auto space-y-5 flex-1">
          {/* DICTIONARY VIEW */}
          {isDictActive && dictionaryData ? (
            <div className="space-y-4 select-text">
              {/* Word Root & Morphology Card */}
              <div className="p-4 sm:p-5 rounded-2xl bg-gradient-to-br from-slate-950 to-slate-900 border border-emerald-500/30 shadow-lg space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-baseline gap-3">
                    <span className="text-2xl sm:text-3xl font-extrabold text-white tracking-tight">
                      {dictionaryData.rootWord}
                    </span>
                    {dictionaryData.phonetic && (
                      <span className="text-sm font-mono text-emerald-400 font-normal">
                        {dictionaryData.phonetic}
                      </span>
                    )}
                    <span className="px-2.5 py-0.5 rounded-lg text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                      {dictionaryData.partOfSpeech}
                    </span>
                  </div>

                  <button
                    type="button"
                    onClick={() => handleSpeakText(dictionaryData.rootWord)}
                    className="p-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 hover:text-white border border-slate-700 transition-colors"
                    title="Kök Kelimeyi Dinle"
                  >
                    <Volume2 className="w-4 h-4 text-emerald-400" />
                  </button>
                </div>

                {/* Grammatical Analysis (Suffix/Inflection details) */}
                {(dictionaryData.originalWord.toLowerCase() !== dictionaryData.rootWord.toLowerCase() ||
                  dictionaryData.grammaticalNote) && (
                  <div className="p-3 rounded-xl bg-slate-900/90 border border-slate-800 text-xs flex flex-wrap items-center gap-2">
                    <span className="text-slate-400">Metindeki Kelime:</span>
                    <span className="px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 font-semibold border border-indigo-500/30">
                      &quot;{dictionaryData.originalWord}&quot;
                    </span>
                    <ChevronRight className="w-3.5 h-3.5 text-slate-600" />
                    <span className="text-slate-400">Kök Hali:</span>
                    <span className="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 font-semibold border border-emerald-500/30">
                      {dictionaryData.rootWord}
                    </span>
                    {dictionaryData.grammaticalNote && (
                      <>
                        <span className="text-slate-600">•</span>
                        <span className="text-amber-300/90 font-medium">
                          {dictionaryData.grammaticalNote}
                        </span>
                      </>
                    )}
                  </div>
                )}
              </div>

              {/* Meanings & Examples */}
              <div className="space-y-3">
                <div className="flex items-center justify-between text-xs text-slate-400 px-1">
                  <span className="font-semibold text-slate-300 flex items-center gap-1.5">
                    <Sparkles className="w-3.5 h-3.5 text-emerald-400" />
                    Kelime Anlamları & Karşılıkları ({langInfo.name}):
                  </span>
                  <span className="text-[11px] text-slate-500">
                    {dictionaryData.meanings.length} anlam bulundu
                  </span>
                </div>

                <div className="space-y-3">
                  {dictionaryData.meanings.map((meaning, idx) => (
                    <div
                      key={idx}
                      className="p-4 rounded-2xl bg-slate-950/70 border border-slate-800/90 hover:border-slate-700/90 transition-all space-y-3"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-bold flex items-center justify-center shrink-0">
                              {idx + 1}
                            </span>
                            <span className="text-base sm:text-lg font-bold text-white selection:bg-indigo-600">
                              {meaning.meaning}
                            </span>
                          </div>

                          {meaning.definition && (
                            <p className="text-xs text-slate-400 pl-7 leading-relaxed">
                              {meaning.definition}
                            </p>
                          )}
                        </div>

                        <div className="flex items-center gap-1.5 shrink-0">
                          <button
                            type="button"
                            onClick={() => handleCopyText(meaning.meaning)}
                            className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg transition-colors"
                            title="Bu Anlamı Kopyala"
                          >
                            <Copy className="w-3.5 h-3.5" />
                          </button>
                          <button
                            type="button"
                            onClick={() => handleSpeakText(meaning.meaning, langInfo.speechLang)}
                            className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg transition-colors"
                            title="Telaffuzu Dinle"
                          >
                            <Volume2 className="w-3.5 h-3.5 text-emerald-400" />
                          </button>
                        </div>
                      </div>

                      {/* Example sentences for this meaning */}
                      {meaning.examples && meaning.examples.length > 0 && (
                        <div className="pl-7 space-y-2 pt-2 border-t border-slate-800/60">
                          <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-500 flex items-center gap-1">
                            <Quote className="w-3 h-3 text-slate-500" />
                            Cümle İçinde Kullanım Örnekleri:
                          </span>

                          <div className="space-y-2">
                            {meaning.examples.map((ex, exIdx) => {
                              const exId = `${idx}-${exIdx}`;
                              const isExampleCopied = copiedExampleIndex === exId;

                              return (
                                <div
                                  key={exIdx}
                                  className="p-2.5 rounded-xl bg-slate-900/60 border border-slate-800 hover:border-slate-700 text-xs space-y-1 group transition-colors"
                                >
                                  <div className="flex items-start justify-between gap-2">
                                    <p className="text-slate-200 font-medium selection:bg-indigo-600">
                                      &quot;{ex.source}&quot;
                                    </p>
                                    <div className="flex items-center gap-1 shrink-0 opacity-80 group-hover:opacity-100 transition-opacity">
                                      <button
                                        type="button"
                                        onClick={() => handleSpeakText(ex.source)}
                                        className="p-1 hover:text-white rounded"
                                        title="Cümleyi Sesli Dinle"
                                      >
                                        <Volume2 className="w-3 h-3 text-slate-400" />
                                      </button>
                                      <button
                                        type="button"
                                        onClick={() =>
                                          handleCopyExample(exId, `"${ex.source}" ➔ "${ex.target}"`)
                                        }
                                        className="p-1 hover:text-white rounded"
                                        title="Örneği Kopyala"
                                      >
                                        {isExampleCopied ? (
                                          <Check className="w-3 h-3 text-emerald-400" />
                                        ) : (
                                          <Copy className="w-3 h-3 text-slate-400" />
                                        )}
                                      </button>
                                    </div>
                                  </div>
                                  <p className="text-emerald-400/90 pl-2 border-l border-emerald-500/40 text-[11px] selection:bg-indigo-600">
                                    ➔ {ex.target}
                                  </p>
                                </div>
                              );
                            })}
                          </div>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>

              {/* Synonyms / Eş Anlamlılar */}
              {dictionaryData.synonyms && dictionaryData.synonyms.length > 0 && (
                <div className="p-3.5 rounded-2xl bg-slate-950/50 border border-slate-800/80 space-y-2">
                  <span className="text-[11px] font-semibold text-slate-400 flex items-center gap-1.5">
                    <Layers className="w-3.5 h-3.5 text-indigo-400" />
                    Eş / Benzer Anlamlılar:
                  </span>
                  <div className="flex flex-wrap gap-1.5">
                    {dictionaryData.synonyms.map((syn, sIdx) => (
                      <span
                        key={sIdx}
                        className="px-2.5 py-1 rounded-lg text-xs font-medium bg-slate-800 text-slate-300 border border-slate-700/80"
                      >
                        {syn}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ) : (
            /* PLAIN / REGULAR TRANSLATION VIEW */
            <div className="space-y-4">
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
          )}
        </div>

        {/* Bottom Actions Toolbar */}
        <div className="px-5 py-4 bg-slate-950/90 border-t border-slate-800 flex flex-wrap items-center justify-between gap-2.5 shrink-0">
          {/* Left Actions: TTS Pronunciation */}
          <div className="flex items-center gap-2">
            {'speechSynthesis' in window && (
              <button
                type="button"
                onClick={() =>
                  handleSpeakText(
                    isDictActive && dictionaryData ? dictionaryData.rootWord : translatedText,
                    langInfo.speechLang
                  )
                }
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

            {dictionaryData && (
              <span className="hidden sm:inline text-[11px] text-slate-500">
                {isDictActive ? '📖 Tek kelime sözlük modu aktif' : '📝 Cümle çevirisi modu'}
              </span>
            )}
          </div>

          {/* Right Actions: Copy, Apply to Editor, Close */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() =>
                handleCopyText(isDictActive && dictionaryData ? getFullDictionaryCopyText() : translatedText)
              }
              className={`px-4 py-2 rounded-xl text-xs font-semibold flex items-center gap-2 transition-all active:scale-95 shadow-md ${
                copied
                  ? 'bg-emerald-600 text-white shadow-emerald-900/40'
                  : 'bg-indigo-600 hover:bg-indigo-500 text-white shadow-indigo-900/40'
              }`}
              title="Panoya Kopyala"
            >
              {copied ? (
                <>
                  <Check className="w-4 h-4 text-white" />
                  <span>Kopyalandı!</span>
                </>
              ) : (
                <>
                  <Copy className="w-4 h-4 text-white" />
                  <span>{isDictActive ? 'Sözlüğü Kopyala' : 'Panoya Kopyala'}</span>
                </>
              )}
            </button>

            <button
              type="button"
              onClick={() =>
                handleApply(
                  isDictActive && dictionaryData
                    ? dictionaryData.meanings[0]?.meaning || dictionaryData.rootWord
                    : translatedText
                )
              }
              className="px-3.5 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 hover:text-white border border-slate-700 text-xs font-medium flex items-center gap-1.5 transition-all active:scale-95"
              title="Kelimeyi/çeviriyi ana yazı editörüne aktar"
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
