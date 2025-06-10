using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// Interaction logic for TitleAndSkuPage.xaml
    /// </summary>
    public partial class TitleAndSkuPage : Page, IWizardPage
    {
        private readonly IProductScraper _productScraper;
        private readonly IAIService _aiService;
        private readonly IEbayService _ebayService;
        private string _accountId;
        private bool _isValidatingSku;

        public TitleAndSkuPage(IProductScraper productScraper, IAIService aiService, IEbayService ebayService)
        {
            InitializeComponent();
            _productScraper = productScraper;
            _aiService = aiService;
            _ebayService = ebayService;
        }

        public bool ValidatePage()
        {
            // Validate title
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                System.Windows.MessageBox.Show("Please enter a title for your listing", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (txtTitle.Text.Length < 3)
            {
                System.Windows.MessageBox.Show("Title must be at least 3 characters long", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validate SKU
            if (string.IsNullOrWhiteSpace(txtSku.Text))
            {
                System.Windows.MessageBox.Show("Please enter a SKU for inventory tracking", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check for product identifiers if not marked as "no identifiers"
            if (chkNoProductId.IsChecked != true)
            {
                bool hasIdentifier = !string.IsNullOrWhiteSpace(txtUPC.Text) ||
                                   !string.IsNullOrWhiteSpace(txtEAN.Text) ||
                                   !string.IsNullOrWhiteSpace(txtISBN.Text) ||
                                   !string.IsNullOrWhiteSpace(txtMPN.Text);

                if (!hasIdentifier)
                {
                     System.Windows.MessageBox.Show("Please provide at least one product identifier (UPC, EAN, ISBN, or MPN) " +
                                  "or check 'Product does not have unique identifiers'",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            listingData.Title = txtTitle.Text.Trim();
            listingData.Subtitle = chkSubtitle.IsChecked == true ? txtSubtitle.Text.Trim() : null;
            listingData.CustomSku = txtSku.Text.Trim();
            listingData.UPC = txtUPC.Text.Trim();
            listingData.EAN = txtEAN.Text.Trim();
            listingData.ISBN = txtISBN.Text.Trim();
            listingData.MPN = txtMPN.Text.Trim();
            listingData.Brand = txtBrand.Text.Trim();
        }

        public void LoadData(ListingWizardData listingData)
        {
            _accountId = listingData.SelectedAccountId;

            txtTitle.Text = listingData.Title ?? "";

            if (!string.IsNullOrEmpty(listingData.Subtitle))
            {
                chkSubtitle.IsChecked = true;
                txtSubtitle.Text = listingData.Subtitle;
            }

            txtSku.Text = listingData.CustomSku ?? "";
            txtUPC.Text = listingData.UPC ?? "";
            txtEAN.Text = listingData.EAN ?? "";
            txtISBN.Text = listingData.ISBN ?? "";
            txtMPN.Text = listingData.MPN ?? "";
            txtBrand.Text = listingData.Brand ?? "";

            // Update character count
            UpdateCharacterCount();
        }

        private void txtTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharacterCount();

            // Check for prohibited characters
            if (ContainsProhibitedCharacters(txtTitle.Text))
            {
                txtTitle.BorderBrush = System.Windows.Media.Brushes.Red;
                txtTitle.ToolTip = "Title contains prohibited characters (HTML tags, excessive punctuation)";
            }
            else
            {
                txtTitle.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
                txtTitle.ToolTip = null;
            }
        }

        private void UpdateCharacterCount()
        {
            int charCount = txtTitle.Text?.Length ?? 0;
            txtCharCount.Text = $"{charCount}/80";

            if (charCount > 80)
            {
                txtCharCount.Foreground = System.Windows.Media. Brushes.Red;
            }
            else if (charCount > 70)
            {
                txtCharCount.Foreground = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                txtCharCount.Foreground = (System.Windows.Media.Brush)FindResource("TextMediumBrush");
            }
        }

        private bool ContainsProhibitedCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Check for HTML tags
            if (Regex.IsMatch(text, @"<[^>]+>")) return true;

            // Check for excessive punctuation
            if (Regex.IsMatch(text, @"[!@#$%^&*]{3,}")) return true;

            // Check for all caps (more than 5 consecutive capital letters)
            if (Regex.IsMatch(text, @"[A-Z]{6,}")) return true;

            return false;
        }

        private async void btnOptimizeTitle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                System.Windows.MessageBox.Show("Please enter a title first", "No Title",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Get the current wizard data
                var wizard = Window.GetWindow(this) as WizardWindow;
                var wizardData = wizard?.WizardData;

                if (wizardData == null)
                {
                    System.Windows.MessageBox.Show("Could not access wizard data", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create a product data object
                var product = new ProductData
                {
                    Title = txtTitle.Text,
                    BrandModel = txtBrand.Text,
                    ModelNumber = txtMPN.Text,
                    ItemType = wizardData.PrimaryCategoryName,
                    Condition = wizardData.ConditionName
                };

                // Call AI service
                string optimizedTitle = await _aiService.SuggestTitleAsync(product);

                if (!string.IsNullOrEmpty(optimizedTitle))
                {
                    string originalTitle = txtTitle.Text;
                    txtTitle.Text = optimizedTitle;

                    var result = System.Windows.MessageBox.Show(
                        $"Original: {originalTitle}\n\n" +
                        $"Optimized: {optimizedTitle}\n\n" +
                        "Keep the optimized title?",
                        "AI Title Optimization",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        txtTitle.Text = originalTitle;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error optimizing title: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void chkSubtitle_Checked(object sender, RoutedEventArgs e)
        {
            txtSubtitle.Visibility = Visibility.Visible;
        }

        private void chkSubtitle_Unchecked(object sender, RoutedEventArgs e)
        {
            txtSubtitle.Visibility = Visibility.Collapsed;
            txtSubtitle.Text = "";
        }

        private void btnGenerateSku_Click(object sender, RoutedEventArgs e)
        {
            // Generate a unique SKU based on current date/time and random component
            string prefix = !string.IsNullOrEmpty(txtBrand.Text)
                ? txtBrand.Text.Substring(0, Math.Min(3, txtBrand.Text.Length)).ToUpper()
                : "SKU";

            string timestamp = DateTime.Now.ToString("yyMMdd");
            string random = new Random().Next(1000, 9999).ToString();

            txtSku.Text = $"{prefix}-{timestamp}-{random}";

            // Validate the generated SKU
            ValidateSkuAsync();
        }

        private async void txtSku_LostFocus(object sender, RoutedEventArgs e)
        {
            await ValidateSkuAsync();
        }

        private async Task ValidateSkuAsync()
        {
            if (string.IsNullOrWhiteSpace(txtSku.Text) || _isValidatingSku) return;

            try
            {
                _isValidatingSku = true;
                txtSkuValidation.Visibility = Visibility.Visible;
                txtSkuValidation.Text = "Validating SKU...";
                txtSkuValidation.Foreground = System.Windows.Media.Brushes.Gray;

                bool isValid = await _ebayService.ValidateSkuAsync(_accountId, txtSku.Text);

                if (isValid)
                {
                    txtSkuValidation.Text = "✓ SKU is available";
                    txtSkuValidation.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    txtSkuValidation.Text = "✗ SKU already exists";
                    txtSkuValidation.Foreground = System.Windows.Media.Brushes.Red;
                    txtSku.BorderBrush = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                txtSkuValidation.Text = "Could not validate SKU";
                txtSkuValidation.Foreground = System.Windows.Media.Brushes.Orange;
            }
            finally
            {
                _isValidatingSku = false;
            }
        }
    }
}