using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace YaziciCS.UI
{
    public partial class NotificationWindow : Window, INotifyPropertyChanged
    {
        private string _titleText;
        public string TitleText
        {
            get => _titleText;
            set { _titleText = value; OnPropertyChanged(nameof(TitleText)); }
        }

        private string _messageText;
        public string MessageText
        {
            get => _messageText;
            set { _messageText = value; OnPropertyChanged(nameof(MessageText)); }
        }

        private System.Windows.Media.Brush _themeColor = System.Windows.Media.Brushes.White;
        public System.Windows.Media.Brush ThemeColor
        {
            get => _themeColor;
            set { _themeColor = value; OnPropertyChanged(nameof(ThemeColor)); }
        }

        public NotificationWindow(string title, string message, string hexColor)
        {
            InitializeComponent();
            DataContext = this;

            TitleText = title;
            MessageText = message.Length > 120 ? message.Substring(0, 117) + "..." : message;

            try { ThemeColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)); } catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position Bottom Right
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - this.ActualWidth - 20;
            this.Top = workArea.Bottom - this.ActualHeight - 20;

            var anim = new DoubleAnimation(0, 0.95, TimeSpan.FromMilliseconds(300));
            this.BeginAnimation(Window.OpacityProperty, anim);

            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(0.95, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s2, args2) => this.Close();
                this.BeginAnimation(Window.OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
