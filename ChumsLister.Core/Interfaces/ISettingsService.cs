using ChumsLister.Core.Models;
using System.Threading.Tasks;

namespace ChumsLister.Core.Interfaces
{
    public interface ISettingsService
    {
        string CurrentUserId { get; set; }
        UserSettings GetSettings();
        void SaveSettings(UserSettings settings);
        T GetSetting<T>(string key, T defaultValue);
        void SetSetting<T>(string key, T value);
        bool TestApiConnection(string apiKey);
        void ResetToDefaults();

        // Multi-user methods
        UserSettings GetSettingsForUser(string userId);
        void SaveSettingsForUser(string userId, UserSettings settings);
        Task<UserSettings> GetSettingsAsync();
        Task SaveSettingsAsync(UserSettings settings);
    }
}