using System;

namespace ChumsLister.WPF.Helpers
{
    /// <summary>
    /// Static class to store session-only data that doesn't persist between application restarts
    /// </summary>
    public static class AppSession
    {
        // Current username for the active session (not persisted)
        private static string _currentUsername;

        /// <summary>
        /// Gets or sets the current username for this session only
        /// This is not persisted between application restarts
        /// </summary>
        public static string CurrentUsername
        {
            get => _currentUsername;
            set => _currentUsername = value;
        }

        /// <summary>
        /// Clears all session data
        /// </summary>
        public static void Clear()
        {
            _currentUsername = null;
        }
    }
}