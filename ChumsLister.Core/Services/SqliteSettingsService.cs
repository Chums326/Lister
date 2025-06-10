using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ChumsLister.Core.Services
{
    public class SqliteSettingsService : ISettingsService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;
        private string _currentUserId;

        public SqliteSettingsService(string dbPath, IConfiguration config)
        {
            _config = config;
            _connectionString = $"Data Source={dbPath}";
            EnsureTables();
        }

        public string CurrentUserId
        {
            get => _currentUserId ?? throw new InvalidOperationException("Call SetCurrentUserId before using settings.");
            set => _currentUserId = value;
        }

        public void SetCurrentUserId(string userId) => _currentUserId = userId;

        private void EnsureTables()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS Settings (
                  UserId           TEXT PRIMARY KEY,
                  EbayAccessToken  TEXT,
                  EbayRefreshToken TEXT,
                  EbayExpiry       INTEGER,
                  UseDarkMode      INTEGER,
                  CustomSettings   TEXT
                );", tx);

            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS Inventory (
                  Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                  UserId      TEXT,
                  SKU         TEXT,
                  Description TEXT,
                  Quantity    INTEGER,
                  FOREIGN KEY(UserId) REFERENCES Settings(UserId)
                );", tx);

            tx.Commit();
        }

        public UserSettings GetSettings()
        {
            return GetSettingsForUser(CurrentUserId);
        }

        public UserSettings GetSettingsForUser(string userId)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT EbayAccessToken, EbayRefreshToken, EbayExpiry, UseDarkMode, CustomSettings
                  FROM Settings
                 WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);

            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read())
                return new UserSettings();

            var settings = new UserSettings
            {
                EbayAccessToken = rdr.IsDBNull(0) ? null : rdr.GetString(0),
                EbayRefreshToken = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                EbayTokenExpiry = rdr.IsDBNull(2) ? null : DateTimeOffset.FromUnixTimeSeconds(rdr.GetInt64(2)).UtcDateTime,
                UseDarkMode = rdr.GetInt32(3) == 1
            };

            // Deserialize custom settings if present
            if (!rdr.IsDBNull(4))
            {
                var customSettingsJson = rdr.GetString(4);
                settings.CustomSettings = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(customSettingsJson);
            }

            return settings;
        }

        public void SaveSettings(UserSettings settings)
        {
            SaveSettingsForUser(CurrentUserId, settings);
        }

        public void SaveSettingsForUser(string userId, UserSettings settings)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var customSettingsJson = settings.CustomSettings != null
                ? System.Text.Json.JsonSerializer.Serialize(settings.CustomSettings)
                : null;

            cmd.CommandText = @"
                INSERT INTO Settings (UserId, EbayAccessToken, EbayRefreshToken, EbayExpiry, UseDarkMode, CustomSettings)
                VALUES ($uid, $acc, $ref, $exp, $dm, $cs)
                ON CONFLICT(UserId) DO UPDATE SET
                  EbayAccessToken  = $acc,
                  EbayRefreshToken = $ref,
                  EbayExpiry       = $exp,
                  UseDarkMode      = $dm,
                  CustomSettings   = $cs;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$acc", settings.EbayAccessToken ?? "");
            cmd.Parameters.AddWithValue("$ref", settings.EbayRefreshToken ?? "");
            cmd.Parameters.AddWithValue("$exp", settings.EbayTokenExpiry.HasValue
                ? ((DateTimeOffset)settings.EbayTokenExpiry.Value).ToUnixTimeSeconds()
                : 0);
            cmd.Parameters.AddWithValue("$dm", settings.UseDarkMode ? 1 : 0);
            cmd.Parameters.AddWithValue("$cs", customSettingsJson ?? "");

            cmd.ExecuteNonQuery();
        }

        public async Task<UserSettings> GetSettingsAsync()
        {
            return await Task.Run(() => GetSettings());
        }

        public async Task SaveSettingsAsync(UserSettings settings)
        {
            await Task.Run(() => SaveSettings(settings));
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
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        var json = jsonElement.GetRawText();
                        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
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
            settings.CustomSettings ??= new System.Collections.Generic.Dictionary<string, object>();
            settings.CustomSettings[key] = value;
            SaveSettings(settings);
        }

        public bool TestApiConnection(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey);
        }

        public void ResetToDefaults()
        {
            SaveSettings(new UserSettings());
        }

        public T GetGlobal<T>(string key, T defaultValue = default)
        {
            var val = _config[key];
            if (string.IsNullOrEmpty(val)) return defaultValue;
            return (T)Convert.ChangeType(val, typeof(T));
        }
    }

    internal static class SqliteExtensions
    {
        public static void Execute(this SqliteConnection conn, string sql, SqliteTransaction tx)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}