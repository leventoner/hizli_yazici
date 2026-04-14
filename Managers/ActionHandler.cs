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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static async Task SimulateCtrlC()
        {
            keybd_event(VK_CONTROL, 0, 0, 0);
            keybd_event(VK_C, 0, 0, 0);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
            await Task.Delay(400); // OS'un kopyalama işlemini bitirmesi için süre
        }

        public static async Task HandleFixClipboardAsync(bool triggerCopy = false)
        {
            if (triggerCopy)
            {
                await SimulateCtrlC();
            }

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

        public static async Task HandleImproveClipboardAsync(bool autoDetect, bool triggerCopy = false)
        {
            if (triggerCopy)
            {
                await SimulateCtrlC();
            }

            string text = await TextService.GetClipboardTextAsync();
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
                await TextService.SetClipboardTextAsync(improved);
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
                    // Python project waits 0.5s before writing
                    await Task.Delay(500);

                    // Use a safer typing method. SendKeys.SendWait can fail with certain characters.
                    // SendKeys.Send(string) is often more reliable for plain text.
                    // Also, we escape special SendKeys characters { } [ ] ( ) + ^ % ~
                    string escapedTranscribed = transcribed
                        .Replace("{", "{{}")
                        .Replace("}", "{}}")
                        .Replace("[", "{[}")
                        .Replace("]", "{]}")
                        .Replace("(", "{(}")
                        .Replace(")", "{)}")
                        .Replace("+", "{+}")
                        .Replace("^", "{^}")
                        .Replace("%", "{%}")
                        .Replace("~", "{~}");

                    try 
                    {
                        System.Windows.Forms.SendKeys.SendWait(escapedTranscribed);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SendKeys error: " + ex.Message);
                    }
                    
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
