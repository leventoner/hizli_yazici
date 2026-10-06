using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace YaziciCS.UI
{
    public partial class FloatingMenu : Window, INotifyPropertyChanged
    {
        private DispatcherTimer _closeTimer;
        public Action<string> OnActionSelected { get; set; }
        
        private System.Windows.Media.Brush _themeColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1a237e"));
        public System.Windows.Media.Brush ThemeColor
        {
            get => _themeColor;
            set { _themeColor = value; OnPropertyChanged(nameof(ThemeColor)); }
        }

        public FloatingMenu(System.Windows.Point position, string hexColor, int durationSeconds)
        {
            InitializeComponent();
            DataContext = this;

            try { ThemeColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)); } catch { }

            this.Left = position.X;
            this.Top = position.Y + 20;

            // Force visibility settings for diagnostics
            this.Opacity = 1.0;
            this.ShowInTaskbar = false;
            this.Topmost = true;

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            _closeTimer.Tick += CloseTimer_Tick;
            _closeTimer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            this.BeginAnimation(Window.OpacityProperty, anim);
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            if (this.IsMouseOver)
            {
                // Delay if mouse is over
                _closeTimer.Stop();
                _closeTimer.Interval = TimeSpan.FromSeconds(2);
                _closeTimer.Start();
            }
            else
            {
                _closeTimer.Stop();
                CloseMenu();
            }
        }

        public void CloseMenu()
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            anim.Completed += (s, e) => this.Close();
            this.BeginAnimation(Window.OpacityProperty, anim);
        }

        private void BtnFix_Click(object sender, RoutedEventArgs e) => TriggerAction("fix");
        private void BtnImprove_Click(object sender, RoutedEventArgs e) => TriggerAction("improve");
        private void BtnTranslate_Click(object sender, RoutedEventArgs e) => TriggerAction("translate");
        private void BtnTranslateFr_Click(object sender, RoutedEventArgs e) => TriggerAction("translate_fr");

        private void TriggerAction(string action)
        {
            _closeTimer?.Stop();
            this.Hide();
            OnActionSelected?.Invoke(action);
            this.Close();
        }



        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
