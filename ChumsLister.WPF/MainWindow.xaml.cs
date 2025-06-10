// File: MainWindow.xaml.cs

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;                              // WPF Button, Window, etc.
using ChumsLister.WPF.Views;
using ChumsLister.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

// Disambiguate between WinForms.Application vs WPF.Application,
// and WinForms.Button vs WPF.Button:
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;

namespace ChumsLister.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IProductScraper _productScraper;
        private readonly IServiceProvider _serviceProvider;
        private Button _currentActiveButton;

        /// <summary>
        /// Parameterless constructor so that WPF’s XAML loader can do "new MainWindow()".
        /// We simply delegate into the “real” constructor by pulling services out of App.ServiceProvider.
        /// </summary>
        public MainWindow()
            : this(
                  // Pull IProductScraper out of the static App.ServiceProvider:
                  App.ServiceProvider.GetRequiredService<IProductScraper>(),
                  // Pass along the entire IServiceProvider
                  App.ServiceProvider
              )
        {
            // No additional code here—everything is handled in the chained constructor below.
        }

        /// <summary>
        /// This is the “real” constructor that the DI container (or our parameterless ctor) calls.
        /// </summary>
        public MainWindow(IProductScraper productScraper, IServiceProvider serviceProvider)
        {
            _productScraper = productScraper;
            _serviceProvider = serviceProvider;

            try
            {
                InitializeComponent();

                // Immediately navigate to the Dashboard on startup
                NavigateToPage("Dashboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MainWindow initialization: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Error initializing main window: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// When the main window closes, shut down the entire application.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear saved credentials (your implementation)
            Helpers.AppSettings.ClearSavedCredentials();

            // Force reload inventory data for user change
            InventoryPage.ForceReloadForUserChange();

            // Show the login window again
            var loginWindow = new LoginPage();
            loginWindow.Show();

            // Close this window and exit so we can restart from scratch
            this.Close();
            Application.Current.Shutdown();

            // Restart the EXE
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
        }

        private void NavigateToPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag != null)
                {
                    string pageName = button.Tag.ToString();
                    NavigateToPage(pageName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NavigateToPage_Click: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Navigation error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void SetActiveButton(Button activeButton)
        {
            // Reset all sidebar button backgrounds to Transparent
            btnDashboard.Background = System.Windows.Media.Brushes.Transparent;
            btnListings.Background = System.Windows.Media.Brushes.Transparent;
            btnInventory.Background = System.Windows.Media.Brushes.Transparent;
            btnOrders.Background = System.Windows.Media.Brushes.Transparent;
            btnShipping.Background = System.Windows.Media.Brushes.Transparent;
            btnSettings.Background = System.Windows.Media.Brushes.Transparent;
            btnPOS.Background = System.Windows.Media.Brushes.Transparent;

            // Highlight the newly active button
            activeButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(255, 85, 85, 85)
            );
            _currentActiveButton = activeButton;
        }

        private void NavigateToPage(string pageName)
        {
            try
            {
                // If the current page is InventoryPage, save data before changing
                SaveInventoryDataIfNeeded();

                // Clear whatever was in the Frame before
                MainFrame.Content = null;

                Page page = null;
                switch (pageName)
                {
                    case "Dashboard":
                        page = _serviceProvider.GetService<DashboardPage>();
                        SetActiveButton(btnDashboard);
                        break;

                    case "Listings":
                        page = _serviceProvider.GetService<ListingsPage>();
                        SetActiveButton(btnListings);
                        break;

                    case "Inventory":
                        page = _serviceProvider.GetService<InventoryPage>();
                        SetActiveButton(btnInventory);
                        break;

                    case "Orders":
                        page = _serviceProvider.GetService<OrdersPage>();
                        SetActiveButton(btnOrders);
                        break;

                    case "Shipping":
                        page = _serviceProvider.GetService<ShippingPage>();
                        SetActiveButton(btnShipping);
                        break;

                    case "POS":
                        page = _serviceProvider.GetService<POSPage>();
                        SetActiveButton(btnPOS);
                        break;

                    case "Settings":
                        page = _serviceProvider.GetService<SettingsPage>();
                        SetActiveButton(btnSettings);
                        break;

                    default:
                        Debug.WriteLine($"Unknown page name: {pageName}");
                        page = _serviceProvider.GetService<DashboardPage>();
                        SetActiveButton(btnDashboard);
                        break;
                }

                if (page != null)
                {
                    MainFrame.Navigate(page);
                    this.Title = $"GRUFFIN - {pageName}";
                }
                else
                {
                    Debug.WriteLine($"Failed to create page: {pageName}");
                    System.Windows.MessageBox.Show(
                        $"Could not navigate to {pageName}. The page could not be created.",
                        "Navigation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NavigateToPage: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Navigation error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void SaveInventoryDataIfNeeded()
        {
            try
            {
                if (MainFrame.Content is InventoryPage inventoryPage)
                {
                    inventoryPage.OnNavigatingFrom();
                    Debug.WriteLine("Inventory data saved during navigation");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving inventory data: {ex.Message}");
                // Swallow this error so navigation isn’t blocked
            }
        }
    }
}
