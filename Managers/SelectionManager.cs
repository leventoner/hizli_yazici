using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
            int minDragDist = GetSystemMetrics(SM_CXDRAG);
            if (minDragDist <= 0) minDragDist = 4;
            bool isDrag = (dragDistX > minDragDist || dragDistY > minDragDist);

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
                await Task.Delay(150);
                if (reqId != currentRequestId) return;

                await CheckSelectionAsync(x, y, reqId);
            });
        }

        private static async Task CheckSelectionAsync(int x, int y, int reqId)
        {
            try
            {
                // Just show the menu at the click position.
                // The clipboard is NOT touched here; the selected text is copied
                // only when the user actually picks an action from the menu.
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
