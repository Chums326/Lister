namespace ChumsLister.Core.Services.Marketplaces
{
    public class MarketplaceSaleSummary
    {
        public string OrderId { get; set; }
        public string ListingId { get; set; }
        public string BuyerUsername { get; set; }
        public string ItemTitle { get; set; }
        public decimal SalePrice { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public int QuantitySold { get; set; }
        public DateTime SaleDate { get; set; }
        public string OrderStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string ShippingStatus { get; set; }
    }
}