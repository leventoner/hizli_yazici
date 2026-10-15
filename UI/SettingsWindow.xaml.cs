using System.Windows;
using YaziciCS.Managers;

namespace YaziciCS.UI
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Current { get; set; }

        public SettingsWindow()
        {
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
                TranslationEmail = SettingsManager.Current.TranslationEmail,
                SttProvider = SettingsManager.Current.SttProvider,
                GeminiApiKey = SettingsManager.Current.GeminiApiKey
            };

            DataContext = this;
            InitializeComponent();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current = Current;
            SettingsManager.Save();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
