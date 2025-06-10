using System;

namespace ChumsLister.Core.Models
{
    public class EbayAccount
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string SiteId { get; set; } = "0"; // US by default
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime TokenExpiration { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastUsed { get; set; }
        public string StoreName { get; set; }
    }

    public class ShippingService
    {
        public string ShippingServiceCode { get; set; }
        public string ShippingServiceName { get; set; }
        public decimal? Cost { get; set; }
        public decimal? AdditionalCost { get; set; }
        public bool IsInternational { get; set; }
    }

}