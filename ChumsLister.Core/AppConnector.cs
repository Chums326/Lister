using ChumsLister.Core.Helpers;
using ChumsLister.Core.Services;
using System;
using System.Diagnostics;
using System.Windows;

namespace ChumsLister.Core
{
    /// <summary>
    /// Static class to handle application-wide services and connections
    /// </summary>
    public static class AppConnector
    {
        // Flag to track initialization
        private static bool _isInitialized = false;

        // Event to notify when user changes
        public static event Action<string> UserChanged;

        /// <summary>
        /// Initialize all application services and connections
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                // Ensure the current user is set
                string username = UserSettingsHelper.GetCurrentUsername();

                // Set user context
                UserContext.CurrentUserId = username;

                // Initialize database for this user
                DatabaseService.InitializeDatabase(username);

                // Set current user in settings manager
                SettingsManager.Instance.CurrentUserId = username;

                // Pre-load settings for the current user
                SettingsManager.Instance.LoadSettings();

                // Log initialization
                Debug.WriteLine($"AppConnector initialized for user: {username}");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing AppConnector: {ex.Message}");
                MessageBox.Show($"Error initializing application: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Change the current user and reload all user-specific data
        /// </summary>
        public static void ChangeUser(string newUsername)
        {
            try
            {
                // Update user context
                UserContext.CurrentUserId = newUsername;

                // Update database service
                DatabaseService.SetCurrentUser(newUsername);

                // Update settings manager
                SettingsManager.Instance.CurrentUserId = newUsername;

                // Clear settings cache
                SettingsManager.Instance.ClearUserCache();

                // Try to update AppSettings via reflection
                try
                {
                    var appSettingsType = Type.GetType("ChumsLister.WPF.Helpers.AppSettings, ChumsLister.WPF");
                    if (appSettingsType != null)
                    {
                        var usernameProperty = appSettingsType.GetProperty("Username");
                        if (usernameProperty != null && usernameProperty.CanWrite)
                        {
                            usernameProperty.SetValue(null, newUsername);
                            Debug.WriteLine($"Updated username in AppSettings: {newUsername}");
                        }
                    }
                }
                catch (Exception reflectionEx)
                {
                    Debug.WriteLine($"Error setting username with reflection: {reflectionEx.Message}");
                }

                // Clear inventory cache using reflection only
                try
                {
                    // Try to clear InventoryService cache via reflection
                    var inventoryServiceType = Type.GetType("ChumsLister.WPF.Services.InventoryService, ChumsLister.WPF");
                    if (inventoryServiceType != null)
                    {
                        var clearCacheMethod = inventoryServiceType.GetMethod("ClearCache",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                        if (clearCacheMethod != null)
                        {
                            clearCacheMethod.Invoke(null, new object[] { newUsername });
                            Debug.WriteLine("Cleared InventoryService cache through reflection");
                        }
                    }

                    // Also try InventoryPage method for backward compatibility
                    var inventoryPageType = Type.GetType("ChumsLister.WPF.Views.InventoryPage, ChumsLister.WPF");
                    if (inventoryPageType != null)
                    {
                        var clearMethod = inventoryPageType.GetMethod("ClearStaticInventory",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                        if (clearMethod != null)
                        {
                            clearMethod.Invoke(null, null);
                            Debug.WriteLine("Cleared inventory page cache through reflection");
                        }
                    }
                }
                catch (Exception invEx)
                {
                    Debug.WriteLine($"Error clearing inventory: {invEx.Message}");
                }

                // Fire user changed event
                UserChanged?.Invoke(newUsername);

                // Reset initialization flag to force reloading services
                _isInitialized = false;

                // Re-initialize with the new user
                Initialize();

                Debug.WriteLine($"Changed user to: {newUsername}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing user: {ex.Message}");
                MessageBox.Show($"Error changing user: {ex.Message}",
                    "User Change Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}