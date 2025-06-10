using System.Collections.Generic;

namespace ChumsLister.Core.Models
{
    public class ListingDetailsDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public List<string> ImagePaths { get; set; } = new List<string>();
        public string Location { get; set; } = string.Empty;
        public string PayPalEmail { get; set; } = string.Empty;
        public bool OfferFreeShipping { get; set; }
        public decimal ShippingCost { get; set; }
        public Dictionary<string, string> ItemSpecifics { get; set; } = new Dictionary<string, string>();
    }
}