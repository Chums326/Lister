using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Services.Marketplaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ChumsLister.Core.Services
{
    public class OrderTrackingService
    {
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private readonly ISettingsService _settingsService;
        private readonly string _currentUsername;

        public OrderTrackingService(
            IMarketplaceServiceFactory marketplaceFactory,
            ISettingsService settingsService)
        {
            _marketplaceFactory = marketplaceFactory;
            _settingsService = settingsService;
            _currentUsername = "CurrentUser"; // Replace with actual source of username if needed
        }

        public async Task<List<OrderSummary>> GetRecentOrdersAsync(int days = 30)
        {
            var allOrders = new List<OrderSummary>();
            var services = await _marketplaceFactory.GetAuthenticatedMarketplaceServicesAsync();

            foreach (var service in services)
            {
                try
                {
                    var startDate = DateTime.Now.AddDays(-days);
                    var orders = await service.GetRecentSalesAsync(startDate);

                    foreach (var order in orders)
                    {
                        allOrders.Add(new OrderSummary
                        {
                            OrderId = order.OrderId,
                            SKU = order.SKU ?? "", // USE THE SKU FROM THE MARKETPLACE SERVICE!
                            Platform = service.Name,
                            BuyerName = order.BuyerUsername,
                            ItemTitle = order.ItemTitle,
                            OrderDate = order.SaleDate,
                            OrderTotal = order.TotalAmount,
                            Status = GetNormalizedStatus(order.OrderStatus, order.PaymentStatus, order.ShippingStatus),
                            PaymentStatus = order.PaymentStatus,
                            ShippingStatus = order.ShippingStatus,
                            Username = _currentUsername
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting orders from {service.Name}: {ex.Message}");
                }
            }

            return allOrders.OrderByDescending(o => o.OrderDate).ToList();
        }

        public async Task<OrderDetails> GetOrderDetailsAsync(OrderSummary summary)
        {
            // First, create the basic details from the summary
            var orderDetails = new OrderDetails
            {
                OrderId = summary.OrderId,
                SKU = summary.SKU,
                Platform = summary.Platform,
                BuyerName = summary.BuyerName,
                ItemTitle = summary.ItemTitle,
                OrderDate = summary.OrderDate,
                OrderTotal = summary.OrderTotal,
                Status = summary.Status,
                PaymentStatus = summary.PaymentStatus,
                ShippingStatus = summary.ShippingStatus,
                Username = summary.Username,
                Notes = new List<OrderNote>()
            };

            // Try to get more details from the marketplace service
            try
            {
                var service = _marketplaceFactory.GetMarketplaceService(summary.Platform);  // Changed this line - removed 'await' and 'Async'
                if (service != null && service is EbayMarketplaceService ebayService)
                {
                    // Call a method to get full order details from eBay
                    var fullDetails = await ebayService.GetOrderDetailsAsync(summary.OrderId);
                    if (fullDetails != null)
                    {
                        // Update with real data from eBay
                        orderDetails.BuyerAddress = fullDetails.BuyerAddress;
                        orderDetails.BuyerEmail = fullDetails.BuyerEmail;
                        orderDetails.BuyerPhone = fullDetails.BuyerPhone;
                        orderDetails.TrackingNumber = fullDetails.TrackingNumber ?? "Not yet available";
                        orderDetails.ShippingCarrier = fullDetails.ShippingCarrier;
                        orderDetails.ShippedDate = fullDetails.ShippedDate;
                        orderDetails.PaymentStatus = fullDetails.PaymentStatus;  // Update with real payment status
                        orderDetails.ShippingStatus = fullDetails.ShippingStatus;  // Update with real shipping status

                        // Add real order notes/status changes
                        if (!string.IsNullOrEmpty(orderDetails.PaymentStatus))
                        {
                            orderDetails.Notes.Add(new OrderNote
                            {
                                Timestamp = orderDetails.OrderDate,
                                Text = $"Order placed. Payment status: {orderDetails.PaymentStatus}",
                                Username = "System"
                            });
                        }

                        if (!string.IsNullOrEmpty(orderDetails.ShippingStatus))
                        {
                            string statusText = orderDetails.ShippingStatus.ToLower() switch
                            {
                                "notshipped" => "Awaiting shipment",
                                "shipped" => "Order shipped",
                                _ => $"Shipping status: {orderDetails.ShippingStatus}"
                            };

                            orderDetails.Notes.Add(new OrderNote
                            {
                                Timestamp = orderDetails.ShippedDate ?? DateTime.Now,
                                Text = statusText,
                                Username = "System"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting full order details: {ex.Message}");
                // Fall back to basic details
                orderDetails.BuyerAddress = "Address not available";
                orderDetails.TrackingNumber = "Not yet available";
                orderDetails.Notes.Add(new OrderNote
                {
                    Timestamp = DateTime.Now,
                    Text = "Unable to retrieve full order details",
                    Username = "System"
                });
            }

            return orderDetails;
        }

        // In OrderTrackingService.cs, replace the GetNormalizedStatus method with this:

        private string GetNormalizedStatus(string orderStatus, string paymentStatus, string shippingStatus)
        {
            // Debug output to see what statuses we're getting
            System.Diagnostics.Debug.WriteLine($"Order Status: '{orderStatus}', Payment Status: '{paymentStatus}', Shipping Status: '{shippingStatus}'");

            // Check order status first for cancelled orders
            if (orderStatus?.ToLower() == "cancelled" || orderStatus?.ToLower() == "canceled")
                return "Cancelled";

            // Check if payment is pending
            if (paymentStatus?.ToLower() == "pending" || paymentStatus?.ToLower() == "notpaid")
                return "Payment Pending";

            // Check shipping status
            if (!string.IsNullOrWhiteSpace(shippingStatus))
            {
                var shipStatus = shippingStatus.ToLower();

                if (shipStatus == "shipped" || shipStatus == "delivered")
                    return "Shipped";

                if (shipStatus == "notshipped" || shipStatus == "pending")
                    return "Ready to Ship";
            }

            // If payment is completed but no shipping status, it's ready to ship
            if (paymentStatus?.ToLower() == "completed" || paymentStatus?.ToLower() == "paid" || paymentStatus?.ToLower() == "complete")
            {
                // If we have no shipping status or it's empty, assume ready to ship
                if (string.IsNullOrWhiteSpace(shippingStatus))
                    return "Ready to Ship";
            }

            // Check eBay's OrderStatus field
            if (!string.IsNullOrWhiteSpace(orderStatus))
            {
                var status = orderStatus.ToLower();

                if (status == "completed")
                {
                    // For eBay, "Completed" usually means payment received but not necessarily shipped
                    // Check if we have shipping info
                    if (string.IsNullOrWhiteSpace(shippingStatus) || shippingStatus.ToLower() == "notshipped")
                        return "Ready to Ship";
                    else
                        return "Completed";
                }

                if (status == "active")
                    return "Processing";
            }

            // Default status
            return "Processing";
        }

        public async Task<bool> UpdateTrackingNumberAsync(string orderId, string platform, string trackingNumber, string shippingCarrier)
        {
            try
            {
                var service = _marketplaceFactory.GetMarketplaceService(platform);
                if (service == null) return false;

                System.Diagnostics.Debug.WriteLine($"Updated tracking for {platform} order {orderId}: {trackingNumber} via {shippingCarrier}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating tracking number: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MarkOrderAsShippedAsync(string orderId, string platform)
        {
            try
            {
                var service = _marketplaceFactory.GetMarketplaceService(platform);
                if (service == null) return false;

                System.Diagnostics.Debug.WriteLine($"Marked {platform} order {orderId} as shipped");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking order as shipped: {ex.Message}");
                return false;
            }
        }
    }

    public class OrderSummary : INotifyPropertyChanged
    {
        private string _sku;

        public string OrderId { get; set; }

        public string SKU
        {
            get => _sku;
            set
            {
                _sku = value;
                OnPropertyChanged(nameof(SKU));
            }
        }

        public string Platform { get; set; }
        public string BuyerName { get; set; }
        public string ItemTitle { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal OrderTotal { get; set; }
        public string Status { get; set; }
        public string PaymentStatus { get; set; }
        public string ShippingStatus { get; set; }
        public string Username { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


    public class OrderDetails : OrderSummary
    {
        public string BuyerAddress { get; set; }
        public string BuyerEmail { get; set; }
        public string BuyerPhone { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public decimal ShippingCost { get; set; }
        public decimal TaxAmount { get; set; }
        public string TrackingNumber { get; set; }
        public string ShippingCarrier { get; set; }
        public DateTime? ShippedDate { get; set; }
        public List<OrderNote> Notes { get; set; } = new List<OrderNote>();
    }

    public class OrderItem
    {
        public string SKU { get; set; }
        public string Title { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class OrderNote
    {
        public DateTime Timestamp { get; set; }
        public string Text { get; set; }
        public string Username { get; set; }
    }
}
