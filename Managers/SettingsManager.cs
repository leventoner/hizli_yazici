using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YaziciCS.Managers
{
    public class AppSettings
    {
        [JsonPropertyName("program_name")]
        public string ProgramName { get; set; } = "Yazıcı";

        [JsonPropertyName("hotkey")]
        public string Hotkey { get; set; } = "ctrl+c";

        [JsonPropertyName("stt_hotkey")]
        public string SttHotkey { get; set; } = "ctrl+shift+y";

        [JsonPropertyName("stt_duration")]
        public int SttDuration { get; set; } = 10;

        [JsonPropertyName("cooldown")]
        public double Cooldown { get; set; } = 0.5;

        [JsonPropertyName("notify_on_no_change")]
        public bool NotifyOnNoChange { get; set; } = true;

        [JsonPropertyName("enable_character_fix")]
        public bool EnableCharacterFix { get; set; } = true;

        [JsonPropertyName("enable_ai_improve")]
        public bool EnableAiImprove { get; set; } = true;

        [JsonPropertyName("enable_speech_to_text")]
        public bool EnableSpeechToText { get; set; } = true;

        [JsonPropertyName("color_theme")]
        public string ColorTheme { get; set; } = "#3d5afe";

        [JsonPropertyName("floating_menu_duration")]
        public int FloatingMenuDuration { get; set; } = 10;
        
        [JsonIgnore]
        public string GeminiApiKey { get; set; } = "";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static readonly string EnvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        
        public static AppSettings Current { get; set; } = new AppSettings();

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Save(); // Create default
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                Current = new AppSettings();
            }

            LoadEnv();
        }

        private static void LoadEnv()
        {
            try
            {
                // Priority list of directories to search
                string[] searchDirs = {
                    AppDomain.CurrentDomain.BaseDirectory,
                    Directory.GetCurrentDirectory(),
                    Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)
                };

                string envFile = "";
                foreach (var dir in searchDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    
                    string currentDir = dir;
                    for (int i = 0; i < 4; i++)
                    {
                        string testPath = Path.Combine(currentDir, ".env");
                        if (File.Exists(testPath))
                        {
                            envFile = testPath;
                            break;
                        }
                        var parent = Directory.GetParent(currentDir);
                        if (parent == null) break;
                        currentDir = parent.FullName;
                    }
                    if (!string.IsNullOrEmpty(envFile)) break;
                }

                if (!string.IsNullOrEmpty(envFile))
                {
                    foreach (var line in File.ReadAllLines(envFile))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("GEMINI_API_KEY="))
                        {
                            string key = trimmed.Substring("GEMINI_API_KEY=".Length).Trim();
                            // Strip quotes if present
                            if (key.StartsWith("\"") && key.EndsWith("\"")) key = key.Substring(1, key.Length - 2);
                            if (key.StartsWith("'") && key.EndsWith("'")) key = key.Substring(1, key.Length - 2);
                            
                            Current.GeminiApiKey = key;
                        }
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
