using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using YaziciCS.Managers;
using YaziciCS.UI;
using YaziciCS.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace YaziciCS
{
    public partial class App : Application
    {
        private NotifyIcon _notifyIcon;
        private GlobalHotkeyManager _hotkeyManager;
        private SelectionManager _selectionManager;
        private FloatingMenu _activeMenu;
        private Window _hiddenWindow;
        private GlobalKeyboardHook _keyboardHook;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Beklenmedik kapanmalarin izini surmek icin oturum baslangicini logla
            // ve cozulemeyen istisnalari (UI thread disinda da) yakala.
            CrashLogger.LogSessionStart();

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                CrashLogger.Log("AppDomain.UnhandledException", args.ExceptionObject as Exception);
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                CrashLogger.Log("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };

            // Pencilere dayalı kapanma modu: son pencere kapanınca uygulama KAPANMAZ,
            // sistem tepsisinde çalışmaya devam eder. Shutdown() çağrılana kadar yaşar.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Global error handling
            DispatcherUnhandledException += (s, args) => 
            {
                CrashLogger.Log("DispatcherUnhandledException", args.Exception);
                ActionHandler.ShowNotification?.Invoke("Hata", args.Exception.Message, "#e74c3c");
                args.Handled = true;
            };

            SettingsManager.Load();

            _hiddenWindow = new Window
            {
                Width = 0, Height = 0, WindowStyle = WindowStyle.None,
                ShowInTaskbar = false, ShowActivated = false
            };
            // Gizli pencere kapanırsa uygulama kapanmasın diye referansı koruyoruz.
            _hiddenWindow.Closing += (s, args) =>
            {
                if (ShutdownMode != ShutdownMode.OnExplicitShutdown)
                {
                    // Beklenmedik bir Close çağrısı (ör. kullanıcı X'e bastı) -> engelle.
                    CrashLogger.Log("HiddenWindow.Close engellendi", null);
                    args.Cancel = true;
                }
            };
            _hiddenWindow.Show(); _hiddenWindow.Hide();

            _hotkeyManager = new GlobalHotkeyManager();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_hiddenWindow).Handle;
            _hotkeyManager.Initialize(hwnd);
            
            try
            {
                // We DON'T register Ctrl+C here because it swallows the key.
                // We ONLY register the STT hotkey (like ctrl+shift+y)
                _hotkeyManager.Register(SettingsManager.Current.SttHotkey, 2);
            }
            catch { }

            _hotkeyManager.OnSttHotkeyPressed = async () => await ActionHandler.HandleSpeechToTextAsync();

            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.OnKeyPressed += (key) => 
            {
                // Detect Ctrl+C
                if (key == System.Windows.Input.Key.C)
                {
                    if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                    {
                        // Dikkat: Hook callback icinde dispatcher'a senkron (Invoke) beklemek,
                        // UI megulsa tum sistemi (biz dahil) kilitler ve Windows hook'u
                        // timeout ile dusurur. Bu yuzden fire-and-forget (BeginInvoke) kullan.
                        Dispatcher.BeginInvoke(new Action(async () => await RunMultipleClickLogic()));
                    }
                }
            };

            _selectionManager = new SelectionManager();
            // Hook callback thread'inden dispatcher'a senkron Invoke yerine BeginInvoke
            // kullan: hook callback'ini bloklamak sistem genelinde klavye/fare
            // gecikmesine ve hook'un Windows tarafindan dusurulmesine yol acar.
            _selectionManager.OnTextSelected = (pos, selectedText) => Dispatcher.BeginInvoke(new Action(() => ShowFloatingMenu(pos, selectedText)));
            _selectionManager.OnClickOutside = () => Dispatcher.BeginInvoke(new Action(CloseActiveMenu));
            _selectionManager.IsClickOnMenu = IsClickOnMenu;

            ActionHandler.ShowNotification = (title, msg, color) => Dispatcher.Invoke(() =>
            {
                var notif = new NotificationWindow(title, msg, color);
                notif.Show();
            });

            SetupTrayIcon();
            
            if (string.IsNullOrEmpty(SettingsManager.Current.GeminiApiKey))
            {
                ActionHandler.ShowNotification(SettingsManager.Current.ProgramName, "Hata: API anahtarı yüklenemedi! .env dosyasını ana dizine koyun.", "#e74c3c");
            }
            else
            {
                ActionHandler.ShowNotification(SettingsManager.Current.ProgramName, "Yazıcı Başlatıldı ve API anahtarı yüklendi!", SettingsManager.Current.ColorTheme);
            }
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Information;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Information;
            }
            _notifyIcon.Text = SettingsManager.Current.ProgramName;
            _notifyIcon.Visible = true;

            var menu = new ContextMenuStrip();
            
            var miFix = new ToolStripMenuItem("Karakter Düzeltme Aktif");
            miFix.Checked = SettingsManager.Current.EnableCharacterFix;
            miFix.Click += (s, e) => { SettingsManager.Current.EnableCharacterFix = !SettingsManager.Current.EnableCharacterFix; miFix.Checked = SettingsManager.Current.EnableCharacterFix; SettingsManager.Save(); };
            
            var miAi = new ToolStripMenuItem("Yapay Zeka (AI) Desteği Aktif");
            miAi.Checked = SettingsManager.Current.EnableAiImprove;
            miAi.Click += (s, e) => { SettingsManager.Current.EnableAiImprove = !SettingsManager.Current.EnableAiImprove; miAi.Checked = SettingsManager.Current.EnableAiImprove; SettingsManager.Save(); };
            
            var miStt = new ToolStripMenuItem("Sesle Yazma (STT) Aktif");
            miStt.Checked = SettingsManager.Current.EnableSpeechToText;
            miStt.Click += (s, e) => { SettingsManager.Current.EnableSpeechToText = !SettingsManager.Current.EnableSpeechToText; miStt.Checked = SettingsManager.Current.EnableSpeechToText; SettingsManager.Save(); };
            
            var miTestMenu = new ToolStripMenuItem("Test Menü (Hata Kontrol)", null, (s, e) => ShowFloatingMenu(new System.Windows.Point(100, 100)));
            var miSettings = new ToolStripMenuItem("Ayarlar", null, (s, e) => ShowSettings());
            var miExit = new ToolStripMenuItem("Çıkış", null, (s, e) => Shutdown());

            menu.Items.Add(miFix);
            menu.Items.Add(miAi);
            menu.Items.Add(miStt);
            menu.Items.Add(miTestMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miSettings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void ShowSettings()
        {
            var sw = new SettingsWindow();
            sw.Show();
        }

        private DateTime _lastClickTime = DateTime.MinValue;
        private int _clickCount = 0;

        private async Task RunMultipleClickLogic()
        {
            CloseActiveMenu();

            var now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < (SettingsManager.Current.Cooldown * 1000))
            {
                _clickCount++;
            }
            else
            {
                _clickCount = 1;
            }
            _lastClickTime = now;

            // Simple debounce using Task.Delay
            int currentClicks = _clickCount;
            await Task.Delay((int)(SettingsManager.Current.Cooldown * 1000) + 50);

            if (_clickCount == currentClicks)
            {
                // Execute
                if (currentClicks == 2)
                {
                    if (SettingsManager.Current.EnableCharacterFix) await ActionHandler.HandleFixClipboardAsync();
                }
                else if (currentClicks >= 3)
                {
                    if (SettingsManager.Current.EnableAiImprove) await ActionHandler.HandleImproveClipboardAsync();
                }
                _clickCount = 0;
            }
        }

        private void ShowFloatingMenu(System.Windows.Point pos, string selectedText = null)
        {
            CloseActiveMenu();
            
            // Adjust position slightly and ensure it stays on screen
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            
            double x = pos.X;
            double y = pos.Y + 10;
            
            if (x + 190 > screenWidth) x = screenWidth - 200;
            if (y + 60 > screenHeight) y = pos.Y - 60;
            if (x < 0) x = 10;
            if (y < 0) y = 10;

            _activeMenu = new FloatingMenu(new System.Windows.Point(x, y), SettingsManager.Current.ColorTheme, SettingsManager.Current.FloatingMenuDuration);
            _activeMenu.OnActionSelected = async (action) =>
            {
                // Menüyü açtığımız anda seçili metni (UIAutomation ile) okumuştuk.
                // Pano üzerinden değil, doğrudan bu metni kullanarak işlem yap.
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    await TextService.SetClipboardTextAsync(selectedText);
                }

                if (action == "fix") await ActionHandler.HandleFixClipboardAsync();
                else if (action == "improve" || action == "improve_tr" || action == "improve_auto") await ActionHandler.HandleImproveClipboardAsync();
                else if (action == "translate" || action == "translate_en") await ActionHandler.HandleTranslateClipboardAsync();
                else if (action == "translate_fr") await ActionHandler.HandleTranslateFrenchClipboardAsync();
            };
            _activeMenu.Show();
        }

        private void CloseActiveMenu()
        {
            if (_activeMenu != null)
            {
                _activeMenu.CloseMenu();
                _activeMenu = null;
            }
        }

        private bool IsClickOnMenu(System.Windows.Point p)
        {
            if (_activeMenu == null || !_activeMenu.IsLoaded) return false;
            
            try
            {
                // PointToScreen returns coordinates in screen pixels, which matches the mouse hook coordinates.
                var screenPos = _activeMenu.PointToScreen(new System.Windows.Point(0, 0));
                var screenBottomRight = _activeMenu.PointToScreen(new System.Windows.Point(_activeMenu.ActualWidth, _activeMenu.ActualHeight));
                
                double padding = 15;
                bool xMatch = p.X >= (screenPos.X - padding) && p.X <= (screenBottomRight.X + padding);
                bool yMatch = p.Y >= (screenPos.Y - padding) && p.Y <= (screenBottomRight.Y + padding);
                return xMatch && yMatch;
            }
            catch
            {
                return false;
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // Neden kapandığını logla (beklenmedik kapanmaları tespit için).
            CrashLogger.Log("Application_Exit çagrildi", null);

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _keyboardHook?.Dispose();
            _hotkeyManager?.Dispose();
        }
    }
}
