using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace YaziciCS.Managers
{
    public class GlobalHotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _hWnd;
        private int _currentId;
        public Action OnHotkeyPressed { get; set; }
        public Action OnSttHotkeyPressed { get; set; }

        public GlobalHotkeyManager()
        {
        }

        public void Initialize(IntPtr windowHandle)
        {
            _hWnd = windowHandle;
            HwndSource source = HwndSource.FromHwnd(_hWnd);
            source.AddHook(HwndHook);
        }

        public void Register(string hotkeyStr, int id)
        {
            // Simple parser for "ctrl+c", "ctrl+shift+y"
            uint modifiers = 0;
            uint key = 0;

            string[] parts = hotkeyStr.ToLowerInvariant().Split('+');
            foreach (var part in parts)
            {
                if (part == "ctrl") modifiers |= 0x0002;
                else if (part == "alt") modifiers |= 0x0001;
                else if (part == "shift") modifiers |= 0x0004;
                else if (part == "win") modifiers |= 0x0008;
                else 
                {
                    // Map character 'c' to virtual key 'C'
                    if (part.Length == 1)
                    {
                        key = (uint)char.ToUpper(part[0]);
                    }
                }
            }

            if (key != 0)
            {
                RegisterHotKey(_hWnd, id, modifiers, key);
            }
        }

        public void Unregister(int id)
        {
            UnregisterHotKey(_hWnd, id);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == 1 && OnHotkeyPressed != null)
                {
                    OnHotkeyPressed();
                    handled = true;
                }
                else if (id == 2 && OnSttHotkeyPressed != null)
                {
                    OnSttHotkeyPressed();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotKey(_hWnd, 1);
            UnregisterHotKey(_hWnd, 2);
        }
    }
}
