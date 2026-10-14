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
            
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                notifyIcon.Icon = SystemIcons.Application;
            }

            notifyIcon.Text = SettingsManager.Current.ProgramName;
            notifyIcon.Visible = true;

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("⚙️ Ayarlar", null, (s, e) => OpenSettings());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("❌ Çıkış", null, (s, e) => ExitApplication());

            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += (s, e) => OpenSettings();
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
