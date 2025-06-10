// File: ChumsLister.Core/Services/Marketplaces/MarketplaceOrderSummary.cs
namespace ChumsLister.Core.Services.Marketplaces
{
    public class MarketplaceOrderSummary
    {
        public string OrderId { get; set; }
        public string BuyerUsername { get; set; }
        public string ItemTitle { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string ShippingStatus { get; set; }
        public string SKU { get; set; }


    }
}