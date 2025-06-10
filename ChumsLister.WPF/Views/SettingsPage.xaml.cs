using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using ChumsLister.Core.Services.Marketplaces;
using ChumsLister.WPF.Utilities;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Web;               // For HttpUtility.ParseQueryString
using MessageBox = System.Windows.MessageBox;

namespace ChumsLister.WPF.Views
{
    public partial class SettingsPage : Page
    {
        private readonly ISettingsService _settingsService;
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private readonly IConfiguration _configuration;
        private readonly IEbayTokenStore _ebayTokenStore;

        private UserSettings _currentSettings;

        public SettingsPage(
            ISettingsService settingsService,
            IMarketplaceServiceFactory marketplaceFactory,
            IConfiguration configuration,
            IEbayTokenStore ebayTokenStore)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _marketplaceFactory = marketplaceFactory;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _ebayTokenStore = ebayTokenStore ?? throw new ArgumentNullException(nameof(ebayTokenStore));

            LoadSettings();

            if (_marketplaceFactory != null)
            {
                // (No marketplaces list in this XAML, so nothing to do here)
            }

            if (chkDarkMode != null)
            {
                chkDarkMode.Checked += ChkDarkMode_CheckedChanged;
                chkDarkMode.Unchecked += ChkDarkMode_CheckedChanged;
            }

            // Tell the token store which user’s settings it should read/write.
            // We assume CurrentUserId was already set by App.SetCurrentUser before showing this page.
            if (!string.IsNullOrWhiteSpace(_settingsService.CurrentUserId))
            {
                _ebayTokenStore.SetCurrentUser(_settingsService.CurrentUserId);
            }
        }

