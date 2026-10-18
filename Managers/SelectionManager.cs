using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using YaziciCS.Services;
using YaziciCS.UI;

namespace YaziciCS.Managers
{
    public static class SelectionManager
    {
        public const uint APP_INPUT_SIGNATURE = 0x00485A59; // 'HZY'

        private static GlobalMouseHook? mouseHook;
        private static FloatingMenu? menu;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const int VK_SHIFT = 0x10;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int SM_CXDOUBLECLK = 36;
        private const int SM_CYDOUBLECLK = 37;
        private const int SM_CXDRAG = 68;
        private const int SM_CYDRAG = 69;

        private static int downX, downY;
        private static int lastUpX, lastUpY;
        private static int lastUpTime;
        private static int clickCount;
        private static int currentRequestId;

        private static string? backupClipboard;
        private static bool hasActiveSelection;

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
                TriggerSelectionCheck(x, y);
            }
            else
            {
                // Single stationary click without selection gesture.
                // Hide floating menu if it's currently showing.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (menu != null && menu.IsVisible)
                    {
                        menu.HideMenu();
                    }
                });
            }
        }

        private static bool IsInsideMenu(int x, int y)
        {
            if (menu == null) return false;
            bool inside = false;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (menu != null && menu.IsVisible)
                    {
                        if (menu.IsMouseOver)
                        {
                            inside = true;
                            return;
                        }
                        inside = x >= menu.Left && x <= menu.Left + menu.ActualWidth &&
                                 y >= menu.Top && y <= menu.Top + menu.ActualHeight;
                    }
                });
            }
            catch { }
            return inside;
        }

        private static void TriggerSelectionCheck(int x, int y)
        {
            int reqId = Interlocked.Increment(ref currentRequestId);
            Task.Run(async () =>
            {
                await Task.Delay(120);
                if (reqId != currentRequestId) return;

                await CheckSelectionAsync(x, y, reqId);
            });
        }

        private static async Task CheckSelectionAsync(int x, int y, int reqId)
        {
            try
            {
                // 1. Try to check selection via UI Automation without touching clipboard
                string? uiText = GetSelectedTextViaUIAutomation();
                if (uiText != null)
                {
                    string trimmedUi = uiText.Trim();
                    if (trimmedUi.Length < 3 || CountLetters(trimmedUi) < 3)
                    {
                        // Active element supports TextPattern and has fewer than 3 letters selected (or empty).
                        // Do not show menu.
                        return;
                    }

                    // At least 3 letters are selected!
                    string oldClip = await TextService.GetClipboardTextAsync();
                    backupClipboard = oldClip;
                    hasActiveSelection = true;
                    await TextService.SetClipboardTextAsync(trimmedUi);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (reqId == currentRequestId)
                        {
                            menu?.ShowAt(x, y);
                        }
                    });
                    return;
                }

                // 2. UI Automation not supported for this control (e.g. Chrome, Firefox, etc.)
                // Fallback: check via clipboard change with simulated Ctrl+C.
                if (IsConsoleWindowActive())
                {
                    return;
                }

                uint oldSeq = GetClipboardSequenceNumber();
                string oldClipboard = await TextService.GetClipboardTextAsync();

                // Simulate Ctrl+C with our special signature
                keybd_event(VK_CONTROL, 0, 0, (UIntPtr)APP_INPUT_SIGNATURE);
                keybd_event(VK_C, 0, 0, (UIntPtr)APP_INPUT_SIGNATURE);
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, (UIntPtr)APP_INPUT_SIGNATURE);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, (UIntPtr)APP_INPUT_SIGNATURE);

                await Task.Delay(100);
                if (reqId != currentRequestId) return;

                uint newSeq = GetClipboardSequenceNumber();
                string newClipboard = await TextService.GetClipboardTextAsync();

                // If nothing was selected, Ctrl+C does not change clipboard sequence number in Windows
                bool changed = (newSeq != oldSeq) || (newClipboard != oldClipboard);
                if (!changed || string.IsNullOrWhiteSpace(newClipboard))
                {
                    return;
                }

                string trimmed = newClipboard.Trim();
                int letterCount = CountLetters(trimmed);

                // Requirement: At least 3 letters must be selected.
                if (trimmed.Length < 3 || letterCount < 3)
                {
                    // If the clipboard was modified by Ctrl+C (e.g. 1-2 letters copied),
                    // restore the previous clipboard so we don't leave partial text.
                    if (newSeq != oldSeq)
                    {
                        await TextService.SetClipboardTextAsync(oldClipboard);
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        menu?.HideMenu();
                    });
                    return;
                }

                // Valid selection of at least 3 letters!
                backupClipboard = oldClipboard;
                hasActiveSelection = true;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (reqId == currentRequestId)
                    {
                        menu?.ShowAt(x, y);
                    }
                });
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "SelectionManager.CheckSelectionAsync");
            }
        }

        private static string? GetSelectedTextViaUIAutomation()
        {
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
                    foreach (var sel in selections)
                    {
                        sb.Append(sel.GetText(-1));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
                // UI Automation can throw if element is invalid or unresponsive
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
            hasActiveSelection = false;
            backupClipboard = null;
        }

        public static void OnMenuDismissed()
        {
            if (hasActiveSelection && backupClipboard != null)
            {
                string toRestore = backupClipboard;
                hasActiveSelection = false;
                backupClipboard = null;
                Task.Run(async () =>
                {
                    try
                    {
                        await TextService.SetClipboardTextAsync(toRestore);
                    }
                    catch (Exception ex)
                    {
                        CrashLogger.Log(ex, "SelectionManager.OnMenuDismissed restore clipboard");
                    }
                });
            }
            else
            {
                hasActiveSelection = false;
                backupClipboard = null;
            }
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
