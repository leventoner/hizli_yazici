using System;
using System.Threading.Tasks;
using System.Windows;
using YaziciCS.UI;

namespace YaziciCS.Managers
{
    public static class SelectionManager
    {
        private static GlobalMouseHook? mouseHook;
        private static FloatingMenu? menu;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void Initialize(FloatingMenu floatingMenu)
        {
            menu = floatingMenu;
            mouseHook = new GlobalMouseHook();
            mouseHook.LeftButtonUp += OnLeftButtonUp;
        }

        private static void OnLeftButtonUp(int x, int y)
        {
            Task.Run(async () =>
            {
                await Task.Delay(150); // Give user a moment to finish selection

                // Check clipboard
                string oldClipboard = await Services.TextService.GetClipboardTextAsync();
                
                // Simulate Ctrl+C to test if text is selected
                keybd_event(VK_CONTROL, 0, 0, 0);
                keybd_event(VK_C, 0, 0, 0);
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);

                await Task.Delay(100);
                string newClipboard = await Services.TextService.GetClipboardTextAsync();

                if (!string.IsNullOrWhiteSpace(newClipboard) && newClipboard != oldClipboard && newClipboard.Length >= 2)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        menu?.ShowAt(x, y);
                    });
                }
            });
        }

        public static void Shutdown()
        {
            mouseHook?.Dispose();
        }
    }
}
