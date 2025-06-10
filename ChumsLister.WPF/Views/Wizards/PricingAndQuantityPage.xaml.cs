using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Added for Mouse and Cursors
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// Interaction logic for PricingAndQuantityPage.xaml
    /// </summary>
    public partial class PricingAndQuantityPage : Page, IWizardPage
    {
        private readonly IEbayService _ebayService;
        private readonly IAIService _aiService;
        private string _accountId;
        private string _categoryId;

        public PricingAndQuantityPage(IEbayService ebayService, IAIService aiService)
        {
            InitializeComponent();
            _ebayService = ebayService;
            _aiService = aiService;

            // Set default values
            cboListingType.SelectedIndex = 0; // Fixed Price
            cboDuration.SelectedIndex = 0; // GTC
            txtQuantity.Text = "1";
        }

        public bool ValidatePage()
        {
            // Validate based on listing type
            var selectedType = (cboListingType.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            if (selectedType == "FixedPriceItem" || selectedType == "StoresFixedPrice")
            {
                if (!ValidatePrice(txtBuyItNowPrice.Text, "Buy It Now price"))
                    return false;
            }
            else if (selectedType == "Chinese" || selectedType == "ChineseBuyItNow")
            {
                if (!ValidatePrice(txtStartingBid.Text, "Starting bid"))
                    return false;

                if (selectedType == "ChineseBuyItNow" && !ValidatePrice(txtBuyItNowPrice.Text, "Buy It Now price"))
                    return false;

                if (chkReservePrice.IsChecked == true && !ValidatePrice(txtReservePrice.Text, "Reserve price"))
                    return false;
            }

            // Validate quantity
            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity < 1)
            {
                System.Windows.MessageBox.Show("Please enter a valid quantity (minimum 1)", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validate lot size if applicable
            if (chkLotSize.IsChecked == true)
            {
                if (!int.TryParse(txtLotSize.Text, out int lotSize) || lotSize < 2)
                {
                    System.Windows.MessageBox.Show("Please enter a valid lot size (minimum 2)", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            // Validate Best Offer prices if enabled
            if (chkBestOffer.IsChecked == true)
            {
                decimal buyNowPrice = decimal.Parse(txtBuyItNowPrice.Text);

                if (!string.IsNullOrWhiteSpace(txtAutoAcceptPrice.Text))
                {
                    if (!decimal.TryParse(txtAutoAcceptPrice.Text, out decimal autoAccept) || autoAccept >= buyNowPrice)
                    {
                        System.Windows.MessageBox.Show("Auto-accept price must be less than Buy It Now price", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(txtAutoDeclinePrice.Text))
                {
                    if (!decimal.TryParse(txtAutoDeclinePrice.Text, out decimal autoDecline) || autoDecline >= buyNowPrice)
                    {
                        System.Windows.MessageBox.Show("Auto-decline price must be less than Buy It Now price", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ValidatePrice(string priceText, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(priceText))
            {
                System.Windows.MessageBox.Show($"Please enter a {fieldName}", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!decimal.TryParse(priceText, out decimal price) || price <= 0)
            {
                System.Windows.MessageBox.Show($"Please enter a valid {fieldName} greater than $0", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            // Save listing type
            var selectedType = (cboListingType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            listingData.ListingType = selectedType ?? "FixedPriceItem";

            // Save prices based on listing type
            if (selectedType == "FixedPriceItem" || selectedType == "StoresFixedPrice")
            {
                listingData.StartPrice = decimal.Parse(txtBuyItNowPrice.Text);
                listingData.BuyItNowPrice = null;
            }
            else if (selectedType == "Chinese")
            {
                listingData.StartPrice = decimal.Parse(txtStartingBid.Text);
                listingData.BuyItNowPrice = null;

                if (chkReservePrice.IsChecked == true)
                {
                    listingData.ReservePrice = decimal.Parse(txtReservePrice.Text);
                }
            }
            else if (selectedType == "ChineseBuyItNow")
            {
                listingData.StartPrice = decimal.Parse(txtStartingBid.Text);
                listingData.BuyItNowPrice = decimal.Parse(txtBuyItNowPrice.Text);

                if (chkReservePrice.IsChecked == true)
                {
                    listingData.ReservePrice = decimal.Parse(txtReservePrice.Text);
                }
            }

            // Save quantity and lot size
            listingData.Quantity = int.Parse(txtQuantity.Text);

            if (chkLotSize.IsChecked == true)
            {
                listingData.LotSize = int.Parse(txtLotSize.Text);
            }

            // Save duration
            var selectedDuration = (cboDuration.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            listingData.ListingDuration = selectedDuration ?? "GTC";

            // Save Best Offer settings
            listingData.BestOfferEnabled = chkBestOffer.IsChecked ?? false;

            if (listingData.BestOfferEnabled)
            {
                if (decimal.TryParse(txtAutoAcceptPrice.Text, out decimal autoAccept))
                {
                    listingData.BestOfferAutoAcceptPrice = autoAccept;
                }

                if (decimal.TryParse(txtAutoDeclinePrice.Text, out decimal autoDecline))
                {
                    listingData.BestOfferMinimumPrice = autoDecline;
                }
            }

            // Save other settings
            listingData.PrivateListing = chkPrivateListing.IsChecked ?? false;
            listingData.ImmediatePaymentRequired = chkImmediatePayment.IsChecked ?? false;
        }

        public void LoadData(ListingWizardData listingData)
        {
            _accountId = listingData.SelectedAccountId;
            _categoryId = listingData.PrimaryCategoryId;

            // Load listing type
            foreach (ComboBoxItem item in cboListingType.Items)
            {
                if (item.Tag?.ToString() == listingData.ListingType)
                {
                    cboListingType.SelectedItem = item;
                    break;
                }
            }

            // Load prices
            if (listingData.ListingType == "FixedPriceItem" || listingData.ListingType == "StoresFixedPrice")
            {
                txtBuyItNowPrice.Text = listingData.StartPrice.ToString("F2");
            }
            else if (listingData.ListingType == "Chinese")
            {
                txtStartingBid.Text = listingData.StartPrice.ToString("F2");

                if (listingData.ReservePrice.HasValue)
                {
                    chkReservePrice.IsChecked = true;
                    txtReservePrice.Text = listingData.ReservePrice.Value.ToString("F2");
                }
            }
            else if (listingData.ListingType == "ChineseBuyItNow")
            {
                txtStartingBid.Text = listingData.StartPrice.ToString("F2");
                txtBuyItNowPrice.Text = listingData.BuyItNowPrice?.ToString("F2") ?? "";

                if (listingData.ReservePrice.HasValue)
                {
                    chkReservePrice.IsChecked = true;
                    txtReservePrice.Text = listingData.ReservePrice.Value.ToString("F2");
                }
            }

            // Load quantity and lot size
            txtQuantity.Text = listingData.Quantity.ToString();

            if (listingData.LotSize.HasValue)
            {
                chkLotSize.IsChecked = true;
                txtLotSize.Text = listingData.LotSize.Value.ToString();
            }

            // Load duration
            foreach (ComboBoxItem item in cboDuration.Items)
            {
                if (item.Tag?.ToString() == listingData.ListingDuration)
                {
                    cboDuration.SelectedItem = item;
                    break;
                }
            }

            // Load Best Offer settings
            chkBestOffer.IsChecked = listingData.BestOfferEnabled;

            if (listingData.BestOfferEnabled)
            {
                if (listingData.BestOfferAutoAcceptPrice.HasValue)
                {
                    txtAutoAcceptPrice.Text = listingData.BestOfferAutoAcceptPrice.Value.ToString("F2");
                }

                if (listingData.BestOfferMinimumPrice.HasValue)
                {
                    txtAutoDeclinePrice.Text = listingData.BestOfferMinimumPrice.Value.ToString("F2");
                }
            }

            // Load other settings
            chkPrivateListing.IsChecked = listingData.PrivateListing;
            chkImmediatePayment.IsChecked = listingData.ImmediatePaymentRequired;
        }

        private void cboListingType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlFixedPrice == null || pnlAuction == null) return;

            var selectedType = (cboListingType.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            // Show/hide appropriate panels
            if (selectedType == "FixedPriceItem" || selectedType == "StoresFixedPrice")
            {
                pnlFixedPrice.Visibility = Visibility.Visible;
                pnlAuction.Visibility = Visibility.Collapsed;

                // Best Offer is available for fixed price
                chkBestOffer.IsEnabled = true;

                // Immediate payment is available
                chkImmediatePayment.IsEnabled = true;
            }
            else if (selectedType == "Chinese")
            {
                pnlFixedPrice.Visibility = Visibility.Collapsed;
                pnlAuction.Visibility = Visibility.Visible;

                // Hide Buy It Now price for pure auction
                var buyNowLabel = pnlAuction.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Text == "Buy It Now Price:");
                if (buyNowLabel != null)
                {
                    buyNowLabel.Visibility = Visibility.Collapsed;
                    // Also hide the associated grid
                    var grid = buyNowLabel.Parent as System.Windows.Controls.Panel;
                    if (grid != null)
                    {
                        var buyNowGrid = grid.Children.OfType<Grid>()
                            .FirstOrDefault(g => g.Children.OfType<System.Windows.Controls.TextBox>()
                            .Any(tb => tb.Name == "txtBuyItNowPrice"));
                        if (buyNowGrid != null)
                        {
                            buyNowGrid.Visibility = Visibility.Collapsed;
                        }
                    }
                }

                // Best Offer not available for auctions
                chkBestOffer.IsEnabled = false;
                chkBestOffer.IsChecked = false;

                // Immediate payment not available for auctions
                chkImmediatePayment.IsEnabled = false;
                chkImmediatePayment.IsChecked = false;
            }
            else if (selectedType == "ChineseBuyItNow")
            {
                pnlFixedPrice.Visibility = Visibility.Visible;
                pnlAuction.Visibility = Visibility.Visible;

                // Show Buy It Now price for auction with BIN
                var buyNowLabel = pnlAuction.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Text == "Buy It Now Price:");
                if (buyNowLabel != null)
                {
                    buyNowLabel.Visibility = Visibility.Visible;
                    var grid = buyNowLabel.Parent as System.Windows.Controls.Panel;
                    if (grid != null)
                    {
                        var buyNowGrid = grid.Children.OfType<Grid>()
                            .FirstOrDefault(g => g.Children.OfType<System.Windows.Controls.TextBox>()
                            .Any(tb => tb.Name == "txtBuyItNowPrice"));
                        if (buyNowGrid != null)
                        {
                            buyNowGrid.Visibility = Visibility.Visible;
                        }
                    }
                }

                // Best Offer not available
                chkBestOffer.IsEnabled = false;
                chkBestOffer.IsChecked = false;

                // Immediate payment available only for BIN portion
                chkImmediatePayment.IsEnabled = true;
            }

            // Update duration options based on listing type
            UpdateDurationOptions(selectedType);
        }

        private void UpdateDurationOptions(string listingType)
        {
            cboDuration.Items.Clear();

            if (listingType == "FixedPriceItem" || listingType == "StoresFixedPrice")
            {
                // Fixed price listings can be GTC or fixed duration
                cboDuration.Items.Add(new ComboBoxItem { Content = "Good 'Til Cancelled (GTC)", Tag = "GTC" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "30 Days", Tag = "Days_30" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "10 Days", Tag = "Days_10" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "7 Days", Tag = "Days_7" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "5 Days", Tag = "Days_5" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "3 Days", Tag = "Days_3" });
            }
            else
            {
                // Auctions have fixed durations only
                cboDuration.Items.Add(new ComboBoxItem { Content = "10 Days", Tag = "Days_10" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "7 Days", Tag = "Days_7" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "5 Days", Tag = "Days_5" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "3 Days", Tag = "Days_3" });
                cboDuration.Items.Add(new ComboBoxItem { Content = "1 Day", Tag = "Days_1" });
            }

            cboDuration.SelectedIndex = 0;
        }

        private void chkBestOffer_Checked(object sender, RoutedEventArgs e)
        {
            pnlBestOffer.Visibility = Visibility.Visible;
        }

        private void chkBestOffer_Unchecked(object sender, RoutedEventArgs e)
        {
            pnlBestOffer.Visibility = Visibility.Collapsed;
            txtAutoAcceptPrice.Text = "";
            txtAutoDeclinePrice.Text = "";
        }

        private void chkLotSize_Checked(object sender, RoutedEventArgs e)
        {
            pnlLotSize.Visibility = Visibility.Visible;
        }

        private void chkLotSize_Unchecked(object sender, RoutedEventArgs e)
        {
            pnlLotSize.Visibility = Visibility.Collapsed;
            txtLotSize.Text = "";
        }

        private async void btnResearchPricing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                txtPricingResearch.Text = "Researching similar items...";

                var wizard = Window.GetWindow(this) as WizardWindow;
                var wizardData = wizard?.WizardData;

                if (wizardData == null || string.IsNullOrEmpty(wizardData.Title))
                {
                    txtPricingResearch.Text = "Please complete the title before researching prices";
                    return;
                }

                // Here you would call eBay's Finding API or similar to get pricing data
                // For now, we'll simulate with some example data
                await Task.Delay(2000); // Simulate API call

                txtPricingResearch.Text = "Similar items found:\n" +
                    "• Average selling price: $45.99\n" +
                    "• Price range: $29.99 - $89.99\n" +
                    "• Most common price: $39.99\n" +
                    "• Sold in last 30 days: 47 items\n\n" +
                    "Recommendation: Price competitively at $39.99 - $49.99";
            }
            catch (Exception ex)
            {
                txtPricingResearch.Text = $"Error researching prices: {ex.Message}";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }
}