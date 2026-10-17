using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YaziciCS.Services;

namespace YaziciCS.UI
{
    public partial class TranslationResultWindow : Window
    {
        private string originalText;
        private string translatedText;
        private string langPair;

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
            this.originalText = originalText ?? "";
            this.translatedText = translatedText ?? "";
            this.langPair = langPair ?? "Çeviri";
            RenderContent();
        }

        /// <summary>
        /// Renders the result into either the rich dictionary view (single word) or
        /// the plain translation TextBox, and updates header / badge / stats.
        /// </summary>
        private void RenderContent()
        {
            bool isDictionaryMode = !string.IsNullOrWhiteSpace(originalText) && !originalText.Trim().Contains(" ");

            if (isDictionaryMode)
            {
                TxtTitle.Text = "📖 Sözlük & Anlam Analizi";

                // Reuse the entry already produced by TranslateTextAsync (no second API call).
                DictionaryEntry? entry = Services.DictionaryService.LastEntry;
                if (entry != null)
                {
                    RtResult.Document = Services.DictionaryService.BuildFlowDocument(entry);
                    RtResult.Visibility = Visibility.Visible;
                    TxtResult.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Fallback: no cached entry — show plain text.
                    TxtResult.Text = translatedText;
                    TxtResult.Visibility = Visibility.Visible;
                    RtResult.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                TxtTitle.Text = "Çeviri Sonucu";
                TxtResult.Text = translatedText;
                TxtResult.Visibility = Visibility.Visible;
                RtResult.Visibility = Visibility.Collapsed;
            }

            TxtLang.Text = langPair;

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

            TxtStats.Text = isDictionaryMode
                ? $"{charCount} karakter • sözlük girişi"
                : $"{charCount} karakter • {wordCount} kelime";

            if (isDictionaryMode && RtResult.Visibility == Visibility.Visible) RtResult.Focus();
            else TxtResult.Focus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RenderContent();
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
