namespace ChumsLister.Core.Models
{
    public class SaleTransaction
    {
        public Guid TransId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<CartItem> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // or Card, etc.
    }
}
