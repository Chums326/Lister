using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.Core.Services
{
    public class SettingsManager : ISettingsService
    {
        private static readonly string SettingsBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChumsLister");

        private static readonly object _lock = new object();
        private readonly Dictionary<string, UserSettings> _userSettingsCache = new();
        private string _currentUserId;

        // Singleton instance
        private static SettingsManager _instance;
        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsManager();
                }
                return _instance;
            }
        }

        public void LoadSettings()
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Debug.WriteLine("WARNING: LoadSettings called without CurrentUserId set");
                return;
            }

            // Force reload from file by clearing cache
            lock (_lock)
            {
                _userSettingsCache.Remove(CurrentUserId);
            }

            // Load settings (this will cache them)
            var settings = GetSettings();
            Debug.WriteLine($"Settings loaded for user {CurrentUserId}");
        }

        public string CurrentUserId
        {
            get => _currentUserId;
            set
            {
                _currentUserId = value;
                Debug.WriteLine($"Core SettingsManager: CurrentUserId set to {value}");
            }
        }

        private SettingsManager()
        {
            Directory.CreateDirectory(SettingsBasePath);
        }

        private string GetUserSettingsPath(string userId)
        {
            return Path.Combine(SettingsBasePath, $"user_{userId}_settings.json");
        }

        public UserSettings GetSettings()
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Debug.WriteLine("WARNING: GetSettings called without CurrentUserId set");
                return new UserSettings();
            }
            return GetSettingsForUser(CurrentUserId);
        }

        public UserSettings GetSettingsForUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("WARNING: GetSettingsForUser called with null/empty userId");
                return new UserSettings();
            }

            lock (_lock)
            {
                // Check cache first
                if (_userSettingsCache.TryGetValue(userId, out var cachedSettings))
                {
                    return cachedSettings.Clone();
                }

                // Load from file
                var settings = LoadSettingsFromFile(userId);
                _userSettingsCache[userId] = settings;
                return settings.Clone();
            }
        }

        public void SaveSettings(UserSettings settings)
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Debug.WriteLine("WARNING: SaveSettings called without CurrentUserId set");
                return;
            }
            SaveSettingsForUser(CurrentUserId, settings);
        }

        public void SaveSettingsForUser(string userId, UserSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("WARNING: SaveSettingsForUser called with null settings or userId");
                return;
            }

            lock (_lock)
            {
                // Update cache
                _userSettingsCache[userId] = settings.Clone();

                // Save to file
                SaveSettingsToFile(userId, settings);
            }
        }

        public Task<UserSettings> GetSettingsAsync()
        {
            return Task.FromResult(GetSettings());
        }

        public Task SaveSettingsAsync(UserSettings settings)
        {
            SaveSettings(settings);
            return Task.CompletedTask;
        }

        private UserSettings LoadSettingsFromFile(string userId)
        {
            try
            {
                var filePath = GetUserSettingsPath(userId);

                if (File.Exists(filePath))
                {
                    Debug.WriteLine($"Loading user settings from: {filePath}");
                    string json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                    Debug.WriteLine($"Loaded settings for user {userId}: DarkMode={settings.UseDarkMode}, EbayToken={!string.IsNullOrEmpty(settings.EbayAccessToken)}");
                    return settings;
                }
                else
                {
                    Debug.WriteLine($"User settings file not found for user {userId}, creating new settings");
                    var settings = new UserSettings();
                    SaveSettingsToFile(userId, settings);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user settings for {userId}: {ex.Message}");
                return new UserSettings();
            }
        }

        private void SaveSettingsToFile(string userId, UserSettings settings)
        {
            try
            {
                var filePath = GetUserSettingsPath(userId);
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Debug.WriteLine($"Saved settings for user {userId}: DarkMode={settings.UseDarkMode}, EbayToken={!string.IsNullOrEmpty(settings.EbayAccessToken)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving user settings for {userId}: {ex.Message}");
            }
        }

        public T GetSetting<T>(string key, T defaultValue)
        {
            var settings = GetSettings();
            if (settings?.CustomSettings != null && settings.CustomSettings.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typedValue)
                        return typedValue;

                    // Handle JsonElement conversion
                    if (value is JsonElement jsonElement)
                    {
                        var json = jsonElement.GetRawText();
                        return JsonSerializer.Deserialize<T>(json);
                    }

                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public void SetSetting<T>(string key, T value)
        {
            var settings = GetSettings();
            if (settings != null)
            {
                settings.CustomSettings ??= new Dictionary<string, object>();
                settings.CustomSettings[key] = value;
                SaveSettings(settings);
            }
        }

        public bool TestApiConnection(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey);
        }

        public void ResetToDefaults()
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Debug.WriteLine("WARNING: ResetToDefaults called without CurrentUserId set");
                return;
            }

            var settings = new UserSettings();
            SaveSettings(settings);
        }

        // Clear cache when switching users
        public void ClearUserCache(string userId = null)
        {
            lock (_lock)
            {
                if (userId != null)
                {
                    _userSettingsCache.Remove(userId);
                }
                else
                {
                    _userSettingsCache.Clear();
                }
            }
        }
    }
}