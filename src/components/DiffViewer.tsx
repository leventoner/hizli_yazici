import React from 'react';
import { ArrowRight, Check, Sparkles } from 'lucide-react';

interface DiffViewerProps {
  original: string;
  modified: string;
  actionTitle: string;
}

export const DiffViewer: React.FC<DiffViewerProps> = ({ original, modified, actionTitle }) => {
  // Simple word & character difference generator
  const renderHighlightedDiff = () => {
    // If length or format differs greatly, show side by side
    if (Math.abs(original.length - modified.length) > 20 || original.includes('\n')) {
      return (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
          <div className="p-3 rounded-xl bg-slate-900/80 border border-slate-800">
            <span className="text-[10px] font-bold text-slate-500 uppercase block mb-1">
              Önceki Metin
            </span>
            <p className="text-slate-300 font-mono whitespace-pre-wrap">{original}</p>
          </div>
          <div className="p-3 rounded-xl bg-slate-900/80 border border-emerald-900/40">
            <span className="text-[10px] font-bold text-emerald-400 uppercase block mb-1">
              Dönüştürülen Metin
            </span>
            <p className="text-emerald-200 font-mono whitespace-pre-wrap">{modified}</p>
          </div>
        </div>
      );
    }

    // Character-level diff highlight for same-length or small adjustments
    const origChars = original.split('');
    const modChars = modified.split('');

    return (
      <div className="space-y-2 text-xs">
        <div className="p-3 rounded-xl bg-slate-900/90 border border-slate-800 flex flex-wrap items-center gap-0.5 font-mono">
          {modChars.map((char, index) => {
            const wasDifferent = origChars[index] !== char;
            if (wasDifferent) {
              return (
                <span
                  key={index}
                  className="bg-emerald-500/20 text-emerald-300 px-0.5 rounded font-bold border-b-2 border-emerald-400"
                  title={`'${origChars[index] || ''}' -> '${char}'`}
                >
                  {char}
                </span>
              );
            }
            return (
              <span key={index} className="text-slate-200">
                {char}
              </span>
            );
          })}
        </div>
      </div>
    );
  };

  return (
    <div className="p-4 rounded-2xl bg-slate-800/40 border border-slate-700/60 space-y-2">
      <div className="flex items-center justify-between text-xs font-semibold text-slate-400">
        <div className="flex items-center gap-1.5 text-emerald-400">
          <Check className="w-3.5 h-3.5" />
          <span>{actionTitle} Karşılaştırması</span>
        </div>
        <span className="text-[11px] text-slate-500">Değişiklikler vurgulanmıştır</span>
      </div>
      {renderHighlightedDiff()}
    </div>
  );
};
