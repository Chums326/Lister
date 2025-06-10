using System;
using System.Collections.Generic;

namespace ChumsLister.Core.Models
{
    // Required by DashboardViewModel for Sales data
    public class Sale
    {
        public string Id { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemTitle { get; set; } = string.Empty;
        public string BuyerId { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ItemPrice { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Tax { get; set; }
        public DateTime SaleDate { get; set; }
        public string Status { get; set; } = string.Empty; // "Completed", "Pending", "Cancelled"
        public string PaymentStatus { get; set; } = string.Empty;
        public string ShippingStatus { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string Platform { get; set; } = "eBay"; // Which marketplace
        public string Sku { get; set; } = string.Empty;
    }

    // Required by DashboardViewModel for Listings data
    public class Listing
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new List<string>();
        public string Status { get; set; } = string.Empty; // "Active", "Ended", "Sold"
        public DateTime CreatedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Quantity { get; set; }
        public int QuantitySold { get; set; }
        public int ViewCount { get; set; }
        public int WatcherCount { get; set; }
        public string Platform { get; set; } = "eBay";
        public string ListingUrl { get; set; } = string.Empty;
        public bool OfferFreeShipping { get; set; }
        public decimal ShippingCost { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    // Order model for tracking orders
    public class Order
    {
        public string Id { get; set; } = string.Empty;
        public string BuyerId { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public decimal TotalAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Tax { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty; // "Pending", "Paid", "Shipped", "Completed", "Cancelled"
        public string PaymentStatus { get; set; } = string.Empty;
        public string ShippingStatus { get; set; } = string.Empty;
        public Address ShippingAddress { get; set; } = new Address();
        public Address BillingAddress { get; set; } = new Address();
        public string Platform { get; set; } = "eBay";
        public string TrackingNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;

    }

    // Order item within an order
    public class OrderItem
    {
        public string ListingId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
    }

    // Address model for shipping/billing
    public class Address
    {
        public string Name { get; set; } = string.Empty;
        public string Street1 { get; set; } = string.Empty;
        public string Street2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}