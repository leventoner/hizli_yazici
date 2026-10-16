using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using YaziciCS.Managers;
using YaziciCS.UI;
using Application = System.Windows.Application;

namespace YaziciCS
{
    public partial class App : Application
    {
        private NotifyIcon? notifyIcon;
        private FloatingMenu? floatingMenu;
        private SettingsWindow? settingsWindow;
        private TranslationResultWindow? translationResultWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CrashLogger.Initialize();
            SettingsManager.Load();

            // Setup Notification handler
            ActionHandler.ShowNotification = (title, msg, color) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var notif = new NotificationWindow(title, msg, color);
                    notif.Show();
                });
            };

            // Setup Translation Result Window handler (centered, dynamic sizing, selectable & copyable).
            // Only ONE result window is allowed at a time. Reuse the existing window if open,
            // otherwise create a new one. When closed, clear the reference so a new one can be created.
            ActionHandler.ShowTranslationResult = (originalText, translatedText, langPair) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (translationResultWindow == null || !translationResultWindow.IsLoaded)
                    {
                        translationResultWindow = new TranslationResultWindow(originalText, translatedText, langPair);
                        translationResultWindow.Closed += (s, e) => translationResultWindow = null;
                        translationResultWindow.Show();
                    }
                    else
                    {
                        translationResultWindow.UpdateContent(originalText, translatedText, langPair);
                        translationResultWindow.Activate();
                    }
                });
            };

            // Floating Menu
            floatingMenu = new FloatingMenu();

            // Selection manager
            SelectionManager.Initialize(floatingMenu);

            // Global Hotkeys
            GlobalHotkeyManager.Initialize();

            // System Tray
            SetupSystemTray();
        }

        private void SetupSystemTray()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = GetAppIcon();
            notifyIcon.Text = SettingsManager.Current.ProgramName;
            notifyIcon.Visible = true;

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("🎙️ Sesle Yaz (Ctrl+Shift+Y)", null, (s, e) => _ = ActionHandler.HandleSpeechToTextAsync());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("⚙️ Ayarlar", null, (s, e) => OpenSettings());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("❌ Çıkış", null, (s, e) => ExitApplication());

            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private Icon GetAppIcon()
        {
            try
            {
                // 1. Output directory (BaseDirectory)
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }

                // 2. Current working directory
                string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "icon.ico");
                if (File.Exists(cwdPath))
                {
                    return new Icon(cwdPath);
                }

                // 3. Project root during development (relative to bin/Debug/...)
                string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "icon.ico"));
                if (File.Exists(devPath))
                {
                    return new Icon(devPath);
                }

                // 4. Extract embedded application icon from the running process
                if (!string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
                {
                    var extracted = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                    if (extracted != null)
                    {
                        return extracted;
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "Tray icon loading exception");
            }

            return SystemIcons.Application;
        }

        private void OpenSettings()
        {
            if (settingsWindow == null || !settingsWindow.IsLoaded)
            {
                settingsWindow = new SettingsWindow();
                settingsWindow.Show();
            }
            else
            {
                settingsWindow.Activate();
            }
        }

        private void ExitApplication()
        {
            SelectionManager.Shutdown();
            GlobalHotkeyManager.Shutdown();
            notifyIcon?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
