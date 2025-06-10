using System;

namespace ChumsLister.Core.Services
{
    public static class UserContext
    {
        private static string _currentUserId;

        public static string CurrentUserId
        {
            get => _currentUserId;
            set
            {
                if (_currentUserId != value)
                {
                    _currentUserId = value;
                    OnUserChanged?.Invoke(value);
                }
            }
        }

        public static event Action<string> OnUserChanged;

        public static void Clear()
        {
            CurrentUserId = null;
        }
    }
}