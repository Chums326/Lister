using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ChumsLister.Core.Services
{
    public static class DatabaseService
    {
        private static string _currentUserId = "default";

        // Static property for backward compatibility
        public static string DbPath => GetUserDatabasePath(_currentUserId);

        // Set the current user for database operations
        public static void SetCurrentUser(string userId)
        {
            _currentUserId = userId ?? "default";
        }

        private static string GetUserDatabasePath(string userId)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChumsLister",
                "UserData"
            );

            Directory.CreateDirectory(appDataPath);
            return Path.Combine(appDataPath, $"user_{userId}.db");
        }

        public static void InitializeDatabase()
        {
            // Initialize default database for backward compatibility
            InitializeDatabase("default");
        }

        public static void InitializeDatabase(string userId)
        {
            SetCurrentUser(userId);
            var dbPath = GetUserDatabasePath(userId);

            // Create the database if it doesn't exist
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            // Create tables
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Inventory (
                    SKU TEXT PRIMARY KEY,
                    TRANS_ID TEXT,
                    MODEL_HD_SKU TEXT,
                    DESCRIPTION TEXT,
                    QTY INTEGER,
                    RETAIL_PRICE REAL,
                    COST_ITEM REAL,
                    TOTAL_COST_ITEM REAL,
                    QTY_SOLD INTEGER,
                    SOLD_PRICE TEXT,
                    STATUS TEXT,
                    REPO TEXT,
                    LOCATION TEXT,
                    DATE_SOLD TEXT
                )";
            command.ExecuteNonQuery();

            // Add other tables as needed
        }

        public static string GetDatabasePath(string userId)
        {
            return GetUserDatabasePath(userId);
        }
    }
}
