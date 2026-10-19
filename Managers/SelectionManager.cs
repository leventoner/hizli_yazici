using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using YaziciCS.Services;
using YaziciCS.UI;

namespace YaziciCS.Managers
{
    public static class SelectionManager
    {
        public const uint APP_INPUT_SIGNATURE = 0x00485A59; // 'HZY'

        private static GlobalMouseHook? mouseHook;
        private static FloatingMenu? menu;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEKEYBDHARDWAREINPUT mkhi;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_C = 0x43;
        private const int VK_SHIFT = 0x10;

        private const int SM_CXDOUBLECLK = 36;
        private const int SM_CYDOUBLECLK = 37;
        private const int SM_CXDRAG = 68;
        private const int SM_CYDRAG = 69;

        private static int downX, downY;
        private static int lastUpX, lastUpY;
        private static int lastUpTime;
        private static int clickCount;
        private static int currentRequestId;

        // Seçili metin (pano kirletilmeden, UIA veya Ctrl+C yoluyla okunur).
        private static string? selectedText;

        /// <summary>
        /// Fare ile seçilen metni döndürür. Pano bu aşamada değiştirilmez.
        /// </summary>
        public static string? GetSelectedText() => selectedText;

        public static void Initialize(FloatingMenu floatingMenu)
        {
            menu = floatingMenu;
            mouseHook = new GlobalMouseHook();
            mouseHook.LeftButtonDown += OnLeftButtonDown;
            mouseHook.LeftButtonUp += OnLeftButtonUp;
        }

        private static void OnLeftButtonDown(int x, int y)
        {
            downX = x;
            downY = y;
        }

