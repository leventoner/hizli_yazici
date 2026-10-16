using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace YaziciCS.UI
{
    public partial class TranslationResultWindow : Window
    {
        private readonly string originalText;
        private readonly string translatedText;
        private readonly string langPair;

        public TranslationResultWindow(string originalText, string translatedText, string langPair)
        {
            InitializeComponent();
            this.originalText = originalText ?? "";
            this.translatedText = translatedText ?? "";
            this.langPair = langPair ?? "Çeviri";
        }

        /// <summary>
        /// Refreshes the window content with a new translation result.
        /// Used when reusing an existing single result window instead of opening a new one.
        /// </summary>
        public void UpdateContent(string originalText, string translatedText, string langPair)
        {
            TxtResult.Text = translatedText ?? "";
            TxtLang.Text = langPair ?? "Çeviri";

            // Single word -> dictionary mode (automatic, no button needed)
            if (!string.IsNullOrWhiteSpace(originalText) && !originalText.Trim().Contains(" "))
            {
                TxtTitle.Text = "📖 Sözlük & Anlam Analizi";
            }
            else
            {
                TxtTitle.Text = "Çeviri Sonucu";
            }

            // Set badge color based on language
            if (langPair.Contains("NL") || langPair.Contains("Flamanca") || langPair.Contains("Hollandaca"))
            {
                LangBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C")); // Dutch Orange
            }
            else if (langPair.Contains("FR") || langPair.Contains("Fransızca"))
            {
                LangBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // French Red
            }
            else
            {
                LangBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")); // English Blue
            }

            int charCount = (translatedText ?? "").Length;
            int wordCount = Regex.Split((translatedText ?? "").Trim(), @"\s+").Length;
            if (string.IsNullOrWhiteSpace(translatedText)) wordCount = 0;

            TxtStats.Text = $"{charCount} karakter • {wordCount} kelime";

            TxtResult.Focus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtResult.Text = translatedText;
            TxtLang.Text = langPair;

            // Check if single word for dictionary mode
            if (!string.IsNullOrWhiteSpace(originalText) && !originalText.Trim().Contains(" "))
            {
                TxtTitle.Text = "📖 Sözlük & Anlam Analizi";
            }
            else
            {
                TxtTitle.Text = "Çeviri Sonucu";
            }

            // Set badge color based on language
            if (langPair.Contains("NL") || langPair.Contains("Flamanca") || langPair.Contains("Hollandaca"))
            {
                LangBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C")); // Dutch Orange
            }
            else if (langPair.Contains("FR") || langPair.Contains("Fransızca"))
            {
                LangBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // French Red
            }
            else
            {
                LangBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")); // English Blue
            }

            int charCount = translatedText.Length;
            int wordCount = Regex.Split(translatedText.Trim(), @"\s+").Length;
            if (string.IsNullOrWhiteSpace(translatedText)) wordCount = 0;

            TxtStats.Text = $"{charCount} karakter • {wordCount} kelime";

            TxtResult.Focus();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(translatedText);
                TxtCopyLabel.Text = "Kopyalandı! ✔";

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    TxtCopyLabel.Text = "Kopyala";
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Panoya kopyalanamadı: " + ex.Message);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
