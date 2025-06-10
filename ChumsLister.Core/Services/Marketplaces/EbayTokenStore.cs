using System;
using System.Threading.Tasks;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.Core.Services.Marketplaces
{
    /// <summary>
    /// An implementation of IEbayTokenStore that persists eBay access/refresh tokens
    /// (and expiry) into your ISettingsService / UserSettings.
    /// </summary>
    public class EbayTokenStore : IEbayTokenStore
    {
        private readonly ISettingsService _settingsService;
        private readonly EbayOAuthHelper _oauthHelper;
        private string _currentUserId;

        public EbayTokenStore(ISettingsService settingsService, EbayOAuthHelper oauthHelper)
        {
            _settingsService = settingsService
                ?? throw new ArgumentNullException(nameof(settingsService));
            _oauthHelper = oauthHelper
                ?? throw new ArgumentNullException(nameof(oauthHelper));
        }

        /// <summary>
        /// Tells this store which user ID it should operate against.
        /// Must be called after login (and again if the user changes).
        /// </summary>
        public void SetCurrentUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId cannot be null or whitespace.", nameof(userId));

            _currentUserId = userId;
            _settingsService.CurrentUserId = userId;
        }

        /// <summary>
        /// Returns the stored AccessToken for the current user (or empty if none).
        /// If the token is expired (or near expiration), call RefreshTokenAsync() first.
        /// </summary>
        public string AccessToken
        {
            get
            {
                var settings = LoadUserSettings();
                return settings?.EbayAccessToken ?? string.Empty;
            }
        }

        /// <summary>
        /// Returns the stored RefreshToken for the current user (or empty if none).
        /// </summary>
        public string RefreshToken
        {
            get
            {
                var settings = LoadUserSettings();
                return settings?.EbayRefreshToken ?? string.Empty;
            }
        }

        /// <summary>
        /// Returns the stored token expiry (UTC) for the current user, or DateTime.MinValue if none.
        /// </summary>
        public DateTime TokenExpiry
        {
            get
            {
                var settings = LoadUserSettings();
                return settings?.EbayTokenExpiry ?? DateTime.MinValue;
            }
        }

        /// <summary>
        /// Returns true if the stored access token is already expired (or will expire in the next 5 minutes).
        /// </summary>
        public bool IsExpired()
        {
            // Subtract a small margin (5 minutes) to avoid edge cases
            return TokenExpiry < DateTime.UtcNow.AddMinutes(5);
        }

        /// <summary>
        /// If the stored token is expired (or near expiry), use the stored refresh token
        /// to get a brand-new access/refresh pair from eBay, then persist them back into settings.
        /// </summary>
        public async Task RefreshTokenAsync()
        {
            // Must have a current user
            if (string.IsNullOrWhiteSpace(_currentUserId))
                throw new InvalidOperationException("Current user ID is not set in EbayTokenStore.");

            var settings = LoadUserSettings();
            if (settings == null
                || string.IsNullOrWhiteSpace(settings.EbayRefreshToken))
            {
                throw new InvalidOperationException("No refresh token available to refresh eBay access token.");
            }

            // If the token is not yet expired, we don't need to refresh
            if (!IsExpired())
                return;

            // Call the OAuth helper to exchange the existing refresh token
            var (newAccessToken, newRefreshToken, expiresInSeconds) =
                await _oauthHelper.ExchangeRefreshForTokensAsync(settings.EbayRefreshToken);

            // Compute the new expiry in UTC
            var newExpiryUtc = DateTime.UtcNow.AddSeconds(expiresInSeconds);

            // Persist back into UserSettings
            settings.EbayAccessToken = newAccessToken;
            settings.EbayRefreshToken = newRefreshToken;
            settings.EbayTokenExpiry = newExpiryUtc;

            _settingsService.SaveSettings(settings);
        }

        /// <summary>
        /// Loads the current user’s UserSettings from ISettingsService.
        /// Returns null if there are no settings for that user (which theoretically should not happen).
        /// </summary>
        private UserSettings LoadUserSettings()
        {
            if (string.IsNullOrWhiteSpace(_currentUserId))
                return null;

            // ISettingsService is expected to read/write based on its CurrentUserId
            _settingsService.CurrentUserId = _currentUserId;
            return _settingsService.GetSettingsForUser(_currentUserId);
        }

        public string Email
        {
            get
            {
                var settings = LoadUserSettings();
                return settings?.EbayRefreshToken ?? string.Empty;
            }
        }

        public string Username
        {
            get
            {
                var settings = LoadUserSettings();
                return settings?.EbayRefreshToken ?? string.Empty;
            }
        }


    }
}
