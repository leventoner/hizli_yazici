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


        private void CheckAndTriggerMenu(System.Windows.Point pos)
        {
            try 
            {
                // Artık burada otomatik Ctrl+C göndermiyoruz. Böylece kullanıcının panosu sadece işlem yapıldığında değişir.
                OnTextSelected?.Invoke(pos);
            } 
            catch (Exception ex)
            {
                ActionHandler.ShowNotification?.Invoke("Hata", "Seçim Hatası: " + ex.Message, "#e74c3c");
            }
        }
    }
}
