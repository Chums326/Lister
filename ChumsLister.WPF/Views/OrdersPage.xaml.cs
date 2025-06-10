using ChumsLister.Core.Models;
using ChumsLister.Core.Services;
using ChumsLister.WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace ChumsLister.WPF.Views
{
    public partial class OrdersPage : Page
    {
        private readonly OrderTrackingService _orderTrackingService;
        private ObservableCollection<OrderSummary> _orders;
        private HashSet<string> _processedOrderHashes;
        private readonly string _hashFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "processed_orders.json");

        public OrdersPage(OrderTrackingService orderTrackingService)
        {
            InitializeComponent();
            _orderTrackingService = orderTrackingService;
            _orders = new ObservableCollection<OrderSummary>();
            ordersDataGrid.ItemsSource = _orders;

            // Use user-specific paths
            var userId = UserContext.CurrentUserId ?? "default";
            var userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChumsLister",
                "UserData"
            );
            Directory.CreateDirectory(userDataPath);

            _hashFilePath = Path.Combine(userDataPath, $"user_{userId}_processed_orders.json");

            LoadProcessedOrderHashes();
            this.Loaded += OrdersPage_Loaded;
        }

        private void LoadProcessedOrderHashes()
        {
            if (File.Exists(_hashFilePath))
            {
                var json = File.ReadAllText(_hashFilePath);
                _processedOrderHashes = JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
            }
            else
            {
                _processedOrderHashes = new HashSet<string>();
            }
        }

        private void SaveProcessedOrderHashes()
        {
            var json = JsonSerializer.Serialize(_processedOrderHashes);
            File.WriteAllText(_hashFilePath, json);
        }

        private async void OrdersPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Use user-specific inventory service
            var userId = UserContext.CurrentUserId ?? "default";
            
            await LoadOrdersAsync();
        }

        private async System.Threading.Tasks.Task LoadOrdersAsync()
        {
            try
            {
                loadingIndicator.Visibility = Visibility.Visible;
                _orders.Clear();

                var recentOrders = await _orderTrackingService.GetRecentOrdersAsync(30);
                var uniqueOrderIds = new HashSet<string>();

                Debug.WriteLine($"Retrieved {recentOrders.Count} orders from OrderTrackingService");

                foreach (var order in recentOrders)
                {
                    // Debug log the SKU before and after inventory lookup
                    Debug.WriteLine($"Order {order.OrderId}: SKU from eBay = '{order.SKU}', Title = '{order.ItemTitle}'");

                    

                    Debug.WriteLine($"Order {order.OrderId}: Final SKU = '{order.SKU}'");

                    if (!uniqueOrderIds.Contains(order.OrderId))
                    {
                        _orders.Add(order);
                        uniqueOrderIds.Add(order.OrderId);
                    }
                }

                noOrdersMessage.Visibility = _orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                // Debug log all orders with their SKUs
                Debug.WriteLine("=== All Orders in Grid ===");
                foreach (var order in _orders)
                {
                    Debug.WriteLine($"OrderId: {order.OrderId}, SKU: {order.SKU}, Title: {order.ItemTitle}");
                }
                Debug.WriteLine("=========================");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error in LoadOrdersAsync: {ex}");
            }
            finally
            {
                loadingIndicator.Visibility = Visibility.Collapsed;
            }
        }


        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadOrdersAsync();
        }

        private async void btnViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (ordersDataGrid.SelectedItem is OrderSummary selectedOrder)
            {
                try
                {
                    Debug.WriteLine($"Viewing details for Order {selectedOrder.OrderId}, SKU: {selectedOrder.SKU}");

                    var details = await _orderTrackingService.GetOrderDetailsAsync(selectedOrder);

                    Debug.WriteLine($"Order details retrieved - SKU: {details.SKU}");

                    var detailsWindow = new OrderDetailsWindow(details);
                    detailsWindow.ShowDialog();
                    await LoadOrdersAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to load order details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void btnCheckNewSales_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                loadingIndicator.Visibility = Visibility.Visible;
                var recentOrders = await _orderTrackingService.GetRecentOrdersAsync(30);

                int newSalesCount = 0;
                foreach (var order in recentOrders)
                {
                    string hash = $"{order.OrderId}-{order.OrderDate:yyyyMMdd}";
                    if (_processedOrderHashes.Contains(hash)) continue;

                    newSalesCount++;
                    Debug.WriteLine($"Processing new sale: Order {order.OrderId}, SKU: {order.SKU}");

                    

                    var item = InventoryService.Instance.InventoryItems
                        .FirstOrDefault(i => i.SKU.Equals(order.SKU, StringComparison.OrdinalIgnoreCase));

                    if (item != null && item.LOCATION == "listed")
                    {
                        item.LOCATION = "sold";
                        item.QTY = Math.Max(0, item.QTY - 1);
                        item.QTY_SOLD += 1;

                        string newPrice = order.OrderTotal.ToString("F2");
                        string newDate = order.OrderDate.ToString("MM/dd/yyyy");

                        item.SOLD_PRICE = order.OrderTotal;
                        item.DATE_SOLD = string.IsNullOrWhiteSpace(item.DATE_SOLD)
                            ? newDate : $"{item.DATE_SOLD},{newDate}";

                        InventoryRepository.UpdateItem(item);
                        Debug.WriteLine($"Updated inventory: {item.SKU}, marked as sold.");
                    }
                    else if (item == null && !string.IsNullOrWhiteSpace(order.SKU))
                    {
                        Debug.WriteLine($"WARNING: SKU {order.SKU} not found in inventory!");
                    }

                    if (!_orders.Any(o => o.OrderId == order.OrderId))
                    {
                        _orders.Add(order);
                    }

                    _processedOrderHashes.Add(hash);
                }

                SaveProcessedOrderHashes();
                System.Windows.MessageBox.Show($"Checked {recentOrders.Count} orders. Found {newSalesCount} new sales.", "Check Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error checking new sales: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error in btnCheckNewSales_Click: {ex}");
            }
            finally
            {
                loadingIndicator.Visibility = Visibility.Collapsed;
            }
        }
    }
}