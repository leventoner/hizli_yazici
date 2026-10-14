using System;
using System.IO;

namespace YaziciCS.Managers
{
    public static class CrashLogger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");

        public static void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Log(ex, "AppDomain.UnhandledException");
                }
            };
        }

        public static void Log(Exception ex, string context = "")
        {
            try
            {
                string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText(LogFilePath, message);
            }
            catch { }
        }
    }
}
