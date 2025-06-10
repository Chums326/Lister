using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace ChumsLister.Core.Helpers
{
    public static class UserSettingsHelper
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChumsLister",
            "userSettings.json");

        // Add this new method to fix the error in AppConnector.cs
        public static string GetCurrentUsername()
        {
            try
            {
                // We don't have ILogger here, so we'll handle this case specially
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    if (settings != null && !string.IsNullOrEmpty(settings.Username))
                    {
                        return settings.Username;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get username: {ex.Message}");
            }

            // Fallback to environment username
            return GetUsernameFallback();
        }

        public static UserSettings LoadSettings(ILogger logger)
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load user settings. Using defaults.");
            }
            return new UserSettings();
        }

        public static void SaveSettings(UserSettings settings, ILogger logger)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save user settings.");
            }
        }

        public static string GetUsernameFallback()
        {
            return Environment.UserName;
        }
    }

    public class UserSettings
    {
        public string Username { get; set; } = string.Empty;
        public string PreferredBrowser { get; set; } = "Chrome";
        public bool EnableDebugMode { get; set; } = false;
    }
}