        private static void OnLeftButtonUp(int x, int y)
        {
            if (IsInsideMenu(x, y))
            {
                return;
            }

            int now = Environment.TickCount;

            int dragDistX = Math.Abs(x - downX);
            int dragDistY = Math.Abs(y - downY);
            int minDragDistX = Math.Max(GetSystemMetrics(SM_CXDRAG), 10);
            int minDragDistY = Math.Max(GetSystemMetrics(SM_CYDRAG), 10);
            bool isDrag = (dragDistX > minDragDistX || dragDistY > minDragDistY);

            uint doubleClickTime = GetDoubleClickTime();
            if (doubleClickTime == 0) doubleClickTime = 500;
            int doubleClickDistX = GetSystemMetrics(SM_CXDOUBLECLK);
            if (doubleClickDistX <= 0) doubleClickDistX = 4;
            int doubleClickDistY = GetSystemMetrics(SM_CYDOUBLECLK);
            if (doubleClickDistY <= 0) doubleClickDistY = 4;

            int timeSinceLastUp = unchecked(now - lastUpTime);
            bool isNearLastClick = Math.Abs(x - lastUpX) <= doubleClickDistX && Math.Abs(y - lastUpY) <= doubleClickDistY;

            bool isDoubleClick = false;
            if (timeSinceLastUp >= 0 && timeSinceLastUp <= (int)doubleClickTime && isNearLastClick)
            {
                clickCount++;
                if (clickCount >= 2)
                {
                    isDoubleClick = true;
                }
            }
            else
            {
                clickCount = 1;
            }

            lastUpX = x;
            lastUpY = y;
            lastUpTime = now;

            bool isShift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;

            if (isDrag || isDoubleClick || isShift)
            {
                // Seçimin tüm dikey alanını belirle (çok satırlı seçimlerde en tepe ve en dip satırlar)
                int topY = Math.Min(downY, y);
                int bottomY = Math.Max(downY, y);
                int centerX = isDrag ? ((downX + x) / 2) : x;

                TriggerSelectionCheck(centerX, topY, bottomY);
            }
            else
            {
                // Tek durağan tıklama: menü açıksa kapat
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (menu != null && menu.IsVisible)
                    {
                        menu.HideMenu();
                    }
                });
            }
        }

        private static bool IsInsideMenu(int physX, int physY)
        {
            if (menu == null) return false;
            bool inside = false;
            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (menu != null && menu.IsVisible)
                    {
                        if (menu.IsMouseOver)
                        {
                            inside = true;
                            return;
                        }

                        // Windows DPI ölçeklemesini (125%, 150%) hesaba katarak fiziksel pikseli DIP'e çevir
                        var dpi = VisualTreeHelper.GetDpi(menu);
                        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                        double dipX = physX / scaleX;
                        double dipY = physY / scaleY;

                        inside = dipX >= menu.Left && dipX <= menu.Left + menu.ActualWidth &&
                                 dipY >= menu.Top && dipY <= menu.Top + menu.ActualHeight;
                    }
                });
            }
            catch { }
            return inside;
        }

        private static void TriggerSelectionCheck(int centerX, int topY, int bottomY)
        {
            int reqId = Interlocked.Increment(ref currentRequestId);
            Task.Run(async () =>
            {
                await Task.Delay(120);
                if (reqId != currentRequestId) return;

                await CheckSelectionAsync(centerX, topY, bottomY, reqId);
            });
        }

        private static async Task CheckSelectionAsync(int centerX, int topY, int bottomY, int reqId)
        {
            try
            {
                // 1. Panoyu kirletmeden önce UI Automation ile seçili metni ve kesin ekran koordinatlarını oku
                string? uiText = GetSelectedTextViaUIAutomation(out double? uiaCenterX, out double? uiaTopY, out double? uiaBottomY);
                if (uiText != null)
                {
                    string trimmedUi = uiText.Trim();
                    // En az 2 harfli kelimeleri ("ev", "su", "at", "to", "go") kabul et
                    if (trimmedUi.Length < 2 || CountLetters(trimmedUi) < 2)
                    {
                        selectedText = null;
                        return;
                    }

                    // ÖNEMLİ: Pano burada DEĞİŞTİRİLMEZ. Sadece seçilen metin bellekte tutulur.
                    selectedText = trimmedUi;

                    double finalCenterX = uiaCenterX ?? centerX;
                    double finalTopY = uiaTopY ?? topY;
                    double finalBottomY = uiaBottomY ?? bottomY;

                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (reqId == currentRequestId)
                        {
                            menu?.ShowAt(finalCenterX, finalTopY, finalBottomY);
                        }
                    });
                    return;
                }

                // 2. UI Automation desteklenmeyen pencerelerde (Chromium vb.) Ctrl+C simülasyonu
                if (IsConsoleWindowActive())
                {
                    return;
                }

                uint oldSeq = GetClipboardSequenceNumber();
                string oldClipboard = await TextService.GetClipboardTextAsync();

                // Modern ve atomik SendInput ile Ctrl+C simülasyonu
                SimulateCtrlC();

                await Task.Delay(100);
                if (reqId != currentRequestId) return;

                uint newSeq = GetClipboardSequenceNumber();
                string newClipboard = await TextService.GetClipboardTextAsync();

                bool changed = (newSeq != oldSeq) || (newClipboard != oldClipboard);
                if (!changed || string.IsNullOrWhiteSpace(newClipboard))
                {
                    selectedText = null;
                    return;
                }

                string trimmed = newClipboard.Trim();
                int letterCount = CountLetters(trimmed);

                // En az 2 harf seçilmiş olmalı
                if (trimmed.Length < 2 || letterCount < 2)
                {
                    selectedText = null;
                    if (newSeq != oldSeq)
                    {
                        await TextService.SetClipboardTextAsync(oldClipboard);
                    }
                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        menu?.HideMenu();
                    });
                    return;
                }

                selectedText = trimmed;

                // Pano hâlâ bizim kopyaladığımız metni içeriyorsa eski haline geri yüklenir.
                // (Kullanıcı bu arada kendi kopyalama işlemini yaptıysa dokunulmaz.)
                string currentClip = await TextService.GetClipboardTextAsync();
                if (currentClip.Trim() == trimmed)
                {
                    await TextService.SetClipboardTextAsync(oldClipboard);
                }

                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (reqId == currentRequestId)
                    {
                        menu?.ShowAt(centerX, topY, bottomY);
                    }
                });
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "SelectionManager.CheckSelectionAsync");
            }
        }

        public static void SimulateCtrlC()
        {
            INPUT[] inputs = new INPUT[4];

            // Ctrl DOWN
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].mkhi.ki = new KEYBDINPUT { wVk = VK_CONTROL, dwExtraInfo = (UIntPtr)APP_INPUT_SIGNATURE };

            // C DOWN
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].mkhi.ki = new KEYBDINPUT { wVk = VK_C, dwExtraInfo = (UIntPtr)APP_INPUT_SIGNATURE };

            // C UP
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].mkhi.ki = new KEYBDINPUT { wVk = VK_C, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = (UIntPtr)APP_INPUT_SIGNATURE };

            // Ctrl UP
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].mkhi.ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = (UIntPtr)APP_INPUT_SIGNATURE };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static string? GetSelectedTextViaUIAutomation(out double? centerX, out double? topY, out double? bottomY)
        {
            centerX = null;
            topY = null;
            bottomY = null;

            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused == null) return null;

                if (focused.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj) &&
                    patternObj is TextPattern textPattern)
                {
                    var selections = textPattern.GetSelection();
                    if (selections == null || selections.Length == 0)
                    {
                        return "";
                    }

                    var sb = new StringBuilder();
                    double minTop = double.MaxValue;
                    double maxBottom = double.MinValue;
                    double midX = 0;
                    bool hasRect = false;

                    foreach (var sel in selections)
                    {
                        sb.Append(sel.GetText(-1));

                        var rects = sel.GetBoundingRectangles();
                        if (rects != null && rects.Length > 0)
                        {
                            foreach (var r in rects)
                            {
                                if (r.Width > 0 && r.Height > 0)
                                {
                                    if (r.Top < minTop) minTop = r.Top;
                                    if (r.Bottom > maxBottom) maxBottom = r.Bottom;
                                    midX = r.Left + (r.Width / 2);
                                    hasRect = true;
                                }
                            }
                        }
                    }

                    if (hasRect)
                    {
                        centerX = midX;
                        topY = minTop;
                        bottomY = maxBottom;
                    }

                    return sb.ToString();
                }
            }
            catch
            {
                // UI Automation odaklı öğe yanıt vermiyorsa sessizce null dön
            }
            return null;
        }

        private static bool IsConsoleWindowActive()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                var sb = new StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                string className = sb.ToString();

                return className == "ConsoleWindowClass" || className == "CASCADIA_HOSTING_WINDOW_CLASS";
            }
            catch
            {
                return false;
            }
        }

        public static void NotifyActionExecuted()
        {
            selectedText = null;
        }

        public static void OnMenuDismissed()
        {
            selectedText = null;
        }

        private static int CountLetters(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetter(text[i])) count++;
            }
            return count;
        }

        public static void Shutdown()
        {
            mouseHook?.Dispose();
        }
    }
}
