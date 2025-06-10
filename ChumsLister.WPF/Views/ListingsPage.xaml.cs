using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using ChumsLister.WPF.Views.Wizards;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace ChumsLister.WPF.Views
{
    public partial class ListingsPage : Page
    {
        private readonly IProductScraper _scraper;
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private readonly IAIService _aiService;
        private readonly IServiceProvider _serviceProvider;
        private ObservableCollection<ListingData> _listings;

        public ListingsPage(
            IProductScraper scraper,
            IMarketplaceServiceFactory marketplaceFactory = null,
            IAIService aiService = null,
            IServiceProvider serviceProvider = null)
        {
            InitializeComponent();
            _scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            _marketplaceFactory = marketplaceFactory;
            _aiService = aiService;
            _serviceProvider = serviceProvider;
            _listings = new ObservableCollection<ListingData>();

            // Set the ItemsSource
            listingsDataGrid.ItemsSource = _listings;

            // Load listings data
            LoadListingsData();

            Debug.WriteLine($"ListingsPage constructor received AI service: {_aiService?.GetType()?.FullName ?? "null"}");
        }

        private async void LoadListingsData()
        {
            try
            {
                // Show loading indicator
                loadingSpinner.Visibility = Visibility.Visible;

                // Clear existing items
                _listings.Clear();

                // Load real listings if we have marketplace services
                if (_marketplaceFactory != null)
                {
                    var marketplaceServices = await _marketplaceFactory.GetAuthenticatedMarketplaceServicesAsync();

                    foreach (var service in marketplaceServices)
                    {
                        var listings = await service.GetActiveListingsAsync();

                        foreach (var listing in listings)
                        {
                            _listings.Add(new ListingData
                            {
                                Title = listing.Title,
                                Price = listing.Price,
                                Platform = service.Name,
                                ListingId = listing.ListingId,
                                Status = listing.Status,
                                DateListed = listing.DateListed
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Not connected: {ex.Message}");
                System.Windows.MessageBox.Show($"Not connected: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading indicator
                loadingSpinner.Visibility = Visibility.Collapsed;
            }
        }


        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadListingsData();
        }

        private async void btnCreateListing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get eBay service



                var ebayService = _serviceProvider?.GetService<IEbayService>();
                if (ebayService == null)
                {
                    // Fallback to getting it from the marketplace factory
                    var marketplaceService = _marketplaceFactory?.GetMarketplaceService("eBay");
                    if (marketplaceService is IEbayService ebayServiceFromFactory)
                    {
                        ebayService = ebayServiceFromFactory;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("eBay service not available. Please check your configuration.",
                            "Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Check eBay authentication first
                var isAuthenticated = await ebayService.IsAuthenticatedAsync();
                if (!isAuthenticated)
                {
                    var result = System.Windows.MessageBox.Show(
                        "You need to authenticate with eBay before creating listings.\n\nWould you like to authenticate now?",
                        "eBay Authentication Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var authWindow = new EbayAuthWindow(ebayService);
                        if (authWindow.ShowDialog() != true)
                        {
                            return; // User cancelled authentication
                        }
                    }
                    else
                    {
                        return; // User chose not to authenticate
                    }
                }

                // Create a product data object from the currently displayed data
                var product = new ProductData
                {
                    Title = txtTitle.Text,
                    ModelNumber = txtModelNumber.Text,
                    BrandModel = txtBrand.Text,
                    Description = txtDescription.Text,
                    Features = txtFeatures.Text,
                    Specifications = txtSpecifications.Text,
                    Dimensions = txtDimensions.Text,
                    Price = decimal.TryParse(txtPrice.Text, out var price) ? price : 0m,
                    Condition = txtCondition.Text,
                    ItemType = txtItemType.Text,
                    ItemSpecifics = new Dictionary<string, string>()
                };

                // Copy item specifics if available
                if (gridItemSpecifics.ItemsSource is List<KeyValuePair<string, string>> itemSpecificsList)
                {
                    foreach (var kv in itemSpecificsList)
                    {
                        product.ItemSpecifics[kv.Key] = kv.Value;
                    }
                }

                Debug.WriteLine($"ListingsPage _aiService before creating wizard: {_aiService?.GetType()?.FullName ?? "null"}");

                // Create the wizard (it will automatically handle account selection)
                var wizard = new WizardWindow(_scraper, ebayService, _aiService, product);
                Debug.WriteLine($"Created wizard with eBay service");

                var wizardResult = wizard.ShowDialog();

                if (wizardResult == true)
                {
                    System.Windows.MessageBox.Show($"Listing created successfully!",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadListingsData(); // Refresh the listings
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating wizard: {ex.GetType().Name}: {ex.Message}");
                System.Windows.MessageBox.Show($"Error creating wizard: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateWizardDirectly(ProductData product)
        {
            Debug.WriteLine("Creating wizard directly - this method should not be used with eBay integration");
            System.Windows.MessageBox.Show("Direct wizard creation is not supported. Please use the service provider.",
                "Not Supported", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void btnViewListing_Click(object sender, RoutedEventArgs e)
        {
            // Get selected listing
            if (listingsDataGrid.SelectedItem is ListingData selectedListing)
            {
                // Load listing details
                LoadListingDetails(selectedListing);
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a listing to view", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void LoadListingDetails(ListingData listing)
        {
            try
            {
                // Show loading indicator
                detailsLoadingSpinner.Visibility = Visibility.Visible;

                // If we have marketplace services, load detailed listing
                if (_marketplaceFactory != null)
                {
                    var service = _marketplaceFactory.GetMarketplaceService(listing.Platform);

                    if (service != null)
                    {
                        var detailedListing = await service.GetListingDetailsAsync(listing.ListingId);

                        if (detailedListing != null)
                        {
                            // Update UI with listing details
                            txtTitle.Text = detailedListing.Title;
                            txtModelNumber.Text = detailedListing.ModelNumber;
                            txtDescription.Text = detailedListing.Description;
                            txtFeatures.Text = detailedListing.Features;
                            txtSpecifications.Text = detailedListing.Specifications;
                            txtDimensions.Text = detailedListing.Dimensions;
                            txtPrice.Text = detailedListing.Price.ToString("F2", CultureInfo.InvariantCulture);

                            txtCondition.Text = detailedListing.Condition;
                            txtItemType.Text = detailedListing.ItemType;

                            // Load item specifics
                            if (detailedListing.ItemSpecifics != null)
                            {
                                var itemSpecificsList = new List<KeyValuePair<string, string>>();

                                foreach (var kv in detailedListing.ItemSpecifics)
                                {
                                    itemSpecificsList.Add(new KeyValuePair<string, string>(kv.Key, kv.Value));
                                }

                                gridItemSpecifics.ItemsSource = itemSpecificsList;
                            }
                        }
                    }
                }
                else
                {
                    // If no marketplace service, just load basic data
                    txtTitle.Text = listing.Title;
                    txtPrice.Text = listing.Price.ToString("F2"); // ✅ FIXED
                                                                  // Other fields will be empty
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading listing details: {ex.Message}");
                System.Windows.MessageBox.Show($"Error loading listing details: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading indicator
                detailsLoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }


        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            // Get selected listing
            if (listingsDataGrid.SelectedItem is ListingData selectedListing)
            {
                // Confirm deletion
                var result = System.Windows.MessageBox.Show($"Are you sure you want to delete the listing '{selectedListing.Title}'?",
                    "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool success = false;

                        // Delete from marketplace if possible
                        if (_marketplaceFactory != null)
                        {
                            var service = _marketplaceFactory.GetMarketplaceService(selectedListing.Platform);

                            if (service != null)
                            {
                                success = service.DeleteListingAsync(selectedListing.ListingId).Result;
                            }
                        }
                        else
                        {
                            // Mock success if no marketplace service
                            success = true;
                        }

                        if (success)
                        {
                            _listings.Remove(selectedListing);
                            System.Windows.MessageBox.Show("Listing deleted successfully!", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            // Clear UI
                            ClearListingDetails();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting listing: {ex.Message}");
                        System.Windows.MessageBox.Show($"Error deleting listing: {ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a listing to delete", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearListingDetails()
        {
            txtTitle.Text = string.Empty;
            txtModelNumber.Text = string.Empty;
            txtDescription.Text = string.Empty;
            txtFeatures.Text = string.Empty;
            txtSpecifications.Text = string.Empty;
            txtDimensions.Text = string.Empty;
            txtPrice.Text = string.Empty;
            txtCondition.Text = string.Empty;
            txtItemType.Text = string.Empty;
            gridItemSpecifics.ItemsSource = null;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Implement search functionality
            string searchText = txtSearch.Text.ToLower();
            var view = CollectionViewSource.GetDefaultView(listingsDataGrid.ItemsSource);

            if (view != null)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item is ListingData listingData)
                        {
                            return listingData.Title?.ToLower().Contains(searchText) == true ||
                                   listingData.ListingId?.ToLower().Contains(searchText) == true ||
                                   listingData.Platform?.ToLower().Contains(searchText) == true;
                        }
                        return false;
                    };
                }
            }
        }

        // Event handlers referenced in XAML that were missing
        private async void btnScrape_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProductUrl.Text))
            {
                System.Windows.MessageBox.Show("Please enter a product URL", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Scrape product data
                var product = await _scraper.ScrapeProductDataAsync(txtProductUrl.Text);

                if (product != null)
                {
                    // Populate fields with scraped data
                    txtTitle.Text = product.Title;
                    txtModelNumber.Text = product.ModelNumber;
                    txtDescription.Text = product.Description;
                    txtFeatures.Text = product.Features;
                    txtSpecifications.Text = product.Specifications;
                    txtDimensions.Text = product.Dimensions;
                    txtPrice.Text = product.Price.ToString();
                    txtCondition.Text = product.Condition;
                    txtItemType.Text = product.ItemType;

                    System.Windows.MessageBox.Show("Product data successfully scraped!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("Could not scrape product data from the provided URL.",
                        "Scraping Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in scraping: {ex.Message}");
                System.Windows.MessageBox.Show($"Error scraping product data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        }

        private async void btnEnhanceDescription_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDescription.Text))
            {
                System.Windows.MessageBox.Show("Please enter a description first", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Create a product data object with current values
                var product = new ProductData
                {
                    Title = txtTitle.Text,
                    BrandModel = "", // Assuming you have this field somewhere
                    ModelNumber = txtModelNumber.Text,
                    Description = txtDescription.Text,
                    Features = txtFeatures.Text,
                    Specifications = txtSpecifications.Text,
                    ItemType = txtItemType.Text,
                    Condition = txtCondition.Text
                };

                // Call AI service to enhance description
                string enhancedDescription = await _aiService.EnhanceDescriptionAsync(product);

                if (!string.IsNullOrEmpty(enhancedDescription))
                {
                    string originalDescription = txtDescription.Text;
                    txtDescription.Text = enhancedDescription;

                    var result = System.Windows.MessageBox.Show("AI has enhanced your description. Would you like to keep it?",
                        "AI Enhancement", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        txtDescription.Text = originalDescription;
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("AI enhancement returned empty results. Please try again.",
                        "Enhancement Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AI enhancement: {ex.Message}");
                System.Windows.MessageBox.Show($"Error enhancing description: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        }

        private async void btnOptimizeTitle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                System.Windows.MessageBox.Show("Please enter a title first", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Create a product data object with current values
                var product = new ProductData
                {
                    Title = txtTitle.Text,
                    BrandModel = "", // Assuming you have this field somewhere
                    ModelNumber = txtModelNumber.Text,
                    Description = txtDescription.Text,
                    Features = txtFeatures.Text,
                    Specifications = txtSpecifications.Text,
                    ItemType = txtItemType.Text,
                    Condition = txtCondition.Text
                };

                // Call AI service to optimize title
                string optimizedTitle = await _aiService.SuggestTitleAsync(product);

                if (!string.IsNullOrEmpty(optimizedTitle))
                {
                    string originalTitle = txtTitle.Text;
                    txtTitle.Text = optimizedTitle;

                    var result = System.Windows.MessageBox.Show("AI has optimized your title. Would you like to keep it?",
                        "AI Optimization", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        txtTitle.Text = originalTitle;
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("AI optimization returned empty results. Please try again.",
                        "Optimization Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AI optimization: {ex.Message}");
                System.Windows.MessageBox.Show($"Error optimizing title: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
        }

        private void btnSaveListing_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtDescription.Text))
            {
                System.Windows.MessageBox.Show("Please fill in at least the title and description",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create a product data object from the currently displayed data
                var product = new ProductData
                {
                    Title = txtTitle.Text,
                    ModelNumber = txtModelNumber.Text,
                    Description = txtDescription.Text,
                    Features = txtFeatures.Text,
                    Specifications = txtSpecifications.Text,
                    Dimensions = txtDimensions.Text,
                    Price = decimal.TryParse(txtPrice.Text, out var price) ? price : 0m,
                    Condition = txtCondition.Text,
                    ItemType = txtItemType.Text,
                    ItemSpecifics = new Dictionary<string, string>()
                };

                // Copy item specifics if available
                if (gridItemSpecifics.ItemsSource is List<KeyValuePair<string, string>> itemSpecificsList)
                {
                    foreach (var kv in itemSpecificsList)
                    {
                        product.ItemSpecifics[kv.Key] = kv.Value;
                    }
                }

                // Save the listing to local database or as a draft
                // Implement your saving logic here

                System.Windows.MessageBox.Show("Listing saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving listing: {ex.Message}");
                System.Windows.MessageBox.Show($"Error saving listing: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Helper class for listing data
    public class ListingData
    {
        public string Title { get; set; }
        public decimal Price { get; set; }
        public string Platform { get; set; }
        public string ListingId { get; set; }
        public string Status { get; set; }
        public DateTime DateListed { get; set; }
    }
}