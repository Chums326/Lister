using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// Interaction logic for ShippingPage.xaml in the Wizards folder
    /// </summary>
    public partial class ShippingPage : Page, IWizardPage
    {
        private readonly IEbayService _ebayService;
        private ShippingPageViewModel _viewModel;

        public ShippingPage(IEbayService ebayService)
        {
            InitializeComponent();

            _ebayService = ebayService ?? throw new ArgumentNullException(nameof(ebayService));

            // Initialize view model
            _viewModel = new ShippingPageViewModel();
            DataContext = _viewModel;

            // Initialize with some default data
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            // Set the default shipping type after all controls are loaded
            if (rbFlatShipping != null)
                rbFlatShipping.IsChecked = true;

            // Set default handling time
            if (cboHandlingTime != null && cboHandlingTime.Items.Count > 1)
                cboHandlingTime.SelectedIndex = 1; // 1 business day

            // Initialize grids with empty collections
            if (gridDomesticShipping != null)
                gridDomesticShipping.ItemsSource = _viewModel.DomesticShippingServices;

            if (gridInternationalShipping != null)
                gridInternationalShipping.ItemsSource = _viewModel.InternationalShippingServices;
        }

        #region IWizardPage Implementation

        public void LoadData(ListingWizardData data)
        {
            if (data == null) return;
            LoadFromWizardData(data);
        }

        public void SaveData(ListingWizardData data)
        {
            if (data == null) return;
            PopulateWizardData(data);
        }

        public bool ValidatePage()
        {
            // Check if at least one domestic shipping service is selected
            if (_viewModel.DomesticShippingServices.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Please add at least one domestic shipping service.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // If calculated shipping is selected, validate package details
            if (rbCalculatedShipping != null && rbCalculatedShipping.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(txtWeightLbs.Text) &&
                    string.IsNullOrWhiteSpace(txtWeightOz.Text))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter the package weight for calculated shipping.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(txtLength.Text) ||
                    string.IsNullOrWhiteSpace(txtWidth.Text) ||
                    string.IsNullOrWhiteSpace(txtHeight.Text))
                {
                    System.Windows.MessageBox.Show(
                        "Please enter the package dimensions for calculated shipping.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        #endregion

        private void ShippingType_Changed(object sender, RoutedEventArgs e)
        {
            // Add null checks to prevent errors during initialization
            if (pnlPackageDetails == null || rbCalculatedShipping == null)
                return;

            // Show package details only for calculated shipping
            pnlPackageDetails.Visibility = rbCalculatedShipping.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Update view model
            if (rbFlatShipping?.IsChecked == true) _viewModel.ShippingType = "Flat";
            else if (rbCalculatedShipping?.IsChecked == true) _viewModel.ShippingType = "Calculated";
            else if (rbFreightShipping?.IsChecked == true) _viewModel.ShippingType = "Freight";
            else if (rbLocalPickupOnly?.IsChecked == true) _viewModel.ShippingType = "LocalPickup";
        }

        private void chkInternationalShipping_Checked(object sender, RoutedEventArgs e)
        {
            if (pnlInternationalShipping != null)
                pnlInternationalShipping.Visibility = Visibility.Visible;
        }

        private void chkInternationalShipping_Unchecked(object sender, RoutedEventArgs e)
        {
            if (pnlInternationalShipping != null)
                pnlInternationalShipping.Visibility = Visibility.Collapsed;
        }

        private async void btnAddDomesticService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get available domestic shipping services from eBay
                var services = await GetDomesticShippingServices();

                var dialog = new ShippingServiceDialog(services, "Select Domestic Shipping Service");
                if (dialog.ShowDialog() == true && dialog.SelectedService != null)
                {
                    // Create a wrapper that includes FreeShipping property
                    var shippingServiceWrapper = new ShippingServiceViewModel
                    {
                        ShippingServiceCode = dialog.SelectedService.ShippingServiceCode,
                        ShippingServiceName = dialog.SelectedService.ShippingServiceName,
                        Cost = dialog.Cost,
                        AdditionalCost = dialog.AdditionalCost,
                        FreeShipping = dialog.Cost == 0
                    };
                    _viewModel.DomesticShippingServices.Add(shippingServiceWrapper);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding shipping service: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnAddInternationalService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get available international shipping services from eBay
                var services = await GetInternationalShippingServices();

                var dialog = new ShippingServiceDialog(services, "Select International Shipping Service");
                if (dialog.ShowDialog() == true && dialog.SelectedService != null)
                {
                    // Create a wrapper that includes FreeShipping property
                    var shippingServiceWrapper = new ShippingServiceViewModel
                    {
                        ShippingServiceCode = dialog.SelectedService.ShippingServiceCode,
                        ShippingServiceName = dialog.SelectedService.ShippingServiceName,
                        Cost = dialog.Cost,
                        AdditionalCost = dialog.AdditionalCost,
                        FreeShipping = dialog.Cost == 0
                    };
                    _viewModel.InternationalShippingServices.Add(shippingServiceWrapper);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding shipping service: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRemoveDomesticService_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var service = button?.CommandParameter as ShippingServiceViewModel;
            if (service != null)
            {
                _viewModel.DomesticShippingServices.Remove(service);
            }
        }

        private void btnRemoveInternationalService_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var service = button?.CommandParameter as ShippingServiceViewModel;
            if (service != null)
            {
                _viewModel.InternationalShippingServices.Remove(service);
            }
        }

        // Method to populate data into ListingWizardData
        private void PopulateWizardData(ListingWizardData wizardData)
        {
            // Shipping type
            wizardData.ShippingType = _viewModel.ShippingType;

            // Package details (for calculated shipping)
            if (rbCalculatedShipping.IsChecked == true)
            {
                wizardData.ShippingPackage = (cboPackageType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Package/Thick Envelope";

                // Parse weight
                if (decimal.TryParse(txtWeightLbs.Text, out decimal lbs) &&
                    decimal.TryParse(txtWeightOz.Text, out decimal oz))
                {
                    wizardData.PackageWeight = lbs + (oz / 16m);
                }

                // Parse dimensions
                if (decimal.TryParse(txtLength.Text, out decimal length) &&
                    decimal.TryParse(txtWidth.Text, out decimal width) &&
                    decimal.TryParse(txtHeight.Text, out decimal height))
                {
                    wizardData.PackageDimensions = new PackageDimensions
                    {
                        Length = length,
                        Width = width,
                        Height = height,
                        Unit = "inches"
                    };
                }
            }

            // Handling time
            var selectedHandlingTime = cboHandlingTime.SelectedItem as ComboBoxItem;
            if (selectedHandlingTime != null && int.TryParse(selectedHandlingTime.Tag?.ToString(), out int handlingDays))
            {
                wizardData.HandlingTime = handlingDays;
            }

            // Convert view model services back to ShippingService for wizard data
            wizardData.DomesticShippingServices = _viewModel.DomesticShippingServices
                .Select(vm => new ShippingService
                {
                    ShippingServiceCode = vm.ShippingServiceCode,
                    ShippingServiceName = vm.ShippingServiceName,
                    Cost = vm.Cost,
                    AdditionalCost = vm.AdditionalCost
                }).ToList();

            // International shipping
            if (chkInternationalShipping.IsChecked == true)
            {
                wizardData.InternationalShippingServices = _viewModel.InternationalShippingServices
                    .Select(vm => new ShippingService
                    {
                        ShippingServiceCode = vm.ShippingServiceCode,
                        ShippingServiceName = vm.ShippingServiceName,
                        Cost = vm.Cost,
                        AdditionalCost = vm.AdditionalCost
                    }).ToList();

                wizardData.GlobalShipping = chkGlobalShipping.IsChecked == true;

                // Excluded locations
                wizardData.ExcludeShipToLocations = listExcludeLocations.SelectedItems
                    .Cast<ListBoxItem>()
                    .Select(item => item.Content?.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
        }

        // Method to load data from ListingWizardData
        private void LoadFromWizardData(ListingWizardData wizardData)
        {
            // Shipping type
            switch (wizardData.ShippingType)
            {
                case "Flat":
                    rbFlatShipping.IsChecked = true;
                    break;
                case "Calculated":
                    rbCalculatedShipping.IsChecked = true;
                    break;
                case "Freight":
                    rbFreightShipping.IsChecked = true;
                    break;
                case "LocalPickup":
                    rbLocalPickupOnly.IsChecked = true;
                    break;
            }

            // Package details
            if (wizardData.PackageWeight > 0)
            {
                int lbs = (int)wizardData.PackageWeight;
                decimal oz = (wizardData.PackageWeight - lbs) * 16;
                txtWeightLbs.Text = lbs.ToString();
                txtWeightOz.Text = oz.ToString("0.##");
            }

            if (wizardData.PackageDimensions != null)
            {
                txtLength.Text = wizardData.PackageDimensions.Length.ToString();
                txtWidth.Text = wizardData.PackageDimensions.Width.ToString();
                txtHeight.Text = wizardData.PackageDimensions.Height.ToString();
            }

            // Handling time
            foreach (ComboBoxItem item in cboHandlingTime.Items)
            {
                if (item.Tag?.ToString() == wizardData.HandlingTime.ToString())
                {
                    cboHandlingTime.SelectedItem = item;
                    break;
                }
            }

            // Shipping services - convert to view model
            _viewModel.DomesticShippingServices.Clear();
            foreach (var service in wizardData.DomesticShippingServices)
            {
                _viewModel.DomesticShippingServices.Add(new ShippingServiceViewModel
                {
                    ShippingServiceCode = service.ShippingServiceCode,
                    ShippingServiceName = service.ShippingServiceName,
                    Cost = service.Cost ?? 0m,  // Handle nullable
                    AdditionalCost = service.AdditionalCost ?? 0m,  // Handle nullable
                    FreeShipping = (service.Cost ?? 0m) == 0m
                });
            }

            if (wizardData.InternationalShippingServices.Count > 0)
            {
                chkInternationalShipping.IsChecked = true;
                _viewModel.InternationalShippingServices.Clear();
                foreach (var service in wizardData.InternationalShippingServices)
                {
                    _viewModel.InternationalShippingServices.Add(new ShippingServiceViewModel
                    {
                        ShippingServiceCode = service.ShippingServiceCode,
                        ShippingServiceName = service.ShippingServiceName,
                        Cost = service.Cost ?? 0m,  // Handle nullable
                        AdditionalCost = service.AdditionalCost ?? 0m,  // Handle nullable
                        FreeShipping = (service.Cost ?? 0m) == 0m
                    });
                }
                chkGlobalShipping.IsChecked = wizardData.GlobalShipping;
            }
        }

        private async Task<List<ShippingService>> GetDomesticShippingServices()
        {
            try
            {
                // Try to get from eBay service first
                if (_ebayService != null)
                {
                    var accountId = "default"; // You might want to pass this from wizard data
                    var services = await _ebayService.GetShippingServicesAsync(accountId, false);
                    if (services != null && services.Count > 0)
                        return services;
                }
            }
            catch
            {
                // Fall back to static list if eBay service fails
            }

            // Static fallback list
            return new List<ShippingService>
            {
                new ShippingService { ShippingServiceCode = "USPSGroundAdvantage", ShippingServiceName = "USPS Ground Advantage" },
                new ShippingService { ShippingServiceCode = "USPSPriority", ShippingServiceName = "USPS Priority Mail" },
                new ShippingService { ShippingServiceCode = "USPSPriorityExpress", ShippingServiceName = "USPS Priority Mail Express" },
                new ShippingService { ShippingServiceCode = "UPSGround", ShippingServiceName = "UPS Ground" },
                new ShippingService { ShippingServiceCode = "UPS3DaySelect", ShippingServiceName = "UPS 3 Day Select" },
                new ShippingService { ShippingServiceCode = "UPS2ndDayAir", ShippingServiceName = "UPS 2nd Day Air" },
                new ShippingService { ShippingServiceCode = "UPSNextDayAir", ShippingServiceName = "UPS Next Day Air" },
                new ShippingService { ShippingServiceCode = "FedExGround", ShippingServiceName = "FedEx Ground" },
                new ShippingService { ShippingServiceCode = "FedExExpressSaver", ShippingServiceName = "FedEx Express Saver" },
                new ShippingService { ShippingServiceCode = "FedEx2Day", ShippingServiceName = "FedEx 2Day" },
                new ShippingService { ShippingServiceCode = "FedExStandardOvernight", ShippingServiceName = "FedEx Standard Overnight" }
            };
        }

        private async Task<List<ShippingService>> GetInternationalShippingServices()
        {
            try
            {
                // Try to get from eBay service first
                if (_ebayService != null)
                {
                    var accountId = "default"; // You might want to pass this from wizard data
                    var services = await _ebayService.GetShippingServicesAsync(accountId, true);
                    if (services != null && services.Count > 0)
                        return services;
                }
            }
            catch
            {
                // Fall back to static list if eBay service fails
            }

            // Static fallback list
            return new List<ShippingService>
            {
                new ShippingService { ShippingServiceCode = "USPSPriorityMailInternational", ShippingServiceName = "USPS Priority Mail International" },
                new ShippingService { ShippingServiceCode = "USPSFirstClassMailInternational", ShippingServiceName = "USPS First-Class Mail International" },
                new ShippingService { ShippingServiceCode = "USPSPriorityMailExpressInternational", ShippingServiceName = "USPS Priority Mail Express International" },
                new ShippingService { ShippingServiceCode = "UPSWorldwideExpress", ShippingServiceName = "UPS Worldwide Express" },
                new ShippingService { ShippingServiceCode = "UPSWorldwideExpedited", ShippingServiceName = "UPS Worldwide Expedited" },
                new ShippingService { ShippingServiceCode = "FedExInternationalPriority", ShippingServiceName = "FedEx International Priority" },
                new ShippingService { ShippingServiceCode = "FedExInternationalEconomy", ShippingServiceName = "FedEx International Economy" },
                new ShippingService { ShippingServiceCode = "eBayGlobalShippingProgram", ShippingServiceName = "eBay Global Shipping Program" }
            };
        }

        // View model wrapper for ShippingService that includes UI-specific properties
        private class ShippingServiceViewModel
        {
            public string ShippingServiceCode { get; set; }
            public string ShippingServiceName { get; set; }
            public decimal Cost { get; set; }
            public decimal AdditionalCost { get; set; }
            public bool FreeShipping { get; set; }
        }

        // Simple view model for the shipping page
        private class ShippingPageViewModel
        {
            public string ShippingType { get; set; } = "Flat";
            public ObservableCollection<ShippingServiceViewModel> DomesticShippingServices { get; set; }
            public ObservableCollection<ShippingServiceViewModel> InternationalShippingServices { get; set; }

            public ShippingPageViewModel()
            {
                DomesticShippingServices = new ObservableCollection<ShippingServiceViewModel>();
                InternationalShippingServices = new ObservableCollection<ShippingServiceViewModel>();
            }
        }
    }
}