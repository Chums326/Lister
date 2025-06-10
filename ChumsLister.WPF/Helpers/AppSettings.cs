using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ChumsLister.WPF.Helpers
{
    public static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChumsLister",
            "settings.json");
        private static readonly object _lock = new object();
        private static Settings _settings;

        static AppSettings()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                // Load settings if file exists
                if (File.Exists(SettingsPath))
                {
                    Debug.WriteLine($"Loading settings from: {SettingsPath}");
                    try
                    {
                        string json = File.ReadAllText(SettingsPath);
                        _settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                        Debug.WriteLine($"Loaded settings: Username={_settings.Username}, HasToken={!string.IsNullOrEmpty(_settings.RememberMeToken)}, DarkMode={_settings.DarkMode}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading settings: {ex.Message}");
                        _settings = new Settings();
                    }
                }
                else
                {
                    Debug.WriteLine("Settings file not found, creating new settings");
                    _settings = new Settings();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing AppSettings: {ex.Message}");
                _settings = new Settings();
            }
        }

        /// <summary>
        /// Clears saved login credentials (both token and username)
        /// </summary>
        public static void ClearSavedCredentials()
        {
            _settings.RememberMeToken = string.Empty;
            _settings.Username = string.Empty; // Clear username too
            Save();
            Debug.WriteLine("Saved credentials cleared completely");
        }

        public static string RememberMeToken
        {
            get => _settings.RememberMeToken;
            set
            {
                _settings.RememberMeToken = value;
                Save();
            }
        }

        public static string Username
        {
            get => _settings.Username;
            set
            {
                _settings.Username = value;
                Save();
            }
        }

        public static bool DarkMode
        {
            get => _settings.DarkMode;
            set
            {
                if (_settings.DarkMode != value)
                {
                    _settings.DarkMode = value;
                    Save();
                    Debug.WriteLine($"Dark mode setting changed to: {value}");
                }
            }
        }

        private static void Save()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(SettingsPath, json);
                    Debug.WriteLine($"Saved user settings: DarkMode={_settings.DarkMode}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving settings: {ex.Message}");
                }
            }
        }

        public class Settings
        {
            public string RememberMeToken { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public bool DarkMode { get; set; } = false;
        }

        // Sync method to ensure Core and AppSettings are aligned
        public static void SyncWithCoreSettings(ChumsLister.Core.Models.UserSettings coreSettings)
        {
            if (coreSettings != null)
            {
                if (_settings.DarkMode != coreSettings.UseDarkMode)
                {
                    Debug.WriteLine($"Syncing dark mode setting from core: {coreSettings.UseDarkMode}");
                    _settings.DarkMode = coreSettings.UseDarkMode;
                    Save();
                }
            }
        }
    }
}