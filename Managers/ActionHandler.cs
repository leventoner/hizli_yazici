using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YaziciCS.Services;

namespace YaziciCS.Managers
{
    public static class ActionHandler
    {
        public static Action<string, string, string> ShowNotification { get; set; }

        public static async Task HandleFixClipboardAsync()
        {
            string text = TextService.GetClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            string corrected = Deasciifier.Deasciify(text);

            if (text.Trim() != corrected.Trim())
            {
                TextService.SetClipboardText(corrected);
                ShowNotification?.Invoke("Karakterler Düzeltildi!", corrected, "#2ecc71");
            }
            else if (SettingsManager.Current.NotifyOnNoChange)
            {
                ShowNotification?.Invoke("Düzeltme Gerekmedi", "Metin zaten düzgün görünüyor.", "#3498db");
            }
        }

        public static async Task HandleImproveClipboardAsync(bool autoDetect)
        {
            string text = TextService.GetClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotification?.Invoke("Hata", "Lütfen metni seçin ve tekrar deneyin.", "#e74c3c");
                return;
            }

            string statusMsg = autoDetect ? "Metin kendi dilinde iyileştiriliyor..." : "Metin yapay zeka ile iyileştiriliyor...";
            ShowNotification?.Invoke("İşleniyor...", statusMsg, "#9b59b6");

            string improved = await TextService.ImproveTextAsync(text, autoDetect);

            if (!string.IsNullOrEmpty(improved) && !improved.StartsWith("Hata:"))
            {
                TextService.SetClipboardText(improved);
                string title = autoDetect ? "Dilde İyileştirildi!" : "Yazı İyileştirildi!";
                ShowNotification?.Invoke(title, improved, "#9b59b6");
            }
            else
            {
                ShowNotification?.Invoke("İşlem Başarısız", improved, "#e74c3c");
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

                int duration = SettingsManager.Current.SttDuration;
                ShowNotification?.Invoke("Dinleniyor...", $"Lütfen konuşun ({duration} saniye).", "#9b59b6");

                string tempFile = Path.Combine(Path.GetTempPath(), "yazici_rec.wav");
                await AudioService.RecordAudioAsync(tempFile, duration);

                ShowNotification?.Invoke("İşleniyor...", "Sesiniz metne çevriliyor...", "#e67e22");

                string transcribed = await AudioService.TranscribeAudioAsync(tempFile);

                if (!string.IsNullOrEmpty(transcribed) && !transcribed.StartsWith("Hata") && !transcribed.StartsWith("API Hatası") && !transcribed.StartsWith("Bağlantı") && !transcribed.StartsWith("Ses anlaşılamadı"))
                {
                    // Use a safer typing method
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        try 
                        {
                            System.Windows.Forms.SendKeys.SendWait(transcribed);
                        }
                        catch { /* Ignore sendkeys errors if window lost focus */ }
                    });
                    
                    ShowNotification?.Invoke("Başarılı!", $"Yazıya Geçirildi: {transcribed}", "#2ecc71");
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
