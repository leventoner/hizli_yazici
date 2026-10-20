using System;
using System.Collections.Concurrent;
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

        // Kanca geri çağrımı işletim sistemini bloklamaması için 0 ms içinde dönmeli.
        // Bu yüzden olaylar ayrı ThreadPool işlerine değil, TEK bir sıralı kuyruğa verilir.
        // Böylece "down" olayı her zaman "up" olayından ÖNCE işlenir; aksi halde
        // seçim hareketi (sürükleme) yanlışlıkla "tek tıklama" sayılır ve menü açılmaz.
        private readonly ConcurrentQueue<(Action<int, int> Handler, int X, int Y)> eventQueue = new();
        private readonly SemaphoreSlim eventSignal = new(0);
        private readonly Thread workerThread;
        private volatile bool disposed;

        public GlobalMouseHook()
        {
            proc = HookCallback;
            hookId = SetHook(proc);

            workerThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "MouseHookWorker"
            };
            workerThread.Start();
        }

        private void ProcessQueue()
        {
            try
            {
                while (!disposed)
                {
                    eventSignal.Wait();
                    if (disposed) return;

                    // Kuyruktaki tüm olayları sırayla işle (drain)
                    while (eventQueue.TryDequeue(out var item))
                    {
                        try { item.Handler.Invoke(item.X, item.Y); }
                        catch { /* olay hatası uygulamayı düşürmesin */ }
                    }
                }
            }
            catch { }
        }

        private void Enqueue(Action<int, int> handler, int x, int y)
        {
            if (disposed) return;
            eventQueue.Enqueue((handler, x, y));
            try { eventSignal.Release(); }
            catch (ObjectDisposedException) { }
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
        /// İşlemler ayrı bir işleyici thread'indeki sıralı kuyruk üzerinden asenkron tetiklenir.
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
                    Enqueue((hx, hy) => LeftButtonDown?.Invoke(hx, hy), x, y);
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int x = hookStruct.pt.x;
                    int y = hookStruct.pt.y;
                    Enqueue((hx, hy) => LeftButtonUp?.Invoke(hx, hy), x, y);
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            disposed = true;
            UnhookWindowsHookEx(hookId);

            // Worker thread'i uyandır ve bitmesini bekle.
            // (SemaphoreSlim'i Dispose etmeden önce thread'in çıkması gerekir,
            //  aksi halde thread IDisposable nesne üzerinde çalışır.)
            try { eventSignal.Release(); } catch (ObjectDisposedException) { }

            try
            {
                if (workerThread.IsAlive)
                {
                    workerThread.Join(500);
                }
            }
            catch { }

            try { eventSignal.Dispose(); } catch { }
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
