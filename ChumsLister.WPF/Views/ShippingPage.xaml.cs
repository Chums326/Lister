using System;
using System.Windows;
using System.Windows.Controls;
using ChumsLister.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ChumsLister.WPF.Views
{
    public partial class ShippingPage : Page
    {
        private readonly ShippingViewModel _viewModel;

        public ShippingPage(ShippingViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += async (s, e) =>
            {
                try
                {
                    await _viewModel.InitializeAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error initializing shipping page:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
        }

        public ShippingPage()
            : this(App.ServiceProvider.GetRequiredService<ShippingViewModel>())
        {
        }

        // Add these missing event handlers:

        private void ShippingType_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlPackageDetails != null)
            {
                pnlPackageDetails.Visibility = rbCalculatedShipping.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void btnAddDomesticService_Click(object sender, RoutedEventArgs e)
        {
            // This should be handled by the view model command
            // But if you need a code-behind handler, add logic here
        }

        private void btnRemoveDomesticService_Click(object sender, RoutedEventArgs e)
        {
            // This should be handled by the view model command
            // But if you need a code-behind handler, add logic here
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

        private void btnAddInternationalService_Click(object sender, RoutedEventArgs e)
        {
            // This should be handled by the view model command
            // But if you need a code-behind handler, add logic here
        }

        private void btnRemoveInternationalService_Click(object sender, RoutedEventArgs e)
        {
            // This should be handled by the view model command
            // But if you need a code-behind handler, add logic here
        }
    }
}