using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class WizardWindow : Window
    {
        private readonly IProductScraper _productScraper;
        private readonly IEbayService _ebayService;
        private readonly IAIService _aiService;

        private List<IWizardPage> _pages;
        private int _currentPageIndex = 0;

        public ListingWizardData WizardData { get; private set; }

        public WizardWindow(
            IProductScraper productScraper,
            IEbayService ebayService,
            IAIService aiService = null,
            ProductData initialProduct = null)
        {
            InitializeComponent();

            _productScraper = productScraper ?? throw new ArgumentNullException(nameof(productScraper));
            _ebayService = ebayService ?? throw new ArgumentNullException(nameof(ebayService));
            _aiService = aiService;

            WizardData = initialProduct != null
                ? ConvertProductDataToWizardData(initialProduct)
                : new ListingWizardData();

            // Initialize the wizard asynchronously
            Loaded += async (s, e) => await InitializeWizardAsync();
        }

        private async Task InitializeWizardAsync()
        {
            try
            {
                // Check eBay authentication and populate account info
                await PopulateAccountInfoAsync();

                // Initialize pages (without AccountSelectionPage)
                InitializePages();

                // Navigate to first page (CategorySelectionPage)
                NavigateToPage(0);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error initializing wizard: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async Task PopulateAccountInfoAsync()
        {
            try
            {
                var accounts = await _ebayService.GetAccountsAsync();
                var currentAccount = accounts.FirstOrDefault();

                if (currentAccount != null)
                {
                    WizardData.SelectedAccountId = currentAccount.Id;
                    WizardData.SelectedAccountName = currentAccount.Username;
                    Debug.WriteLine($"Auto-selected eBay account: {currentAccount.Username} (ID: {currentAccount.Id})");
                }
                else
                {
                    throw new Exception("No authenticated eBay account found. Please authenticate with eBay first.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get eBay account information: {ex.Message}");
            }
        }

        // Add SetProductData method that was referenced in ListingsPage
        public void SetProductData(ProductData product)
        {
            if (product == null) return;

            WizardData = ConvertProductDataToWizardData(product);

            // Reload current page if any
            if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
            {
                _pages[_currentPageIndex].LoadData(WizardData);
            }
        }

        private void InitializePages()
        {
            _pages = new List<IWizardPage>
            {
                // REMOVED: new AccountSelectionPage(_ebayService),
                new CategorySelectionPage(_ebayService),                     // Now index 0
                new TitleAndSkuPage(_productScraper, _aiService, _ebayService),  // Now index 1
                new ItemSpecificsPage(_ebayService),                         // Now index 2
                new ConditionAndDescriptionPage(_productScraper, _aiService, _ebayService), // Now index 3
                new ImagesPage(_productScraper, _aiService),                 // Now index 4
                new PricingAndQuantityPage(_ebayService, _aiService),        // Now index 5
                new ShippingPage(_ebayService),                              // Now index 6
                new ReturnsAndPaymentPage(_ebayService),                     // Now index 7
                new SummaryPage(_ebayService)                                // Now index 8
            };

            progressIndicator.Maximum = _pages.Count;
        }

        private ListingWizardData ConvertProductDataToWizardData(ProductData product)
        {
            var wizardData = new ListingWizardData
            {
                Title = product.Title,
                Brand = product.BrandModel,
                MPN = product.ModelNumber,
                Description = product.Description,
                StartPrice = product.Price,
                Quantity = product.AvailableQuantity > 0 ? product.AvailableQuantity : 1,
                CustomSku = product.Sku
            };

            // Convert any existing ItemSpecifics
            if (product.ItemSpecifics != null)
            {
                foreach (var kvp in product.ItemSpecifics)
                {
                    wizardData.ItemSpecifics[kvp.Key] = new List<string> { kvp.Value };
                }
            }

            // Convert any existing ImagePaths to ImageUrls
            if (product.ImagePaths != null)
            {
                wizardData.ImageUrls.AddRange(product.ImagePaths);
            }

            return wizardData;
        }

        private void NavigateToPage(int index)
        {
            if (index < 0 || index >= _pages.Count)
                return;

            _currentPageIndex = index;

            txtPageTitle.Text = GetPageTitle(index);
            lblProgressText.Text = $"Step {index + 1} of {_pages.Count}";
            progressIndicator.Value = index + 1;

            btnBack.IsEnabled = (index > 0);
            btnNext.Content = (index == _pages.Count - 1) ? "Create Listing" : "Next";

            contentFrame.Content = _pages[index];
            _pages[index].LoadData(WizardData);
        }

        private string GetPageTitle(int index)
        {
            return index switch
            {
                0 => "Choose Categories",           // Was index 1, now index 0
                1 => "Title and SKU",              // Was index 2, now index 1
                2 => "Item Specifics",             // Was index 3, now index 2
                3 => "Condition and Description",  // Was index 4, now index 3
                4 => "Add Images",                 // Was index 5, now index 4
                5 => "Set Pricing",                // Was index 6, now index 5
                6 => "Shipping Options",           // Was index 7, now index 6
                7 => "Returns and Payment",        // Was index 8, now index 7
                8 => "Review Listing",             // Was index 9, now index 8
                _ => "Listing Wizard"
            };
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            _pages[_currentPageIndex].SaveData(WizardData);
            NavigateToPage(_currentPageIndex - 1);
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (!_pages[_currentPageIndex].ValidatePage())
                return;

            _pages[_currentPageIndex].SaveData(WizardData);

            if (_currentPageIndex == _pages.Count - 1)
            {
                await FinishWizard();
                return;
            }

            NavigateToPage(_currentPageIndex + 1);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            var confirm = System.Windows.MessageBox.Show(
                "Are you sure you want to cancel? All unsaved changes will be lost.",
                "Cancel Wizard", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        private async Task FinishWizard()
        {
            try
            {
                loadingPanel.Visibility = Visibility.Visible;

                var result = await _ebayService.CreateListingAsync(
                    WizardData.SelectedAccountId,
                    WizardData);

                if (result.Success)
                {
                    System.Windows.MessageBox.Show(
                        $"Listing created successfully!\nListing ID: {result.ListingId}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to create listing:\n{result.ErrorMessage}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error creating listing: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                loadingPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}