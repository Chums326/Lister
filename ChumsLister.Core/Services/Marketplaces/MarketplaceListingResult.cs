// File: ChumsLister.Core/Services/Marketplaces/MarketplaceListingResult.cs
namespace ChumsLister.Core.Services.Marketplaces
{
    public class MarketplaceListingResult
    {
        public bool Success { get; set; }
        public string ListingId { get; set; }
        public string ListingUrl { get; set; }
        public string ErrorMessage { get; set; }
        public string Message { get; set; }
    }
}