        private void LoadSettings()
        {
            try
            {
                _currentSettings = _settingsService.GetSettings()
                                   ?? new UserSettings();

                // Populate existing settings into the UI:
                SafeSetPassword(txtApiKey, _currentSettings.ApiKey);
                SafeSetText(txtUserName, _currentSettings.UserName);
                SafeSetText(txtEmailAddress, _currentSettings.EmailAddress);
                SafeSetText(txtDefaultCategory, _currentSettings.DefaultCategory);

                SafeSetChecked(chkDarkMode, _currentSettings.UseDarkMode);
                SafeSetChecked(chkAutoSync, _currentSettings.AutoSyncInventory);
                SafeSetChecked(chkEnableAI, _currentSettings.EnableAIFeatures);

                // ▶── begin listing-template load:
                var tpl = _currentSettings.ListingTemplate ?? new ListingTemplateSettings();
                SafeSetText(txtShippingPolicyId, tpl.ShippingPolicyId);
                SafeSetText(txtPaymentPolicyId, tpl.PaymentPolicyId);
                SafeSetText(txtReturnPolicyId, tpl.ReturnPolicyId);
                SafeSetText(txtMainEbayCategory, tpl.MainEbayCategory);
                SafeSetText(txtStoreCategory, tpl.StoreCategory);
                SafeSetText(txtMainEbayCategory, tpl.MainEbayCategory);
                SafeSetText(txtStoreCategory, tpl.StoreCategory);

                // ListingType
                foreach (ComboBoxItem it in cboListingType.Items)
                    if ((string)it.Content == tpl.ListingType) { cboListingType.SelectedItem = it; break; }

                // Duration
                foreach (ComboBoxItem it in cboDuration.Items)
                    if ((string)it.Content == tpl.Duration) { cboDuration.SelectedItem = it; break; }

                SafeSetText(txtHandlingTime, tpl.HandlingTimeDays.ToString());
                SafeSetText(txtItemLocation, tpl.ItemLocation);
                txtPaymentMethods.Text = string.Join(", ", tpl.PaymentMethods);
                SafeSetText(txtDescriptionTemplate, tpl.DescriptionTemplateHtml);
                // ▶── end listing-template load

                if (cboDescriptionStyle != null && !string.IsNullOrEmpty(_currentSettings.DescriptionStyle))
                {
                    for (int i = 0; i < cboDescriptionStyle.Items.Count; i++)
                    {
                        if (cboDescriptionStyle.Items[i] is ComboBoxItem item &&
                            item.Content.ToString() == _currentSettings.DescriptionStyle)
                        {
                            cboDescriptionStyle.SelectedIndex = i;
                            break;
                        }
                    }
                }

                SafeSetChecked(chkIncludeShipping, _currentSettings.IncludeShippingByDefault);
                SafeSetChecked(chkAutoCalculateShipping, _currentSettings.AutoCalculateShipping);
                SafeSetChecked(chkAutomaticUpdates, _currentSettings.AutomaticUpdates);
                SafeSetChecked(chkCheckInventory, _currentSettings.CheckInventoryBeforeListing);

                // Show eBay status:
                if (!string.IsNullOrWhiteSpace(_currentSettings.EbayAccessToken))
                {
                    lblEbayStatus.Content = "eBay: Connected ✓";
                }
                else
                {
                    lblEbayStatus.Content = "eBay: Not Connected";
                }

                Debug.WriteLine($"[SettingsPage] Settings loaded: DarkMode={_currentSettings.UseDarkMode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error loading settings: {ex}");
                MessageBox.Show($"Error loading settings: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void SafeSetText(System.Windows.Controls.TextBox textBox, string value)
        {
            if (textBox != null) textBox.Text = value ?? string.Empty;
        }

        private void SafeSetPassword(PasswordBox passwordBox, string value)
        {
            if (passwordBox != null) passwordBox.Password = value ?? string.Empty;
        }

        private void SafeSetChecked(System.Windows.Controls.CheckBox checkBox, bool value)
        {
            if (checkBox != null) checkBox.IsChecked = value;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentSettings == null)
                    _currentSettings = new UserSettings();

                if (txtApiKey != null) _currentSettings.ApiKey = txtApiKey.Password;
                if (txtUserName != null) _currentSettings.UserName = txtUserName.Text;
                if (txtEmailAddress != null) _currentSettings.EmailAddress = txtEmailAddress.Text;
                if (txtDefaultCategory != null) _currentSettings.DefaultCategory = txtDefaultCategory.Text;

                if (chkDarkMode != null) _currentSettings.UseDarkMode = chkDarkMode.IsChecked ?? false;
                if (chkAutoSync != null) _currentSettings.AutoSyncInventory = chkAutoSync.IsChecked ?? false;
                if (chkEnableAI != null) _currentSettings.EnableAIFeatures = chkEnableAI.IsChecked ?? false;

                if (cboDescriptionStyle?.SelectedItem is ComboBoxItem styleItem)
                    _currentSettings.DescriptionStyle = styleItem.Content.ToString();

                if (chkIncludeShipping != null) _currentSettings.IncludeShippingByDefault = chkIncludeShipping.IsChecked ?? false;
                if (chkAutoCalculateShipping != null) _currentSettings.AutoCalculateShipping = chkAutoCalculateShipping.IsChecked ?? false;
                if (chkAutomaticUpdates != null) _currentSettings.AutomaticUpdates = chkAutomaticUpdates.IsChecked ?? false;
                if (chkCheckInventory != null) _currentSettings.CheckInventoryBeforeListing = chkCheckInventory.IsChecked ?? false;

                // ▶── begin listing-template save:
                if (_currentSettings.ListingTemplate == null)
                    _currentSettings.ListingTemplate = new ListingTemplateSettings();

                var t = _currentSettings.ListingTemplate;
                t.MainEbayCategory = txtMainEbayCategory.Text.Trim();
                t.StoreCategory = txtStoreCategory.Text.Trim();
                t.ShippingPolicyId = txtShippingPolicyId.Text.Trim();
                t.PaymentPolicyId = txtPaymentPolicyId.Text.Trim();
                t.ReturnPolicyId = txtReturnPolicyId.Text.Trim();
                t.MainEbayCategory = txtMainEbayCategory.Text.Trim();
                t.StoreCategory = txtStoreCategory.Text.Trim();
                t.ListingType = ((ComboBoxItem)cboListingType.SelectedItem)?.Content.ToString() ?? t.ListingType;
                t.Duration = ((ComboBoxItem)cboDuration.SelectedItem)?.Content.ToString() ?? t.Duration;
                t.HandlingTimeDays = int.TryParse(txtHandlingTime.Text.Trim(), out var d) ? d : t.HandlingTimeDays;
                t.ItemLocation = txtItemLocation.Text.Trim();
                t.PaymentMethods = txtPaymentMethods.Text
                                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .ToList();
                t.DescriptionTemplateHtml = txtDescriptionTemplate.Text;
                // ▶── end listing-template save

                _settingsService.SaveSettings(_currentSettings);

                MessageBox.Show("Settings saved successfully!",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                ApplySettings();
                Debug.WriteLine($"[SettingsPage] Settings saved: DarkMode={_currentSettings.UseDarkMode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error saving settings: {ex}");
                MessageBox.Show($"Error saving settings: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void ChkDarkMode_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                bool useDarkMode = chkDarkMode.IsChecked ?? false;
                if (_currentSettings != null)
                    _currentSettings.UseDarkMode = useDarkMode;

                ThemeManager.ApplyTheme(useDarkMode);
                Debug.WriteLine($"[SettingsPage] Dark mode toggled: {useDarkMode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error toggling dark mode: {ex}");
            }
        }

        private void ApplySettings()
        {
            try
            {
                bool useDarkMode = _currentSettings.UseDarkMode;
                ThemeManager.ApplyTheme(useDarkMode);
                ChumsLister.WPF.Helpers.AppSettings.SyncWithCoreSettings(_currentSettings);

                Debug.WriteLine($"[SettingsPage] Applied settings: DarkMode={useDarkMode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error applying settings: {ex}");
            }
        }

        private void btnLoginToEbay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var oauthHelper = new EbayOAuthHelper(_configuration);
                var authUrl = oauthHelper.GenerateAuthorizationUrl();

                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                MessageBox.Show(
                    "After signing in and granting consent, you will be redirected to a page with an\n" +
                    "eBay authorization code in the URL (it might show an error on that page).\n\n" +
                    "Please copy the ENTIRE final URL from your browser’s address bar and paste it below.",
                    "eBay Authorization",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Failed to open eBay login: {ex}");
                MessageBox.Show($"Failed to open eBay login: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private async void btnSubmitEbayCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEbayCode.Text))
            {
                MessageBox.Show(
                    "Please paste the full redirect URL (with code=…) from your browser’s address bar.",
                    "Missing Code",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            btnSubmitEbayCode.IsEnabled = false;
            try
            {
                string inputUrl = txtEbayCode.Text.Trim();
                Debug.WriteLine($"[SettingsPage] Input URL: {inputUrl}");

                // Extract the "code=" parameter value
                string authCode;
                if (inputUrl.Contains("code=", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(inputUrl);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    authCode = query["code"] ?? string.Empty;
                }
                else
                {
                    // If user just pasted the code itself
                    authCode = inputUrl;
                }

                if (string.IsNullOrWhiteSpace(authCode))
                {
                    MessageBox.Show(
                        "Could not find 'code=' in the URL. Please paste the exact eBay redirect URL.",
                        "Invalid Input",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                var oauthHelper = new EbayOAuthHelper(_configuration);
                var (accessToken, refreshToken, expiresIn) =
                    await oauthHelper.ExchangeCodeForTokensAsync(authCode);

                // Persist them in UserSettings
                _currentSettings.EbayAccessToken = accessToken;
                _currentSettings.EbayRefreshToken = refreshToken;
                _currentSettings.EbayTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                _settingsService.SaveSettings(_currentSettings);

                Debug.WriteLine($"[SettingsPage] eBay tokens saved: " +
                                $"Access={accessToken.Substring(0, Math.Min(10, accessToken.Length))}…, " +
                                $"Refresh={refreshToken.Substring(0, Math.Min(10, refreshToken.Length))}…, " +
                                $"ExpiresIn={expiresIn}s");

                // Tell the token store which user to use
                _ebayTokenStore.SetCurrentUser(_settingsService.CurrentUserId);

                lblEbayStatus.Content = "eBay: Connected ✓";

                MessageBox.Show(
                    "eBay account successfully connected! You may now create or manage listings.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // Clear the text box after success
                txtEbayCode.Text = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error exchanging code for tokens: {ex}");
                MessageBox.Show($"Error connecting to eBay: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
            finally
            {
                btnSubmitEbayCode.IsEnabled = true;
            }
        }
    }
}
