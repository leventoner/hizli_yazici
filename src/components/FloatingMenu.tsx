import React from 'react';
import { Wand2, Globe, Mic, Copy, Check, Pencil } from 'lucide-react';

interface FloatingMenuProps {
  themeColor: string;
  onFix?: () => void;
  onImprove: () => void;
  onTranslateEn: () => void;
  onTranslateFr: () => void;
  onTranslateNl: () => void;
  onRecordAudio: () => void;
  onCopy: () => void;
  position?: { top: number; left: number } | null;
  isFloating?: boolean;
  isLoading?: boolean;
  copied?: boolean;
  isSingleWord?: boolean;
}

export const FloatingMenu: React.FC<FloatingMenuProps> = ({
  themeColor,
  onFix,
  onImprove,
  onTranslateEn,
  onTranslateFr,
  onTranslateNl,
  onRecordAudio,
  onCopy,
  position,
  isFloating = false,
  isLoading = false,
  copied = false,
  isSingleWord = false,
}) => {
  const style: React.CSSProperties = isFloating && position
    ? {
        position: 'fixed',
        top: `${position.top}px`,
        left: `${position.left}px`,
        transform: 'translate(-50%, -100%) translateY(-28px)',
        zIndex: 40,
      }
    : {};

  return (
    <div
      style={style}
      className={`inline-flex items-center gap-1 p-1.5 rounded-2xl border transition-all duration-200 select-none ${
        isFloating
          ? 'shadow-2xl shadow-black/80 animate-in fade-in zoom-in-95'
          : 'shadow-lg shadow-black/40'
      }`}
      // Replicate the gradient background from FloatingMenu.xaml
      // <GradientStop Color="#2c3e50" Offset="0"/> <GradientStop Color="#1e272e" Offset="1"/>
      // and border gradient using themeColor
    >
      <div
        className="flex items-center gap-1 px-1 py-0.5 rounded-xl backdrop-blur-md"
        style={{
          background: 'linear-gradient(180deg, #2c3e50 0%, #1e272e 100%)',
          border: `1px solid ${themeColor}88`,
          boxShadow: `0 0 15px ${themeColor}33`,
        }}
      >
        {/* Metni Düzelt ve İyileştir (Karakter, İmla, Noktalama) */}
        <button
          type="button"
          onClick={onImprove}
          disabled={isLoading}
          title="Metni Düzelt ve İyileştir (Karakter, İmla, Noktalama)"
          className="group relative p-2 rounded-xl text-slate-200 hover:text-white hover:bg-white/10 active:scale-95 transition-all disabled:opacity-40"
        >
          <Wand2 className="w-4 h-4 text-amber-400 group-hover:scale-110 transition-transform" />
          <span className="sr-only">Metni Düzelt ve İyileştir</span>
        </button>

        {/* Çevir EN (Türkçe ↔ İngilizce / Sözlük) */}
        <button
          type="button"
          onClick={onTranslateEn}
          disabled={isLoading}
          title={isSingleWord ? "Sözlükte Kelime Anlamı & Çeviri (EN)" : "Çevir (Türkçe ↔ İngilizce)"}
          className="group relative p-2 rounded-xl text-slate-200 hover:text-white hover:bg-white/10 active:scale-95 transition-all disabled:opacity-40 flex items-center justify-center"
        >
          <div className="relative">
            <Globe className="w-4 h-4 text-blue-400 group-hover:scale-110 transition-transform" />
            <span className="absolute -bottom-1 -right-2 text-[8px] font-bold bg-blue-600 text-white rounded px-0.5 leading-none py-0.5">
              EN
            </span>
          </div>
          <span className="sr-only">İngilizce Çevir / Sözlük</span>
        </button>

        {/* Çevir FR (Türkçe ↔ Fransızca / Sözlük) */}
        <button
          type="button"
          onClick={onTranslateFr}
          disabled={isLoading}
          title={isSingleWord ? "Sözlükte Kelime Anlamı & Çeviri (FR)" : "Çevir (Türkçe ↔ Fransızca)"}
          className="group relative p-2 rounded-xl text-slate-200 hover:text-white hover:bg-white/10 active:scale-95 transition-all disabled:opacity-40 flex items-center justify-center"
        >
          <div className="relative">
            <Globe className="w-4 h-4 text-rose-400 group-hover:scale-110 transition-transform" />
            <span className="absolute -bottom-1 -right-2 text-[8px] font-bold bg-rose-600 text-white rounded px-0.5 leading-none py-0.5">
              FR
            </span>
          </div>
          <span className="sr-only">Fransızca Çevir / Sözlük</span>
        </button>

        {/* Çevir NL (Türkçe ↔ Flamanca / Hollandaca / Sözlük) */}
        <button
          type="button"
          onClick={onTranslateNl}
          disabled={isLoading}
          title={isSingleWord ? "Sözlükte Kelime Anlamı & Çeviri (NL)" : "Çevir (Türkçe ↔ Flamanca / Hollandaca)"}
          className="group relative p-2 rounded-xl text-slate-200 hover:text-white hover:bg-white/10 active:scale-95 transition-all disabled:opacity-40 flex items-center justify-center"
        >
          <div className="relative">
            <Globe className="w-4 h-4 text-orange-400 group-hover:scale-110 transition-transform" />
            <span className="absolute -bottom-1 -right-2 text-[8px] font-bold bg-orange-600 text-white rounded px-0.5 leading-none py-0.5">
              NL
            </span>
          </div>
          <span className="sr-only">Flamanca Çevir / Sözlük</span>
        </button>

        <div className="w-[1px] h-5 bg-white/20 mx-0.5" />

        {/* Sesle Yazma */}
        <button
          type="button"
          onClick={onRecordAudio}
          disabled={isLoading}
          title="Sesle Yazma (Mikrofon)"
          className="group relative p-2 rounded-xl text-slate-200 hover:text-white hover:bg-white/10 active:scale-95 transition-all disabled:opacity-40"
        >
          <Mic className="w-4 h-4 text-purple-400 group-hover:scale-110 transition-transform" />
          <span className="sr-only">Sesle Yazma</span>
        </button>

        {/* Panoya Kopyala */}
        <button
          type="button"
          onClick={onCopy}
          title="Panoya Kopyala"
          className="group relative p-2 rounded-xl text-slate-200 hover:text-white hover:bg-white/10 active:scale-95 transition-all"
        >
          {copied ? (
            <Check className="w-4 h-4 text-emerald-400" />
          ) : (
            <Copy className="w-4 h-4 text-slate-300 group-hover:scale-110 transition-transform" />
          )}
          <span className="sr-only">Kopyala</span>
        </button>
      </div>
    </div>
  );
};
