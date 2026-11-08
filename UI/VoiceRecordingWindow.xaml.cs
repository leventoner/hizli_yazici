using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YaziciCS.Managers;
using YaziciCS.Services;

namespace YaziciCS.UI
{
    public partial class VoiceRecordingWindow : Window
    {
        private DispatcherTimer _timer;
        private int _elapsedSeconds = 0;
        private Task<bool>? _recordingTask;
        private string _tempFilePath = "";
        private readonly Brush _idleBarBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        private readonly Brush _activeBarBrush;
        private readonly Random _rand = new Random();

        // 20 frekans çubuğu için Gaussian çan eğrisi çarpanları (merkez daha yüksek)
        private static readonly float[] BarWeights = new float[]
        {
            0.25f, 0.35f, 0.50f, 0.65f, 0.80f, 0.92f, 1.0f, 1.15f, 1.25f, 1.30f,
            1.30f, 1.25f, 1.15f, 1.0f, 0.92f, 0.80f, 0.65f, 0.50f, 0.35f, 0.25f
        };

        public VoiceRecordingWindow()
        {
            InitializeComponent();

            // Neon zümrüt/turkuaz dalga degrade fırçası
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(0, 0)
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x10, 0xB9, 0x81), 0.0));
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x06, 0xB6, 0xD4), 1.0));
            _activeBarBrush = gradient;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AudioService.AudioLevelChanged += OnAudioLevelChanged;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            AudioService.AudioLevelChanged -= OnAudioLevelChanged;
            _timer.Stop();
            if (AudioService.IsRecording)
            {
                AudioService.StopRecording();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (AudioService.IsRecording)
            {
                AudioService.StopRecording();
            }
            Close();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _elapsedSeconds++;
            int mins = _elapsedSeconds / 60;
            int secs = _elapsedSeconds % 60;
            TxtTimer.Text = $"{mins:D2}:{secs:D2}";
        }

        private void OnAudioLevelChanged(float level)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnAudioLevelChanged(level)));
                return;
            }

            if (!AudioService.IsRecording || WaveformPanel == null)
            {
                ResetWaveformBars();
                return;
            }

            int count = Math.Min(WaveformPanel.Children.Count, BarWeights.Length);
            for (int i = 0; i < count; i++)
            {
                if (WaveformPanel.Children[i] is Border bar)
                {
                    float weight = BarWeights[i];
                    // Küçük rastgele canlılık varyasyonu
                    float jitter = 0.85f + (float)_rand.NextDouble() * 0.3f;
                    float targetHeight = 6f + (level * 36f * weight * jitter);
                    if (targetHeight > 38f) targetHeight = 38f;
                    if (targetHeight < 6f) targetHeight = 6f;

                    bar.Height = targetHeight;
                    bar.Background = (level > 0.03f) ? _activeBarBrush : _idleBarBrush;
                }
            }
        }

        private void ResetWaveformBars()
        {
            if (WaveformPanel == null) return;
            for (int i = 0; i < WaveformPanel.Children.Count; i++)
            {
                if (WaveformPanel.Children[i] is Border bar)
                {
                    bar.Height = 6;
                    bar.Background = _idleBarBrush;
                }
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (AudioService.IsRecording) return;

            _elapsedSeconds = 0;
            TxtTimer.Text = "00:00";
            _timer.Start();

            BtnStart.Visibility = Visibility.Collapsed;
            BtnStop.Visibility = Visibility.Visible;
            BtnStop.IsEnabled = true;

            TxtStatus.Text = "Kaydediliyor... Konuşun";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Canlı Kırmızı
            ResultBorder.Visibility = Visibility.Collapsed;
            Height = 300;

            _tempFilePath = Path.Combine(Path.GetTempPath(), $"yazici_voice_{Guid.NewGuid():N}.wav");
            _recordingTask = AudioService.RecordAudioAsync(_tempFilePath, 600); // 10 dakikaya kadar manuel kayıt
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (!AudioService.IsRecording && _recordingTask == null) return;

            _timer.Stop();
            BtnStop.IsEnabled = false;

            TxtStatus.Text = "Metne Çevriliyor... (Lokal AI)";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Mavi
            ResetWaveformBars();

            // Kaydı durdur
            AudioService.StopRecording();

            bool recorded = false;
            try
            {
                if (_recordingTask != null)
                {
                    recorded = await _recordingTask;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recording task await error: {ex.Message}");
            }

            _recordingTask = null;

            if (!recorded || !File.Exists(_tempFilePath))
            {
                TxtStatus.Text = "Ses algılanamadı (Kayıt çok kısa).";
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                ResetControls();
                return;
            }

            // Metne çevir
            string transcribed = await AudioService.TranscribeAudioAsync(_tempFilePath);

            // Geçici dosyayı temizle
            try
            {
                if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath);
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(transcribed) &&
                !transcribed.StartsWith("Hata") &&
                !transcribed.StartsWith("Ses algılanamadı") &&
                !transcribed.StartsWith("Ses anlaşılamadı"))
            {
                // Panoya otomatik kopyala
                await TextService.SetClipboardTextAsync(transcribed);

                TxtResult.Text = transcribed;
                ResultBorder.Visibility = Visibility.Visible;
                Height = 360;

                TxtStatus.Text = "Tamamlandı (Panoya Kopyalandı)";
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Yeşil
                ActionHandler.ShowNotification?.Invoke("Panoya Kopyalandı!", transcribed, "#2ecc71");
            }
            else
            {
                TxtStatus.Text = string.IsNullOrWhiteSpace(transcribed) ? "Ses anlaşılamadı." : transcribed;
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }

            ResetControls();
        }

        private void ResetControls()
        {
            BtnStart.Visibility = Visibility.Visible;
            BtnStop.Visibility = Visibility.Collapsed;
            BtnStop.IsEnabled = true;
            ResetWaveformBars();
        }

        private async void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtResult.Text))
            {
                await TextService.SetClipboardTextAsync(TxtResult.Text);
                ActionHandler.ShowNotification?.Invoke("Panoya Kopyalandı!", TxtResult.Text, "#2ecc71");
            }
        }
    }
}
