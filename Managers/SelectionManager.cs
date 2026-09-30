using System;
using System.Linq;
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

        
        public Action<System.Windows.Point, string> OnTextSelected { get; set; }
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

            // Sürüklemeye başlama eşiğini aşan tıklamalarda çift/tık sayısını sıfırla,
            // böylece sürükle-bırak sonrası yanlışlıkla çift tık mantığı çalışmaz.
            if (isDragging)
            {
                _clickCount = 0;
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
                // UIAutomation kullanarak panoyu bozmadan seçili metni okumaya çalış
                string selectedText = PeekSelectedTextViaUIAutomation();

                // TextPattern desteklenmeyen ya da bilinmeyen bir durum:
                // Kanıtlanmış en az 2 karakterlik bir seçim olmadan menüyü ASLA açma.
                if (selectedText == null)
                {
                    return;
                }

                // En az 2 karakterlik (harf/rakam içeren) bir seçim olmadan menüyü açma.
                if (!HasMeaningfulSelection(selectedText))
                {
                    return;
                }

                // Artık burada otomatik Ctrl+C göndermiyoruz. Böylece kullanıcının panosu sadece işlem yapıldığında değişir.
                OnTextSelected?.Invoke(pos, selectedText);
            } 
            catch (Exception ex)
            {
                ActionHandler.ShowNotification?.Invoke("Hata", "Seçim Hatası: " + ex.Message, "#e74c3c");
            }
        }

        /// <summary>
        /// Seçim menünün açılmasını gerektirecek kadar anlamlı mı?
        /// En az 2 karakter olmalı ve en az 2 harf/rakam içermeli.
        /// </summary>
        private static bool HasMeaningfulSelection(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            string trimmed = text.Trim();
            if (trimmed.Length < 2) return false;

            int letterOrDigitCount = trimmed.Count(char.IsLetterOrDigit);
            return letterOrDigitCount >= 2;
        }

        private string PeekSelectedTextViaUIAutomation()
        {
            try
            {
                // Mouse hook callback thread'inde runspace bulunmaz; STA dispatcher thread'inde oku.
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    return dispatcher.Invoke(() => PeekSelectedTextViaUIAutomationCore());
                }
                return PeekSelectedTextViaUIAutomationCore();
            }
            catch 
            { 
            }
            return null; // Bilinmeyen durumda null dön, menü açılmaz.
        }

        private string PeekSelectedTextViaUIAutomationCore()
        {
            try
            {
                var focusedElement = System.Windows.Automation.AutomationElement.FocusedElement;
                if (focusedElement != null)
                {
                    if (focusedElement.TryGetCurrentPattern(System.Windows.Automation.TextPattern.Pattern, out object patternObj))
                    {
                        var textPattern = (System.Windows.Automation.TextPattern)patternObj;
                        var selections = textPattern.GetSelection();
                        if (selections != null && selections.Length > 0)
                        {
                            // Birden fazla seçim range'i varsa hepsini birleştir.
                            string combined = string.Concat(selections.Select(safe =>
                            {
                                try { return safe.GetText(-1) ?? ""; }
                                catch { return ""; }
                            }));
                            return combined;
                        }
                        return ""; // TextPattern destekliyor ancak seçim yok, menüyü engelle
                    }
                    else
                    {
                        // TextPattern desteklemeyen ögeler (ör. Klasör içi boşluklar)
                        var type = focusedElement.Current.ControlType;
                        if (type == System.Windows.Automation.ControlType.List ||
                            type == System.Windows.Automation.ControlType.ListItem ||
                            type == System.Windows.Automation.ControlType.Pane ||
                            type == System.Windows.Automation.ControlType.Window ||
                            type == System.Windows.Automation.ControlType.Tree ||
                            type == System.Windows.Automation.ControlType.TreeItem ||
                            type == System.Windows.Automation.ControlType.Button ||
                            type == System.Windows.Automation.ControlType.DataGrid ||
                            type == System.Windows.Automation.ControlType.Table ||
                            type == System.Windows.Automation.ControlType.Thumb ||
                            type == System.Windows.Automation.ControlType.ScrollBar ||
                            type == System.Windows.Automation.ControlType.Header)
                        {
                            return ""; // Menünün açılmasını engeller
                        }
                    }
                }
            }
            catch 
            { 
            }
            return null; // Bilinmeyen durumda null dön, menü açılmaz.
        }
    }
}
