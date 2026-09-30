using System;
using System.IO;

namespace YaziciCS.Managers
{
    /// <summary>
    /// Beklenmedik kapanmaları (crash) tespit etmek icin tum onemli olaylari
    /// ana dizindeki crash_log.txt dosyasina yazan basit logger.
    /// Her satirin basina zaman damgasi ve olay turu eklenir.
    /// </summary>
    public static class CrashLogger
    {
        private static readonly object _lock = new object();
        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");

        public static void Log(string category, Exception ex = null, string details = null)
        {
            try
            {
                lock (_lock)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
                    sb.Append(category);
                    if (!string.IsNullOrEmpty(details))
                    {
                        sb.Append(" :: ").Append(details);
                    }
                    if (ex != null)
                    {
                        sb.AppendLine();
                        sb.Append("    Exception: ").AppendLine(ex.ToString());
                    }
                    else
                    {
                        sb.AppendLine();
                    }

                    // Dosya kilitlenirse (orn. bir baska surec yaziyorsa) sessizce yut,
                    // loglama yuzunden uygulama durmasin.
                    File.AppendAllText(LogPath, sb.ToString());
                }
            }
            catch
            {
                // Loglama basarisiz olursa asla istisna firlatma.
            }
        }

        /// <summary>
        /// Uygulama her baslatildiginda cagrilir. Onceki oturumun son satirina
        /// "clean" ifadesi eklenmemisse, onceki oturum beklenmedik sekilde kapanmistir.
        /// </summary>
        public static void LogSessionStart()
        {
            try
            {
                bool previousSessionClean = true;
                if (File.Exists(LogPath))
                {
                    string[] lines = File.ReadAllLines(LogPath);
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        if (lines[i].Contains("SESSION_END"))
                        {
                            previousSessionClean = lines[i].Contains("clean");
                            break;
                        }
                        if (lines[i].Contains("SESSION_START"))
                        {
                            previousSessionClean = true;
                            break;
                        }
                    }
                }

                if (!previousSessionClean)
                {
                    Log("PREVIOUS_SESSION_CRASHED",
                        details: "Onceki oturum temiz kapatilmamiss olabilir. Yukaridaki son kayitlara bakin.");
                }
            }
            catch { }

            Log("SESSION_START", details: "PID=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Log("SESSION_END", details: "clean");
            };
        }
    }
}
