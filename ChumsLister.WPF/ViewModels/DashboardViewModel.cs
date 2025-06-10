using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Services.Marketplaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

// We need ICommand for the RefreshCommand:
using System.Windows.Input;

// Disambiguate 'Application' between WinForms vs WPF:
using Application = System.Windows.Application;

namespace ChumsLister.WPF.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private IMarketplaceService _ebayService;

        // These types must already be defined in their own files:
        //   - StatCardViewModel  (e.g. in StatCardViewModel.cs)
        //   - ActivityItemViewModel  (e.g. in ActivityItemViewModel.cs)

        public ObservableCollection<StatCardViewModel> StatCards { get; set; } = new();
        public ObservableCollection<ActivityItemViewModel> RecentActivities { get; set; } = new();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _hasLoaded = false;
        public ICommand RefreshCommand { get; }

        public DashboardViewModel(IMarketplaceServiceFactory marketplaceFactory)
        {
            _marketplaceFactory = marketplaceFactory;
            RefreshCommand = new AsyncRelayCommand(LoadDashboardAsync);

            // Populate stat cards & recent activities with placeholder text immediately:
            InitializePlaceholderData();
        }

        private void InitializePlaceholderData()
        {
            StatCards.Add(new StatCardViewModel("Total Sales", "Loading..."));
            StatCards.Add(new StatCardViewModel("Revenue", "Loading..."));
            StatCards.Add(new StatCardViewModel("Active Listings", "Loading..."));
            StatCards.Add(new StatCardViewModel("Pending Orders", "Loading..."));

            RecentActivities.Add(new ActivityItemViewModel
            {
                Title = "Loading recent activities...",
                Subtitle = "Please wait"
            });
        }

        /// <summary>
        /// Loads real data from eBay (total sales, revenue, active listings,
        /// pending orders, recent activities).  This method only runs once per App‐lifetime
        /// unless you explicitly click Refresh.
        /// </summary>
        public async Task LoadDashboardAsync()
        {
            if (_hasLoaded)
                return;

            _hasLoaded = true;
            try
            {
                IsLoading = true;
                _ebayService = _marketplaceFactory.GetMarketplaceService("eBay");

                // If we don't have an authenticated eBay service, show fallback data.
                if (_ebayService is not EbayMarketplaceService ebayService
                    || !await ebayService.IsAuthenticatedAsync())
                {
                    await LoadFallbackDataAsync();
                    return;
                }

                // 1) Fetch sales from the last 90 days
                var sales = await ebayService.GetRecentSalesAsync(DateTime.UtcNow.AddDays(-90));

                // 2) Fetch active listings count
                var listings = await ebayService.GetActiveListingsAsync();

                int totalSales = sales.Count;
                decimal totalRevenue = sales.Sum(s => s.TotalAmount);
                int totalListings = listings.Count;


                // 3) Compute "Ready to Ship" orders - matching the exact logic in OrderTrackingService.GetNormalizedStatus
                int readyToShipOrders = sales.Count(s =>
                {
                    var orderStatus = (s.OrderStatus ?? "").Trim().ToLowerInvariant();
                    var paymentStatus = (s.PaymentStatus ?? "").Trim().ToLowerInvariant();
                    var shippingStatus = (s.ShippingStatus ?? "").Trim().ToLowerInvariant();

                    // Skip cancelled orders
                    if (orderStatus == "cancelled" || orderStatus == "canceled")
                        return false;

                    // For eBay, "Completed" OrderStatus means payment received
                    if (orderStatus == "completed")
                    {
                        // Check if it's not shipped yet
                        if (string.IsNullOrWhiteSpace(shippingStatus) || shippingStatus == "notshipped" || shippingStatus == "pending")
                        {
                            return true;
                        }
                    }

                    // Also check traditional payment status (in case some orders have it)
                    bool isPaid = paymentStatus == "paid" || paymentStatus == "complete" || paymentStatus == "completed";

                    if (isPaid)
                    {
                        // Check shipping status
                        if (string.IsNullOrWhiteSpace(shippingStatus) || shippingStatus == "notshipped" || shippingStatus == "pending")
                        {
                            return true;
                        }
                    }

                    return false;
                });

                // 4) Marshal the results back onto the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatCards.Clear();
                    StatCards.Add(new StatCardViewModel("Total Sales", totalSales.ToString()));
                    StatCards.Add(new StatCardViewModel("Revenue", $"${totalRevenue:F2}"));
                    StatCards.Add(new StatCardViewModel("Active Listings", totalListings.ToString()));
                    StatCards.Add(new StatCardViewModel("Ready to Ship", readyToShipOrders.ToString()));

                    RecentActivities.Clear();
                    if (sales.Any())
                    {
                        foreach (var sale in sales
                                         .OrderByDescending(s => s.SaleDate)
                                         .Take(10))
                        {
                            RecentActivities.Add(new ActivityItemViewModel
                            {
                                Title = $"Sold: {sale.ItemTitle}",
                                Subtitle = $"${sale.TotalAmount:F2} on {sale.SaleDate:g}"
                            });
                        }
                    }
                    else
                    {
                        RecentActivities.Add(new ActivityItemViewModel
                        {
                            Title = "No recent sales",
                            Subtitle = "Sales will appear here once you start selling"
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
                await LoadFallbackDataAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// If we can’t talk to eBay (not authenticated or error),
        /// show zeros and a “not connected” message.
        /// </summary>
        private Task LoadFallbackDataAsync()
        {
            return Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatCards.Clear();
                    StatCards.Add(new StatCardViewModel("Total Sales", "0"));
                    StatCards.Add(new StatCardViewModel("Revenue", "$0.00"));
                    StatCards.Add(new StatCardViewModel("Active Listings", "0"));
                    StatCards.Add(new StatCardViewModel("Pending Orders", "0"));

                    RecentActivities.Clear();
                    RecentActivities.Add(new ActivityItemViewModel
                    {
                        Title = "No eBay connection",
                        Subtitle = "Connect your eBay account to see live data"
                    });
                });
            });
        }
    }
}