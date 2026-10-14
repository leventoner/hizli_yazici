using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace YaziciCS.UI
{
    public partial class NotificationWindow : Window
    {
        private readonly string title;
        private readonly string message;
        private readonly string colorHex;

        public NotificationWindow(string title, string message, string colorHex)
        {
            InitializeComponent();
            this.title = title;
            this.message = message;
            this.colorHex = colorHex;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtTitle.Text = title;
            TxtMessage.Text = message;

            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                MainBorder.BorderBrush = new SolidColorBrush(color);
                IconBadge.Background = new SolidColorBrush(color) { Opacity = 0.2 };
            }
            catch { }

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 15;
            Top = workArea.Bottom - Height - 15;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                anim.Completed += (a, b) => Close();
                BeginAnimation(OpacityProperty, anim);
            };
            timer.Start();
        }
    }
}
