using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using YaziciCS.Managers;
using YaziciCS.Services;

namespace YaziciCS.UI
{
    public partial class DictionaryWindow : Window
    {
        private bool isInitializing = true;
        private DispatcherTimer? copyResetTimer;

        public DictionaryWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSavedLanguages();
            isInitializing = false;
            TxtSearch.Focus();
        }

        private void LoadSavedLanguages()
        {
            string savedSource = (SettingsManager.Current.DictionarySourceLang ?? "tr").ToLowerInvariant();
            string savedTarget = (SettingsManager.Current.DictionaryTargetLang ?? "en").ToLowerInvariant();

            SelectComboItem(CmbSourceLang, savedSource);
            SelectComboItem(CmbTargetLang, savedTarget);
        }

        private static void SelectComboItem(ComboBox combo, string langCode)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is string tag && tag.Equals(langCode, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private string GetSelectedLangCode(ComboBox combo, string defaultVal)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return tag;
            }
            return defaultVal;
        }

        private void CmbLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            string source = GetSelectedLangCode(CmbSourceLang, "tr");
            string target = GetSelectedLangCode(CmbTargetLang, "en");

            // Tercihleri anında kaydet (hatırlansın)
            SettingsManager.Current.DictionarySourceLang = source;
            SettingsManager.Current.DictionaryTargetLang = target;
            SettingsManager.Save();
        }

        private void BtnSwapLang_Click(object sender, RoutedEventArgs e)
        {
            string source = GetSelectedLangCode(CmbSourceLang, "tr");
            string target = GetSelectedLangCode(CmbTargetLang, "en");

            isInitializing = true;
            SelectComboItem(CmbSourceLang, target);
            SelectComboItem(CmbTargetLang, source);
            isInitializing = false;

            SettingsManager.Current.DictionarySourceLang = target;
            SettingsManager.Current.DictionaryTargetLang = source;
            SettingsManager.Save();

            // Eğer arama kutusunda halihazırda kelime varsa ters yönde otomatik ara
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                PerformSearch();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtPlaceholder != null)
            {
                TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                PerformSearch();
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private async void PerformSearch()
        {
            string query = (TxtSearch.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                TxtResult.Text = "Lütfen sözlükte aramak istediğiniz bir kelime veya cümle yazın.";
                TxtStats.Text = "";
                return;
            }

            string sourceLang = GetSelectedLangCode(CmbSourceLang, "tr");
            string targetLang = GetSelectedLangCode(CmbTargetLang, "en");

            if (sourceLang.Equals(targetLang, StringComparison.OrdinalIgnoreCase))
            {
                TxtResult.Text = "Kaynak ve hedef diller aynı seçildi. Lütfen farklı diller seçin.";
                return;
            }

            PnlLoading.Visibility = Visibility.Visible;
            ScrResult.Visibility = Visibility.Collapsed;
            BtnSearch.IsEnabled = false;

            try
            {
                string result = await FreeTranslationService.TranslateDirectAsync(query, sourceLang, targetLang);

                ScrResult.Visibility = Visibility.Visible;
                PnlLoading.Visibility = Visibility.Collapsed;

                if (string.IsNullOrWhiteSpace(result))
                {
                    TxtResult.Text = "Eşleşen anlam veya çeviri bulunamadı.";
                    TxtStats.Text = "";
                }
                else
                {
                    TxtResult.Text = result;
                    int charCount = result.Length;
                    int wordCount = Regex.Split(result.Trim(), @"\s+").Length;
                    TxtStats.Text = $"{charCount} karakter • {wordCount} kelime";
                }
            }
            catch (Exception ex)
            {
                ScrResult.Visibility = Visibility.Visible;
                PnlLoading.Visibility = Visibility.Collapsed;
                TxtResult.Text = $"Hata oluştu: {ex.Message}";
                TxtStats.Text = "";
            }
            finally
            {
                BtnSearch.IsEnabled = true;
            }
        }

        private async void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            string textToCopy = TxtResult.Text;
            if (string.IsNullOrWhiteSpace(textToCopy)) return;

            await TextService.SetClipboardTextAsync(textToCopy);

            TxtCopyLabel.Text = "Kopyalandı! ✓";

            copyResetTimer?.Stop();
            copyResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            copyResetTimer.Tick += (s, args) =>
            {
                TxtCopyLabel.Text = "Kopyala";
                copyResetTimer.Stop();
            };
            copyResetTimer.Start();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
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
