using System.Windows;
using YaziciCS.Managers;

namespace YaziciCS.UI
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Current { get; set; }

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Create a copy to bind
            Current = new AppSettings
            {
                ProgramName = SettingsManager.Current.ProgramName,
                Hotkey = SettingsManager.Current.Hotkey,
                SttHotkey = SettingsManager.Current.SttHotkey,
                SttDuration = SettingsManager.Current.SttDuration,
                Cooldown = SettingsManager.Current.Cooldown,
                NotifyOnNoChange = SettingsManager.Current.NotifyOnNoChange,
                EnableCharacterFix = SettingsManager.Current.EnableCharacterFix,
                EnableAiImprove = SettingsManager.Current.EnableAiImprove,
                EnableSpeechToText = SettingsManager.Current.EnableSpeechToText,
                ColorTheme = SettingsManager.Current.ColorTheme,
                FloatingMenuDuration = SettingsManager.Current.FloatingMenuDuration,
                TranslationProvider = SettingsManager.Current.TranslationProvider,
                TranslationEmail = SettingsManager.Current.TranslationEmail
            };
            
            this.DataContext = this;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ProgramName = Current.ProgramName;
            SettingsManager.Current.Hotkey = Current.Hotkey;
            SettingsManager.Current.SttHotkey = Current.SttHotkey;
            SettingsManager.Current.SttDuration = Current.SttDuration;
            SettingsManager.Current.Cooldown = Current.Cooldown;
            SettingsManager.Current.NotifyOnNoChange = Current.NotifyOnNoChange;
            SettingsManager.Current.EnableCharacterFix = Current.EnableCharacterFix;
            SettingsManager.Current.EnableAiImprove = Current.EnableAiImprove;
            SettingsManager.Current.EnableSpeechToText = Current.EnableSpeechToText;
            SettingsManager.Current.ColorTheme = Current.ColorTheme;
            SettingsManager.Current.FloatingMenuDuration = Current.FloatingMenuDuration;
            SettingsManager.Current.TranslationProvider = Current.TranslationProvider;
            SettingsManager.Current.TranslationEmail = Current.TranslationEmail;

            SettingsManager.Save();
            
            // Optionally close and ask for restart
            System.Windows.MessageBox.Show("Ayarlar kaydedildi. Kısayol değişikliklerinin uygulanması için uygulamayı yeniden başlatmanız gerekebilir.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            
            this.Close();
        }
    }
}
