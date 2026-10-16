using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YaziciCS.Services;

namespace YaziciCS.Managers
{
    public static class ActionHandler
    {
        public static Action<string, string, string>? ShowNotification { get; set; }
        public static Action<string, string, string>? ShowTranslationResult { get; set; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static async Task SimulateCtrlC()
        {
            keybd_event(VK_CONTROL, 0, 0, (UIntPtr)SelectionManager.APP_INPUT_SIGNATURE);
            keybd_event(VK_C, 0, 0, (UIntPtr)SelectionManager.APP_INPUT_SIGNATURE);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, (UIntPtr)SelectionManager.APP_INPUT_SIGNATURE);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, (UIntPtr)SelectionManager.APP_INPUT_SIGNATURE);
            await Task.Delay(400);
        }

        public static async Task HandleFixClipboardAsync(bool triggerCopy = false)
        {
            if (!SettingsManager.Current.EnableCharacterFix) return;

            if (triggerCopy) await SimulateCtrlC();

            string text = await TextService.GetClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            string corrected = Deasciifier.Deasciify(text);

            if (text.Trim() != corrected.Trim())
            {
                await TextService.SetClipboardTextAsync(corrected);
                ShowNotification?.Invoke("Karakterler Düzeltildi!", corrected, "#2ecc71");
            }
            else if (SettingsManager.Current.NotifyOnNoChange)
            {
                ShowNotification?.Invoke("Düzeltme Gerekmedi", "Metin zaten düzgün görünüyor.", "#3498db");
            }
        }

        public static async Task HandleImproveClipboardAsync(bool triggerCopy = false)
        {
            if (triggerCopy) await SimulateCtrlC();

            string text = await TextService.GetClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            ShowNotification?.Invoke("İşleniyor...", "Metin sihirbaz ile iyileştiriliyor...", "#9b59b6");

            string improved = await TextService.ImproveTextAsync(text);

            if (!string.IsNullOrEmpty(improved) && !improved.StartsWith("Hata:") && !improved.StartsWith("Hiçbir model"))
            {
                await TextService.SetClipboardTextAsync(improved);
                ShowNotification?.Invoke("Metin İyileştirildi!", improved, "#9b59b6");
            }
            else
            {
                ShowNotification?.Invoke("İşlem Başarısız", improved, "#e74c3c");
            }
        }

        public static async Task HandleTranslateClipboardAsync(bool triggerCopy = false)
        {
            if (triggerCopy) await SimulateCtrlC();

            string text = await TextService.GetClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            ShowNotification?.Invoke("İşleniyor...", "Metin çevriliyor (TR ↔ EN)...", "#16a085");

            string translated = await TextService.TranslateTextAsync(text, "en");

            if (!string.IsNullOrEmpty(translated) && !translated.StartsWith("Hata:") && !translated.StartsWith("Hiçbir model"))
            {
                await TextService.SetClipboardTextAsync(translated);
                if (ShowTranslationResult != null)
                {
                    ShowTranslationResult.Invoke(text, translated, "TR ↔ EN");
                }
                else
                {
                    ShowNotification?.Invoke("Çeviri Tamamlandı! (EN)", translated, "#16a085");
                }
            }
            else
            {
                ShowNotification?.Invoke("İşlem Başarısız", translated, "#e74c3c");
            }
        }

        public static async Task HandleTranslateFrenchClipboardAsync(bool triggerCopy = false)
        {
            if (triggerCopy) await SimulateCtrlC();

            string text = await TextService.GetClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            ShowNotification?.Invoke("İşleniyor...", "Metin çevriliyor (TR ↔ FR)...", "#16a085");

            string translated = await TextService.TranslateFrenchTextAsync(text);

            if (!string.IsNullOrEmpty(translated) && !translated.StartsWith("Hata:") && !translated.StartsWith("Hiçbir model"))
            {
                await TextService.SetClipboardTextAsync(translated);
                if (ShowTranslationResult != null)
                {
                    ShowTranslationResult.Invoke(text, translated, "TR ↔ FR");
                }
                else
                {
                    ShowNotification?.Invoke("Çeviri Tamamlandı! (FR)", translated, "#16a085");
                }
            }
            else
            {
                ShowNotification?.Invoke("İşlem Başarısız", translated, "#e74c3c");
            }
        }

