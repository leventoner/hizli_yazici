using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace YaziciCS.Managers
{
    public static class GlobalHotkeyManager
    {
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static GlobalKeyboardHook? keyboardHook;
        private static bool isCtrlDown = false;
        private static bool isShiftDown = false;
        private static DateTime lastCtrlCTime = DateTime.MinValue;

        public static void Initialize()
        {
            keyboardHook = new GlobalKeyboardHook();
            keyboardHook.KeyAction += OnKeyAction;
        }

        private static void OnKeyAction(int vkCode, bool isDown)
        {
            // VK_CONTROL = 0x11, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3
            if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3)
            {
                isCtrlDown = isDown;
            }
            // VK_SHIFT = 0x10, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1
            else if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1)
            {
                isShiftDown = isDown;
            }
            // VK_A = 0x41 (Ctrl + A -> Select All)
            else if (vkCode == 0x41 && isDown && (isCtrlDown || (GetKeyState(0x11) & 0x8000) != 0) && !isShiftDown)
            {
                SelectionManager.TriggerSelectionCheckFromKeyboard();
            }
            // VK_C = 0x43
            else if (vkCode == 0x43 && isDown && isCtrlDown && !isShiftDown)
            {
                var now = DateTime.Now;
                double diff = (now - lastCtrlCTime).TotalSeconds;
                if (diff <= SettingsManager.Current.Cooldown && diff > 0.05)
                {
                    string? source = SelectionManager.GetSelectedText();
                    SelectionManager.NotifyActionExecuted();

                    Task.Run(async () =>
                    {
                        // Aktif uygulamanın (Chrome, Word vb.) 2. Ctrl+C tuşunu işleyip
                        // panoya eski metni yazmasını bekle (yarış durumunu / race condition kesin önle)
                        await Task.Delay(120);
                        await ActionHandler.HandleImproveClipboardAsync(triggerCopy: false, sourceText: source);
                    });
                }
                lastCtrlCTime = now;
            }
            // VK_Y = 0x59 (Ctrl + Shift + Y)
            else if (vkCode == 0x59 && isDown && isCtrlDown && isShiftDown)
            {
                Task.Run(async () => await ActionHandler.HandleSpeechToTextAsync());
            }
        }

        public static void Shutdown()
        {
            keyboardHook?.Dispose();
        }
    }
}
