using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using YaziciCS.Services;

namespace YaziciCS.Managers
{
    public class SelectionManager
    {
        private readonly GlobalMouseHook _mouseHook;
        private System.Windows.Point _mouseDownPos;
        private DateTime _lastUpTime = DateTime.MinValue;
        private int _clickCount = 0;
        private string _lastClipboardText = "";
        
        public Action<System.Windows.Point> OnTextSelected { get; set; }
        public Action OnClickOutside { get; set; }

        // We check UI active menu by callback or static
        public Func<System.Windows.Point, bool> IsClickOnMenu { get; set; }

        public SelectionManager()
        {
            _mouseHook = new GlobalMouseHook();
            _mouseHook.OnLeftButtonDown += MouseHook_OnLeftButtonDown;
            _mouseHook.OnLeftButtonUp += MouseHook_OnLeftButtonUp;
        }

        private void MouseHook_OnLeftButtonDown(int x, int y)
        {
            var currentPos = new System.Windows.Point(x, y);

            if (IsClickOnMenu != null && !IsClickOnMenu(currentPos))
            {
                OnClickOutside?.Invoke();
            }

            var now = DateTime.Now;
            if ((now - _lastUpTime).TotalMilliseconds < 400)
            {
                _clickCount++;
            }
            else
            {
                _clickCount = 1;
            }

            _mouseDownPos = currentPos;

            // Her tıklamada clipboard içeriğini kaydet (çift tıklama veya sürükleme sonrası metin seçildiğinde karşılaştırmak için)
            _lastClipboardText = TextService.GetClipboardText() ?? "";
        }

        private void MouseHook_OnLeftButtonUp(int x, int y)
        {
            _lastUpTime = DateTime.Now;
            var currentPos = new System.Windows.Point(x, y);

            bool isDragging = false;
            double dist = Math.Sqrt(Math.Pow(currentPos.X - _mouseDownPos.X, 2) + Math.Pow(currentPos.Y - _mouseDownPos.Y, 2));
            if (dist > 15)
            {
                isDragging = true;
            }

            if (_clickCount >= 2 || isDragging)
            {
                // Delay slightly to let OS select the text
                Task.Delay(100).ContinueWith(_ => 
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        CheckAndTriggerMenu(currentPos);
                    });
                });
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void CheckAndTriggerMenu(System.Windows.Point pos)
        {
            try 
            {
                // Simulate Ctrl+C to copy selected text
                keybd_event(VK_CONTROL, 0, 0, 0);
                keybd_event(VK_C, 0, 0, 0);
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);

                // Give OS time to finish selection
                System.Threading.Thread.Sleep(400); 

                string currentClip = TextService.GetClipboardText() ?? "";

                // ÖNEMLİ: Sadece gerçekten yeni metin seçildiğinde menüyü göster
                // Eğer clipboard içeriği değişmediyse, metin seçilmemiştir
                if (!string.IsNullOrWhiteSpace(currentClip) && currentClip != _lastClipboardText)
                {
                    OnTextSelected?.Invoke(pos);
                }
            } 
            catch (Exception ex)
            {
                ActionHandler.ShowNotification?.Invoke("Hata", "Seçim Hatası: " + ex.Message, "#e74c3c");
            }
        }
    }
}
