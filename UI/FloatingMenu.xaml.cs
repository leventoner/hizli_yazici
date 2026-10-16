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

        public void ShowAt(double x, double y)
        {
            UpdateTheme();
            Left = Math.Max(10, x - (Width / 2));
            Top = Math.Max(10, y - Height - 10);

            hideTimer.Stop();
            hideTimer.Interval = TimeSpan.FromSeconds(SettingsManager.Current.FloatingMenuDuration);
            hideTimer.Start();

            Show();
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, anim);
        }

        public void HideMenu()
        {
            hideTimer.Stop();
            var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150));
            anim.Completed += (s, e) => Hide();
            BeginAnimation(OpacityProperty, anim);
        }

        private async void BtnFix_Click(object sender, RoutedEventArgs e)
        {
            HideMenu();
            await ActionHandler.HandleFixClipboardAsync(triggerCopy: false);
        }

        private async void BtnImprove_Click(object sender, RoutedEventArgs e)
        {
            HideMenu();
            await ActionHandler.HandleImproveClipboardAsync(triggerCopy: false);
        }

        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            HideMenu();
            await ActionHandler.HandleTranslateClipboardAsync(triggerCopy: false);
        }

        private async void BtnTranslateFr_Click(object sender, RoutedEventArgs e)
        {
            HideMenu();
            await ActionHandler.HandleTranslateFrenchClipboardAsync(triggerCopy: false);
        }

        private async void BtnTranslateNl_Click(object sender, RoutedEventArgs e)
        {
            HideMenu();
            await ActionHandler.HandleTranslateDutchClipboardAsync(triggerCopy: false);
        }

        private async void BtnVoice_Click(object sender, RoutedEventArgs e)
        {
            HideMenu();
            await ActionHandler.HandleSpeechToTextAsync();
        }
    }
}
