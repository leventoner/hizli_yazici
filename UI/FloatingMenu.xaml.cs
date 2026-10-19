using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using YaziciCS.Managers;

namespace YaziciCS.UI
{
    public partial class FloatingMenu : Window
    {
        private DispatcherTimer hideTimer;
        public SolidColorBrush ThemeColor { get; set; } = new SolidColorBrush(Colors.RoyalBlue);

        public FloatingMenu()
        {
            InitializeComponent();
            DataContext = this;

            hideTimer = new DispatcherTimer();
            hideTimer.Tick += (s, e) => HideMenu();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTheme();
        }

        public void UpdateTheme()
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SettingsManager.Current.ColorTheme);
                ThemeColor = new SolidColorBrush(color);
            }
            catch
            {
                ThemeColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3d, 0x5a, 0xfe));
            }
        }

        public void ShowAt(double physX, double physY) => ShowAt(physX, physY, physY);

        /// <summary>
        /// Yüzen menüyü ekran ölçeklemesine (DPI Scaling) ve seçimin tüm dikey alanına göre konumlandırır.
        /// physCenterX: İmlecin / seçimin yatay fiziksel piksel merkezi
        /// physTopY: Seçimin fiziksel piksel TEPE noktası (çok satırlı seçimlerde en üst satır)
        /// physBottomY: Seçimin fiziksel piksel ALT noktası (çok satırlı seçimlerde en alt satır)
        /// </summary>
        public void ShowAt(double physCenterX, double physTopY, double physBottomY)
        {
            UpdateTheme();

            // Windows ekran ölçekleme faktörünü (100%, 125%, 150%, 200%) al
            var dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            // Fiziksel piksel koordinatlarını WPF DIP (Device Independent Pixels) birimine dönüştür
            double dipCenterX = physCenterX / scaleX;
            double dipTopY = physTopY / scaleY;
            double dipBottomY = physBottomY / scaleY;

            var workArea = SystemParameters.WorkArea;
            double menuWidth = ActualWidth > 0 ? ActualWidth : (Width > 0 ? Width : 268);
            double menuHeight = ActualHeight > 0 ? ActualHeight : (Height > 0 ? Height : 48);

            // Yatay ortalama ve çalışma alanı sınırları
            double targetLeft = dipCenterX - (menuWidth / 2);
            if (targetLeft < workArea.Left + 15) targetLeft = workArea.Left + 15;
            if (targetLeft + menuWidth > workArea.Right - 15) targetLeft = workArea.Right - menuWidth - 15;
            Left = targetLeft;

            // Dikey konumlandırma:
            // Menü kesinlikle seçili metnin üzerine veya imlecin dibine binmeyecek şekilde:
            // 1. Tercihen seçimin TEPE noktasından 35 DIP yukarıda aç.
            // 2. Ekranın en üstünde yer yoksa seçimin EN ALT noktasından 35 DIP aşağıda aç.
            const double safeOffset = 35;
            double targetTop = dipTopY - menuHeight - safeOffset;

            if (targetTop < workArea.Top + 10)
            {
                // Yukarıda yer yok: seçimin altından güvenli mesafede aç
                targetTop = dipBottomY + safeOffset;
                if (targetTop + menuHeight > workArea.Bottom - 10)
                {
                    targetTop = Math.Max(workArea.Top + 10, workArea.Bottom - menuHeight - 15);
                }
            }
            Top = targetTop;

            hideTimer.Stop();
            hideTimer.Interval = TimeSpan.FromSeconds(SettingsManager.Current.FloatingMenuDuration);
            hideTimer.Start();

            Show();
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, anim);
        }

        public void HideMenu(bool actionExecuted = false)
        {
            hideTimer.Stop();
            if (!actionExecuted)
            {
                SelectionManager.OnMenuDismissed();
            }
            var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(100));
            anim.Completed += (s, e) => Hide();
            BeginAnimation(OpacityProperty, anim);
        }

        private async void BtnFix_Click(object sender, RoutedEventArgs e)
        {
            SelectionManager.NotifyActionExecuted();
            HideMenu(actionExecuted: true);
            await ActionHandler.HandleFixClipboardAsync(triggerCopy: false);
        }

        private async void BtnImprove_Click(object sender, RoutedEventArgs e)
        {
            SelectionManager.NotifyActionExecuted();
            HideMenu(actionExecuted: true);
            await ActionHandler.HandleImproveClipboardAsync(triggerCopy: false);
        }

        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            SelectionManager.NotifyActionExecuted();
            HideMenu(actionExecuted: true);
            await ActionHandler.HandleTranslateClipboardAsync(triggerCopy: false);
        }

        private async void BtnTranslateFr_Click(object sender, RoutedEventArgs e)
        {
            SelectionManager.NotifyActionExecuted();
            HideMenu(actionExecuted: true);
            await ActionHandler.HandleTranslateFrenchClipboardAsync(triggerCopy: false);
        }

        private async void BtnTranslateNl_Click(object sender, RoutedEventArgs e)
        {
            SelectionManager.NotifyActionExecuted();
            HideMenu(actionExecuted: true);
            await ActionHandler.HandleTranslateDutchClipboardAsync(triggerCopy: false);
        }

        private async void BtnVoice_Click(object sender, RoutedEventArgs e)
        {
            SelectionManager.NotifyActionExecuted();
            HideMenu(actionExecuted: true);
            await ActionHandler.HandleSpeechToTextAsync();
        }

        private async void BtnAudio_Click(object sender, RoutedEventArgs e) => BtnVoice_Click(sender, e);
    }
}
