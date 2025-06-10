using ChumsLister.Core.Models;
using ChumsLister.Core.Services.Marketplaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChumsLister.Core.Interfaces
{
    public interface IEbayService
    {
        // Authentication methods
        Task<List<EbayAccount>> GetAccountsAsync();
        Task<bool> AuthenticateAccountAsync(string accountId);
        Task<bool> IsAuthenticatedAsync(); 

        // Category methods
        Task<List<EbayCategory>> GetCategoriesAsync(string accountId, string parentId = null);
        Task<List<EbayCategory>> GetStoreCategoriesAsync(string accountId);
        Task<List<CategorySpecific>> GetCategorySpecificsAsync(string accountId, string categoryId);
        Task<List<ConditionType>> GetCategoryConditionsAsync(string accountId, string categoryId);
        Task<SuggestedCategory> SuggestCategoryAsync(string accountId, string title);

        // Listing methods
        Task<MarketplaceListingResult> CreateListingAsync(string accountId, ListingWizardData data);
        Task<bool> ValidateSkuAsync(string accountId, string sku);

        // Shipping methods
        Task<List<ShippingService>> GetShippingServicesAsync(string accountId, bool international = false);
    }

    public class EbayCategory
    {
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategoryParentId { get; set; }
        public int CategoryLevel { get; set; }
        public bool LeafCategory { get; set; }
        public bool AutoPayEnabled { get; set; }
        public bool BestOfferEnabled { get; set; }
    }

    public class CategorySpecific
    {
        public string Name { get; set; }
        public bool Required { get; set; }
        public string SelectionMode { get; set; } // FreeText, SelectionOnly
        public List<string> ValueRecommendations { get; set; }
        public int MaxValues { get; set; }
        public string HelpText { get; set; }
    }

    public class ConditionType
    {
        public string ConditionId { get; set; }
        public string ConditionDisplayName { get; set; }
    }

    public class SuggestedCategory
    {
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategoryPath { get; set; }
        public double PercentFound { get; set; }
    }
}