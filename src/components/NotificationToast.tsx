import React from 'react';
import { ToastMessage } from '../types';
import { CheckCircle2, AlertCircle, Info, Sparkles, Languages, X } from 'lucide-react';

interface NotificationToastProps {
  toasts: ToastMessage[];
  onDismiss: (id: string) => void;
}

export const NotificationToast: React.FC<NotificationToastProps> = ({ toasts, onDismiss }) => {
  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-6 right-6 z-50 flex flex-col gap-3 max-w-sm w-full pointer-events-none">
      {toasts.map((toast) => {
        let Icon = CheckCircle2;
        if (toast.color.includes('e74c3c') || toast.color.includes('ef4444')) {
          Icon = AlertCircle;
        } else if (toast.color.includes('9b59b6')) {
          Icon = Sparkles;
        } else if (toast.color.includes('16a085')) {
          Icon = Languages;
        } else if (toast.color.includes('3498db')) {
          Icon = Info;
        }

        return (
          <div
            key={toast.id}
            className="pointer-events-auto flex items-start gap-3 p-4 rounded-xl bg-slate-900/95 backdrop-blur-md border border-slate-700/80 shadow-2xl transition-all duration-300 animate-in slide-in-from-bottom-5"
            style={{
              borderLeftWidth: '4px',
              borderLeftColor: toast.color,
            }}
          >
            <div
              className="p-1.5 rounded-lg flex items-center justify-center shrink-0"
              style={{ backgroundColor: `${toast.color}20`, color: toast.color }}
            >
              <Icon className="w-5 h-5" />
            </div>
            <div className="flex-1 min-w-0">
              <h4 className="text-sm font-semibold text-white tracking-tight">{toast.title}</h4>
              <p className="text-xs text-slate-300 mt-0.5 line-clamp-3 break-words font-normal">
                {toast.message}
              </p>
            </div>
            <button
              onClick={() => onDismiss(toast.id)}
              className="text-slate-400 hover:text-white p-1 rounded-md transition-colors"
              title="Kapat"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        );
      })}
    </div>
  );
};
