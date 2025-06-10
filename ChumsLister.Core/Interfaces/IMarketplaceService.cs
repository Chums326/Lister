// File: ChumsLister.Core/Interfaces/IMarketplaceService.cs
using ChumsLister.Core.Models;
using ChumsLister.Core.Services;
using ChumsLister.Core.Services.Marketplaces;

namespace ChumsLister.Core.Interfaces
{
    public interface IMarketplaceService
    {
        string Name { get; }
        string PlatformName { get; } // Add this property
        bool IsConfigured { get; }
        bool IsAuthenticated { get; }
        bool SupportsDraftListings { get; } // Add this property


        // Add this to your IMarketplaceService interface:
        Task<OrderDetails> GetOrderDetailsAsync(string orderId);
        Task<bool> AuthenticateAsync(string apiKey, string secretKey = null);
        Task<bool> IsAuthenticatedAsync(); // Add this method
        Task<List<MarketplaceListingSummary>> GetActiveListingsAsync();
        Task<ProductData> GetListingDetailsAsync(string listingId);
        Task<MarketplaceListingResult> CreateListingAsync(ProductData product);
        Task<MarketplaceListingResult> UpdateListingAsync(string listingId, ListingWizardData updateData);
        Task<bool> DeleteListingAsync(string listingId);
        bool ValidateProductData(ProductData product, out List<string> missingFields);
        Task<MarketplaceListingResult> PublishDraftListingAsync(string draftId); // Add this method
        Task<List<MarketplaceOrderSummary>> GetRecentSalesAsync(DateTime startDate);
    }
}