        public static async Task HandleTranslateDutchClipboardAsync(bool triggerCopy = false)
        {
            if (triggerCopy) await SimulateCtrlC();

            string text = await TextService.GetClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            ShowNotification?.Invoke("İşleniyor...", "Metin çevriliyor (TR ↔ NL / Flamanca)...", "#ea580c");

            string translated = await TextService.TranslateDutchTextAsync(text);

            if (!string.IsNullOrEmpty(translated) && !translated.StartsWith("Hata:") && !translated.StartsWith("Hiçbir model"))
            {
                await TextService.SetClipboardTextAsync(translated);
                if (ShowTranslationResult != null)
                {
                    ShowTranslationResult.Invoke(text, translated, "TR ↔ NL");
                }
                else
                {
                    ShowNotification?.Invoke("Çeviri Tamamlandı! (NL)", translated, "#ea580c");
                }
            }
            else
            {
                ShowNotification?.Invoke("İşlem Başarısız", translated, "#e74c3c");
            }
        }

        public static async Task HandleSpeechToTextAsync()
        {
            try 
            {
                if (!SettingsManager.Current.EnableSpeechToText)
                {
                    ShowNotification?.Invoke("Uyarı", "Sesle yazma özelliği kapalı.", "#e74c3c");
                    return;
                }

                // Halihazırda kayıt yapılıyorsa tekrar tetiklendiğinde hemen durdur ve çevir
                if (AudioService.IsRecording)
                {
                    ShowNotification?.Invoke("Kayıt Tamamlandı", "Ses işleniyor...", "#3498db");
                    AudioService.StopRecording();
                    return;
                }

                int duration = SettingsManager.Current.SttDuration;
                if (duration <= 0) duration = 10;
                string hotkey = SettingsManager.Current.SttHotkey.ToUpper();
                ShowNotification?.Invoke("Dinleniyor...", $"Konuşun... (Bitirmek için {hotkey}'e tekrar basın veya bekleyin)", "#9b59b6");

                string tempFile = Path.Combine(Path.GetTempPath(), "yazici_rec.wav");
                bool recordSuccess = await AudioService.RecordAudioAsync(tempFile, duration);

                if (!recordSuccess)
                {
                    ShowNotification?.Invoke("Uyarı", "Kayıt dosyası oluşturulamadı veya mikrofon sesi alınamadı.", "#f1c40f");
                    return;
                }

                ShowNotification?.Invoke("İşleniyor...", "Ses metne çevriliyor (Lokal Whisper)...", "#e67e22");

                string transcribed = await AudioService.TranscribeAudioAsync(tempFile);

                if (!string.IsNullOrEmpty(transcribed) && 
                    !transcribed.StartsWith("Hata") && 
                    !transcribed.StartsWith("API Hatası") && 
                    !transcribed.StartsWith("Bağlantı") && 
                    !transcribed.StartsWith("Ses algılanamadı") &&
                    !transcribed.StartsWith("Ses anlaşılamadı") &&
                    !transcribed.StartsWith("Lokal Ses Tanıma Hatası"))
                {
                    await TextService.SetClipboardTextAsync(transcribed);
                    ShowNotification?.Invoke("Panoya Kopyalandı!", transcribed, "#2ecc71");
                }
                else
                {
                    ShowNotification?.Invoke("Uyarı", transcribed, "#f1c40f");
                }
            }
            catch (Exception ex)
            {
                ShowNotification?.Invoke("Hata", "Ses işlemi sırasında hata oluştu: " + ex.Message, "#e74c3c");
            }
        }
    }
}
