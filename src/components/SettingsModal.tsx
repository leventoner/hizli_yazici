import React, { useState } from 'react';
import { AppSettings } from '../types';
import { Settings, X, Palette, Keyboard, Sliders, Globe, Mic, Check, Sparkles } from 'lucide-react';

interface SettingsModalProps {
  settings: AppSettings;
  isOpen: boolean;
  onClose: () => void;
  onSave: (newSettings: AppSettings) => Promise<void>;
}

const THEME_PRESETS = [
  { name: 'Klasik İndigo', value: '#3d5afe' },
  { name: 'Yeşil Zümrüt', value: '#10b981' },
  { name: 'Mor Ametist', value: '#8b5cf6' },
  { name: 'Mavi Safir', value: '#0284c7' },
  { name: 'Turuncu Güneş', value: '#f59e0b' },
  { name: 'Kırmızı Mercan', value: '#ef4444' },
  { name: 'Deniz Mavisi', value: '#06b6d4' },
];

export const SettingsModal: React.FC<SettingsModalProps> = ({
  settings: initialSettings,
  isOpen,
  onClose,
  onSave,
}) => {
  const [form, setForm] = useState<AppSettings>({ ...initialSettings });
  const [saving, setSaving] = useState(false);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await onSave(form);
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-in fade-in">
      <div className="relative w-full max-w-xl max-h-[90vh] bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl flex flex-col overflow-hidden">
        {/* Header */}
        <div
          className="p-6 text-white transition-colors duration-300 relative overflow-hidden"
          style={{ backgroundColor: form.color_theme }}
        >
          <div className="absolute inset-0 bg-black/20 pointer-events-none" />
          <div className="relative z-10 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-white/20 backdrop-blur-md">
                <Settings className="w-6 h-6 text-white" />
              </div>
              <div>
                <h3 className="text-xl font-bold tracking-tight">Uygulama Yapılandırması</h3>
                <p className="text-xs text-white/80 mt-0.5">
                  Kullanıcı deneyiminizi buradan kişiselleştirin
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="p-2 text-white/80 hover:text-white rounded-lg hover:bg-white/10 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Scrollable Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* Genel Görünüm */}
          <section className="space-y-4">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
              <Palette className="w-4 h-4" style={{ color: form.color_theme }} />
              <h4>Genel Görünüm</h4>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Uygulama Adı
                </label>
                <input
                  type="text"
                  value={form.program_name}
                  onChange={(e) => setForm({ ...form, program_name: e.target.value })}
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Tema Rengi
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="color"
                    value={form.color_theme}
                    onChange={(e) => setForm({ ...form, color_theme: e.target.value })}
                    className="w-9 h-9 rounded-lg border border-slate-700 cursor-pointer bg-slate-800 p-0.5"
                  />
                  <input
                    type="text"
                    value={form.color_theme}
                    onChange={(e) => setForm({ ...form, color_theme: e.target.value })}
                    className="flex-1 px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm font-mono text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  />
                </div>
              </div>
            </div>

            {/* Renk Paleti Hızlı Seçim */}
            <div className="flex flex-wrap gap-2 pt-1">
              {THEME_PRESETS.map((preset) => (
                <button
                  key={preset.value}
                  type="button"
                  onClick={() => setForm({ ...form, color_theme: preset.value })}
                  className={`flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs border transition-all ${
                    form.color_theme.toLowerCase() === preset.value.toLowerCase()
                      ? 'border-white bg-slate-800 text-white font-medium shadow-sm'
                      : 'border-slate-700 bg-slate-800/60 text-slate-300 hover:border-slate-600'
                  }`}
                >
                  <span
                    className="w-3 h-3 rounded-full shrink-0"
                    style={{ backgroundColor: preset.value }}
                  />
                  {preset.name}
                </button>
              ))}
            </div>
          </section>

          <hr className="border-slate-800" />

          {/* Özellikler & Fonksiyonlar */}
          <section className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
              <Sliders className="w-4 h-4" style={{ color: form.color_theme }} />
              <h4>Özellikler & Davranış</h4>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <label className="flex items-start gap-3 p-3 rounded-xl bg-slate-800/60 border border-slate-700/60 cursor-pointer hover:bg-slate-800 transition-colors">
                <input
                  type="checkbox"
                  checked={form.enable_character_fix}
                  onChange={(e) => setForm({ ...form, enable_character_fix: e.target.checked })}
                  className="mt-0.5 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500 bg-slate-900 w-4 h-4"
                />
                <div className="text-xs">
                  <span className="font-semibold text-white block">Karakter Düzeltme</span>
                  <span className="text-slate-400">Türkçe karakterleri (ç, ğ, ı, ö, ş, ü) otomatik düzeltir</span>
                </div>
              </label>

              <label className="flex items-start gap-3 p-3 rounded-xl bg-slate-800/60 border border-slate-700/60 cursor-pointer hover:bg-slate-800 transition-colors">
                <input
                  type="checkbox"
                  checked={form.enable_ai_improve}
                  onChange={(e) => setForm({ ...form, enable_ai_improve: e.target.checked })}
                  className="mt-0.5 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500 bg-slate-900 w-4 h-4"
                />
                <div className="text-xs">
                  <span className="font-semibold text-white block">AI Metin İyileştirme</span>
                  <span className="text-slate-400">Yapay zeka ile dilbilgisi, noktalama ve akıcılık iyileştirmesi</span>
                </div>
              </label>

              <label className="flex items-start gap-3 p-3 rounded-xl bg-slate-800/60 border border-slate-700/60 cursor-pointer hover:bg-slate-800 transition-colors">
                <input
                  type="checkbox"
                  checked={form.enable_speech_to_text}
                  onChange={(e) => setForm({ ...form, enable_speech_to_text: e.target.checked })}
                  className="mt-0.5 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500 bg-slate-900 w-4 h-4"
                />
                <div className="text-xs">
                  <span className="font-semibold text-white block">Sesle Yazma (STT)</span>
                  <span className="text-slate-400">Mikrofondan konuşarak metne dönüştürme</span>
                </div>
              </label>

              <label className="flex items-start gap-3 p-3 rounded-xl bg-slate-800/60 border border-slate-700/60 cursor-pointer hover:bg-slate-800 transition-colors">
                <input
                  type="checkbox"
                  checked={form.notify_on_no_change}
                  onChange={(e) => setForm({ ...form, notify_on_no_change: e.target.checked })}
                  className="mt-0.5 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500 bg-slate-900 w-4 h-4"
                />
                <div className="text-xs">
                  <span className="font-semibold text-white block">Değişiklik Bildirimi</span>
                  <span className="text-slate-400">Metin zaten düzgünse &quot;Düzeltme gerekmedi&quot; bildirimi ver</span>
                </div>
              </label>

              <label className="flex items-start gap-3 p-3 rounded-xl bg-slate-800/60 border border-slate-700/60 cursor-pointer hover:bg-slate-800 transition-colors md:col-span-2">
                <input
                  type="checkbox"
                  checked={form.auto_copy_on_action ?? true}
                  onChange={(e) => setForm({ ...form, auto_copy_on_action: e.target.checked })}
                  className="mt-0.5 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500 bg-slate-900 w-4 h-4"
                />
                <div className="text-xs">
                  <span className="font-semibold text-white block">İşlem Yapılınca Panoya Kopyala</span>
                  <span className="text-slate-400">
                    Metin seçildiğinde panonuz kesinlikle değişmez; sadece Düzelt, İyileştir veya Çevir butonlarına tıklandığında sonuç panoya kopyalanır.
                  </span>
                </div>
              </label>
            </div>
          </section>

          <hr className="border-slate-800" />

          {/* Kısayollar & Süreler */}
          <section className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
              <Keyboard className="w-4 h-4" style={{ color: form.color_theme }} />
              <h4>Klavye Kısayolları & Ses Süresi</h4>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Metin İşlem Kısayolu
                </label>
                <input
                  type="text"
                  value={form.hotkey}
                  onChange={(e) => setForm({ ...form, hotkey: e.target.value })}
                  placeholder="ctrl+c"
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm font-mono text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Sesle Yazma Kısayolu
                </label>
                <input
                  type="text"
                  value={form.stt_hotkey}
                  onChange={(e) => setForm({ ...form, stt_hotkey: e.target.value })}
                  placeholder="ctrl+shift+y"
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm font-mono text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Maksimum Ses Kayıt Süresi (Saniye)
                </label>
                <div className="flex items-center gap-3">
                  <input
                    type="range"
                    min="3"
                    max="60"
                    step="1"
                    value={form.stt_duration}
                    onChange={(e) => setForm({ ...form, stt_duration: parseInt(e.target.value, 10) })}
                    className="flex-1 accent-indigo-500"
                  />
                  <span className="text-sm font-mono text-white w-12 text-right">
                    {form.stt_duration}s
                  </span>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Yüzer Menü Gösterim Süresi (Saniye)
                </label>
                <div className="flex items-center gap-3">
                  <input
                    type="range"
                    min="3"
                    max="30"
                    step="1"
                    value={form.floating_menu_duration}
                    onChange={(e) =>
                      setForm({ ...form, floating_menu_duration: parseInt(e.target.value, 10) })
                    }
                    className="flex-1 accent-indigo-500"
                  />
                  <span className="text-sm font-mono text-white w-12 text-right">
                    {form.floating_menu_duration}s
                  </span>
                </div>
              </div>
            </div>
          </section>

          <hr className="border-slate-800" />

          {/* Metin İyileştirme Sağlayıcısı */}
          <section className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
              <Sparkles className="w-4 h-4" style={{ color: form.color_theme }} />
              <h4>Metin İyileştirme Sağlayıcısı</h4>
            </div>

            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  İyileştirme Yöntemi
                </label>
                <select
                  value={form.improve_provider || 'auto'}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      improve_provider: e.target.value as 'auto' | 'free' | 'gemini',
                    })
                  }
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  <option value="auto">Otomatik (Önerilen: Gemini dener, 503/yoğunlukta Ücretsiz Motor devreye girer)</option>
                  <option value="free">Tamamen Ücretsiz & Yerel (API Anahtarsız, 13.463 Kural & İmla Motoru)</option>
                  <option value="gemini">Yalnızca Gemini AI (Flash 3.8 / 3.6 / 2.5 modelleri)</option>
                </select>
                <p className="text-xs text-slate-400 mt-1.5 leading-relaxed">
                  Gemini Flash 3.8 ve 3.6 modelleri bazen Google sunucularında yoğunluk (503) yaşayabilir. <strong>Otomatik</strong> mod açıkken sistem kesintisiz olarak hızlı yedek modellere veya ücretsiz yerel motora geçerek sizi asla yarı yolda bırakmaz.
                </p>
              </div>
            </div>
          </section>

          <hr className="border-slate-800" />

          {/* Çeviri Sağlayıcısı */}
          <section className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
              <Globe className="w-4 h-4" style={{ color: form.color_theme }} />
              <h4>Çeviri Sağlayıcısı</h4>
            </div>

            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  Sağlayıcı Tercihi
                </label>
                <select
                  value={form.translation_provider}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      translation_provider: e.target.value as 'free' | 'gemini',
                    })
                  }
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  <option value="free">Ücretsiz (API anahtarsız - Google Dahili / MyMemory)</option>
                  <option value="gemini">Gemini Yapay Zeka (Doğal & Akıcı Çeviri)</option>
                </select>
                <p className="text-xs text-slate-400 mt-1.5 leading-relaxed">
                  Ücretsiz modda Google anahtarsız servisi kullanılır; kota aşımında MyMemory devreye girer. Gemini seçeneği gelişmiş yapay zeka çevirisi sağlar.
                </p>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1.5">
                  MyMemory İsteğe Bağlı E-posta Adresi
                </label>
                <input
                  type="email"
                  value={form.translation_email}
                  onChange={(e) => setForm({ ...form, translation_email: e.target.value })}
                  placeholder="ornek@alanadi.com"
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
                <p className="text-xs text-slate-500 mt-1">
                  E-posta girilirse MyMemory günlük ücretsiz istek limiti 5.000&apos;den 50.000 karaktere çıkar.
                </p>
              </div>
            </div>
          </section>
        </form>

        {/* Footer */}
        <div className="p-4 bg-slate-950 border-t border-slate-800 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-xl text-sm font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition-colors"
          >
            İptal
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={saving}
            className="px-6 py-2 rounded-xl text-sm font-semibold text-white shadow-lg transition-transform active:scale-95 disabled:opacity-50"
            style={{ backgroundColor: form.color_theme }}
          >
            {saving ? 'Kaydediliyor...' : 'Değişiklikleri Kaydet'}
          </button>
        </div>
      </div>
    </div>
  );
};
