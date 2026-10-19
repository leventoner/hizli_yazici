using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace YaziciCS.Managers
{
    public class GlobalMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;

        private LowLevelMouseProc proc;
        private IntPtr hookId = IntPtr.Zero;

        public event Action<int, int>? LeftButtonDown;
        public event Action<int, int>? LeftButtonUp;

        public GlobalMouseHook()
        {
            proc = HookCallback;
            hookId = SetHook(proc);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// Kanca geri çağrımı işletim sistemini bloklamaması için 0 ms içinde dönmeli.
        /// İşlemler ThreadPool üzerinden asenkron tetiklenerek farenin donması veya
        /// Windows'un kancayı zaman aşımı (LowLevelHooksTimeout) nedeniyle düşürmesi önlenir.
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int x = hookStruct.pt.x;
                    int y = hookStruct.pt.y;
                    ThreadPool.QueueUserWorkItem(_ => LeftButtonDown?.Invoke(x, y));
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int x = hookStruct.pt.x;
                    int y = hookStruct.pt.y;
                    ThreadPool.QueueUserWorkItem(_ => LeftButtonUp?.Invoke(x, y));
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(hookId);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
