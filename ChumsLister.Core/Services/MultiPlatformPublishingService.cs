using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using ChumsLister.Core.Services.Marketplaces;
using ProductData = ChumsLister.Core.Models.ProductData;
using System.Linq;

namespace ChumsLister.Core.Services
{
    public class MultiPlatformPublishingService
    {
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private readonly ISettingsService _settingsService;

        public MultiPlatformPublishingService(
            IMarketplaceServiceFactory marketplaceFactory,
            ISettingsService settingsService)
        {
            _marketplaceFactory = marketplaceFactory;
            _settingsService = settingsService;
        }

        public async Task<Dictionary<string, MarketplaceListingResult>> PublishToMultiplePlatformsAsync(
            ListingWizardData listingData,
            List<string> platformNames)
        {
            var results = new Dictionary<string, MarketplaceListingResult>();

            foreach (var platformName in platformNames)
            {
                var marketplaceService = _marketplaceFactory.GetMarketplaceService(platformName);

                if (marketplaceService == null)
                {
                    results[platformName] = new MarketplaceListingResult
                    {
                        Success = false,
                        ErrorMessage = $"Marketplace service for {platformName} not found"
                    };
                    continue;
                }

                try
                {
                    // Check if authenticated
                    if (!await marketplaceService.IsAuthenticatedAsync())
                    {
                        results[platformName] = new MarketplaceListingResult
                        {
                            Success = false,
                            ErrorMessage = $"Not authenticated with {platformName}"
                        };
                        continue;
                    }

                    // Create listing
                    var productData = ConvertToProductData(listingData);
                    var result = await marketplaceService.CreateListingAsync(productData);
                    results[platformName] = result;

                    // Log the result
                    System.Diagnostics.Debug.WriteLine($"Publishing to {platformName}: {(result.Success ? "Success" : "Failed")}");
                    if (!result.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    results[platformName] = new MarketplaceListingResult
                    {
                        Success = false,
                        ErrorMessage = $"Error publishing to {platformName}: {ex.Message}"
                    };
                    System.Diagnostics.Debug.WriteLine($"Exception publishing to {platformName}: {ex.Message}");
                }
            }

            return results;
        }

        private ProductData ConvertToProductData(ListingWizardData listingData)
        {
            // 1) Build a single comma-separated string for Features
            string featuresStr = string.Empty;
            if (listingData.ItemSpecifics != null && listingData.ItemSpecifics.Any())
            {
                featuresStr = string.Join(
                    ", ",
                    listingData.ItemSpecifics
                        .SelectMany(kvp => kvp.Value)       // flatten List<string> → string
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                );
            }

            // 2) Build a single “Key: value1, value2” semicolon-separated string for Specifications
            string specsStr = string.Empty;
            if (listingData.ItemSpecifics != null && listingData.ItemSpecifics.Any())
            {
                specsStr = string.Join(
                    "; ",
                    listingData.ItemSpecifics
                        .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}")
                );
            }

            // 3) Convert Dictionary<string, List<string>> → Dictionary<string, string> for ItemSpecifics
            var flattenedSpecifics = new Dictionary<string, string>();
            if (listingData.ItemSpecifics != null && listingData.ItemSpecifics.Any())
            {
                foreach (var kvp in listingData.ItemSpecifics)
                {
                    flattenedSpecifics[kvp.Key] = string.Join(", ", kvp.Value);
                }
            }

            // 4) Format Dimensions into a single string
            string dims = string.Empty;
            if (listingData.PackageDimensions != null)
            {
                var d = listingData.PackageDimensions;
                dims = $"{d.Length}×{d.Width}×{d.Height} {d.Unit}";
            }

            return new ProductData
            {
                Title = listingData.Title,
                BrandModel = listingData.Brand,
                ModelNumber = listingData.MPN,
                Description = listingData.Description,
                Dimensions = dims,

                // Now assign the flattened strings instead of a Dictionary or List
                Features = featuresStr,
                Specifications = specsStr,
                ItemSpecifics = flattenedSpecifics,

                Price = listingData.StartPrice,
                Condition = listingData.ConditionName,
                ItemType = listingData.PrimaryCategoryName,

                // You can also set Sku, Weight, etc. if needed:
                Sku = listingData.CustomSku,
                Weight = listingData.PackageWeight.ToString()
            };
        }


        public async Task<MarketplaceListingResult> PublishDraftListingAsync(
            string platformName,
            string draftId)
        {
            var marketplaceService = _marketplaceFactory.GetMarketplaceService(platformName);

            if (marketplaceService == null)
            {
                return new MarketplaceListingResult
                {
                    Success = false,
                    ErrorMessage = $"Marketplace service for {platformName} not found"
                };
            }

            if (!marketplaceService.SupportsDraftListings)
            {
                return new MarketplaceListingResult
                {
                    Success = false,
                    ErrorMessage = $"{platformName} does not support draft listings"
                };
            }

            try
            {
                // Check if authenticated
                if (!await marketplaceService.IsAuthenticatedAsync())
                {
                    return new MarketplaceListingResult
                    {
                        Success = false,
                        ErrorMessage = $"Not authenticated with {platformName}"
                    };
                }

                // Publish draft
                return await marketplaceService.PublishDraftListingAsync(draftId);
            }
            catch (Exception ex)
            {
                return new MarketplaceListingResult
                {
                    Success = false,
                    ErrorMessage = $"Error publishing draft to {platformName}: {ex.Message}"
                };
            }
        }
    }
}
