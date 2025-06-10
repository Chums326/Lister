using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ChumsLister.Core.Services.Marketplaces
{
    /// <summary>
    /// Encapsulates eBay OAuth2 calls:
    ///   • GenerateAuthorizationUrl()
    ///   • ExchangeCodeForTokensAsync(...)
    ///   • ExchangeRefreshForTokensAsync(...)
    /// 
    /// Expects these keys in IConfiguration (e.g. appsettings.json):
    ///   "eBay:ClientId", "eBay:ClientSecret", "eBay:RuName"
    /// </summary>
    public class EbayOAuthHelper
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;

        // Change scopes as needed
        private const string _defaultScope =
            "https://api.ebay.com/oauth/api_scope " +
            "https://api.ebay.com/oauth/api_scope/sell.inventory";

        public EbayOAuthHelper(IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            _clientId = configuration["eBay:ClientId"]
                            ?? throw new InvalidOperationException("Missing eBay:ClientId in configuration");
            _clientSecret = configuration["eBay:ClientSecret"]
                            ?? throw new InvalidOperationException("Missing eBay:ClientSecret in configuration");
            _redirectUri = configuration["eBay:RuName"]
                            ?? throw new InvalidOperationException("Missing eBay:RuName in configuration");
        }

        /// <summary>
        /// Builds the URL that the user clicks to log in and grant consent. 
        /// After consent, eBay will redirect to RuName with ?code=… in the query string.
        /// </summary>
        public string GenerateAuthorizationUrl()
        {
            string encodedRedirect = Uri.EscapeDataString(_redirectUri);
            string encodedScope = Uri.EscapeDataString(_defaultScope);

            return "https://auth.ebay.com/oauth2/authorize?"
                 + $"client_id={_clientId}"
                 + "&response_type=code"
                 + $"&redirect_uri={encodedRedirect}"
                 + $"&scope={encodedScope}";
        }

        /// <summary>
        /// Exchange a one‐time authorization code for (access_token, refresh_token, expires_in).
        /// </summary>
        public async Task<(string accessToken, string refreshToken, int expiresIn)>
            ExchangeCodeForTokensAsync(string authorizationCode)
        {
            if (string.IsNullOrWhiteSpace(authorizationCode))
                throw new ArgumentException("Authorization code cannot be empty", nameof(authorizationCode));

            using var client = new HttpClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "authorization_code"),
                new KeyValuePair<string,string>("code", authorizationCode),
                new KeyValuePair<string,string>("redirect_uri", _redirectUri)
            });

            // Basic‐Auth header
            string creds = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")
            );
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);

            HttpResponseMessage response = await client.PostAsync(
                "https://api.ebay.com/identity/v1/oauth2/token",
                form
            );

            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[EbayOAuthHelper] Token request failed: {response.StatusCode} → {json}");
                throw new InvalidOperationException($"eBay token endpoint returned {response.StatusCode}: {json}");
            }

            var o = JObject.Parse(json);
            string accessToken = (string)o["access_token"]
                                  ?? throw new InvalidOperationException("Log in to Ebay.");
            string refreshToken = (string)o["refresh_token"]
                                  ?? throw new InvalidOperationException("Log in to Ebay.");
            int expiresIn = (int?)o["expires_in"] ?? 0;

            return (accessToken, refreshToken, expiresIn);
        }

        /// <summary>
        /// Exchange an existing refresh_token for a fresh (access_token, refresh_token, expires_in).
        /// </summary>
        public async Task<(string accessToken, string refreshToken, int expiresIn)>
            ExchangeRefreshForTokensAsync(string existingRefreshToken)
        {
            if (string.IsNullOrWhiteSpace(existingRefreshToken))
                throw new ArgumentException("Refresh token cannot be empty", nameof(existingRefreshToken));

            using var client = new HttpClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "refresh_token"),
                new KeyValuePair<string,string>("refresh_token", existingRefreshToken),
                // It’s recommended to include the same redirect_uri for refresh grant:
                new KeyValuePair<string,string>("redirect_uri", _redirectUri)
            });

            string creds = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")
            );
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);

            HttpResponseMessage response = await client.PostAsync(
                "https://api.ebay.com/identity/v1/oauth2/token",
                form
            );

            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[EbayOAuthHelper] Refresh request failed: {response.StatusCode} → {json}");
                throw new InvalidOperationException($"eBay refresh endpoint returned {response.StatusCode}: {json}");
            }

            var o = JObject.Parse(json);
            string newAccessToken = (string)o["access_token"]
                                     ?? throw new InvalidOperationException("Log in to Ebay.");
            string newRefreshToken = (string)o["refresh_token"]
                                     ?? throw new InvalidOperationException("Log in to Ebay.");
            int expiresIn = (int?)o["expires_in"] ?? 0;

            return (newAccessToken, newRefreshToken, expiresIn);
        }
    }
}
