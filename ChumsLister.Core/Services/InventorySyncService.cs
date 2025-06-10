using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChumsLister.Core.Services
{
    public class InventorySyncService
    {
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private readonly ISettingsService _settingsService;

        public InventorySyncService(
            IMarketplaceServiceFactory marketplaceFactory,
            ISettingsService settingsService)
        {
            _marketplaceFactory = marketplaceFactory;
            _settingsService = settingsService;
        }

        public async Task<List<InventorySyncResult>> SyncInventoryAcrossPlatformsAsync()
        {
            var results = new List<InventorySyncResult>();

            try
            {
                // Get all authenticated marketplace services
                var marketplaceServices = await _marketplaceFactory.GetAuthenticatedMarketplaceServicesAsync();

                if (!marketplaceServices.Any())
                {
                    results.Add(new InventorySyncResult
                    {
                        Success = false,
                        Message = "No authenticated marketplace services found"
                    });
                    return results;
                }

                // Get inventory from each service
                var inventoryByPlatform = new Dictionary<string, List<MarketplaceListingSummary>>();

                foreach (var service in marketplaceServices)
                {
                    try
                    {
                        var listings = await service.GetActiveListingsAsync();
                        inventoryByPlatform[service.Name] = listings;

                        results.Add(new InventorySyncResult
                        {
                            PlatformName = service.Name,
                            Success = true,
                            Message = $"Retrieved {listings.Count} listings",
                            ListingsRetrieved = listings.Count
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new InventorySyncResult
                        {
                            PlatformName = service.Name,
                            Success = false,
                            Message = $"Error retrieving listings: {ex.Message}"
                        });
                    }
                }

                // Find duplicate listings based on title or model number
                var listingGroups = FindDuplicateListings(inventoryByPlatform);

                // Sync quantity across platforms
                foreach (var group in listingGroups)
                {
                    if (group.Listings.Count <= 1)
                        continue;

                    // Determine the true quantity available
                    int totalQuantity = _settingsService.GetSetting<bool>("UseActualInventoryCount", false)
                        ? GetActualInventoryCount(group.ModelNumber)
                        : group.Listings.Sum(l => l.Quantity);

                    int quantityPerPlatform = totalQuantity / group.Listings.Count;
                    int remainder = totalQuantity % group.Listings.Count;

                    // Update each platform
                    foreach (var listing in group.Listings)
                    {
                        try
                        {
                            var service = _marketplaceFactory.GetMarketplaceService(listing.PlatformName);

                            // Assign quantity (add remainder to first listing)
                            int quantityToAssign = quantityPerPlatform;
                            if (remainder > 0)
                            {
                                quantityToAssign += remainder;
                                remainder = 0;
                            }

                            // Only update if quantity is different
                            if (listing.Quantity != quantityToAssign)
                            {
                                // Create new ListingWizardData with required fields initialized
                                var listingData = new ListingWizardData
                                {
                                    // Only set properties that exist on ListingWizardData
                                    Title = listing.Title,
                                    Quantity = quantityToAssign,
                                    Brand = string.Empty,
                                    MPN = string.Empty,
                                    ConditionName = string.Empty,
                                    PrimaryCategoryName = string.Empty,
                                    Description = string.Empty,
                                    // Other fields can be left at their defaults or filled as needed
                                };

                                var updateResult = await service.UpdateListingAsync(listing.ListingId, listingData);

                                results.Add(new InventorySyncResult
                                {
                                    PlatformName = listing.PlatformName,
                                    Success = updateResult.Success,
                                    Message = updateResult.Success
                                        ? $"Updated quantity for '{group.Title}' from {listing.Quantity} to {quantityToAssign}"
                                        : $"Failed to update quantity: {updateResult.ErrorMessage}",
                                    ModelNumber = group.ModelNumber,
                                    ListingTitle = group.Title
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            results.Add(new InventorySyncResult
                            {
                                PlatformName = listing.PlatformName,
                                Success = false,
                                Message = $"Error updating quantity: {ex.Message}",
                                ModelNumber = group.ModelNumber,
                                ListingTitle = group.Title
                            });
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                results.Add(new InventorySyncResult
                {
                    Success = false,
                    Message = $"Error during inventory sync: {ex.Message}"
                });
                return results;
            }
        }

        private List<ListingGroup> FindDuplicateListings(Dictionary<string, List<MarketplaceListingSummary>> inventoryByPlatform)
        {
            var allListings = new List<ListingWithPlatform>();

            // Flatten all listings with platform info
            foreach (var platform in inventoryByPlatform)
            {
                foreach (var listing in platform.Value)
                {
                    allListings.Add(new ListingWithPlatform
                    {
                        PlatformName = platform.Key,
                        ListingId = listing.ListingId,
                        Title = listing.Title,
                        // Extract model number from title or item specifics
                        ModelNumber = ExtractModelNumber(listing.Title),
                        Quantity = listing.Quantity
                    });
                }
            }

            // Group by model number if available, otherwise by normalized title
            var groups = allListings
                .GroupBy(l => !string.IsNullOrEmpty(l.ModelNumber) ? l.ModelNumber : NormalizeTitle(l.Title))
                .Select(g => new ListingGroup
                {
                    ModelNumber = g.First().ModelNumber,
                    Title = g.First().Title,
                    Listings = g.ToList()
                })
                .Where(g => g.Listings.Count > 1)  // Only groups with multiple listings
                .ToList();

            return groups;
        }

        private string ExtractModelNumber(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Simple regex to extract model numbers
            var regex = new Regex(
                @"(?:model|mdl)(?:\s|-|#|:|\.|number|no\.?|num|nbr)?\s*(?<model>[\w\d-]{4,})",
                RegexOptions.IgnoreCase);

            var match = regex.Match(title);

            return match.Success ? match.Groups["model"].Value : string.Empty;
        }

        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Remove common phrases, punctuation, convert to lowercase
            var normalized = title.ToLower()
                .Replace("new", "")
                .Replace("brand new", "")
                .Replace("unopened", "")
                .Replace("sealed", "")
                .Replace("in box", "")
                .Replace("- free shipping", "")
                .Replace("free shipping", "");

            // Remove all punctuation
            normalized = new string(normalized.Where(c => !char.IsPunctuation(c)).ToArray());

            // Remove extra spaces
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized;
        }

        private int GetActualInventoryCount(string modelNumber)
        {
            // In a real implementation, this would query your inventory database
            // For this example, we'll return a dummy value
            return 5;
        }
    }

    public class ListingWithPlatform
    {
        public string PlatformName { get; set; }
        public string ListingId { get; set; }
        public string Title { get; set; }
        public string ModelNumber { get; set; }
        public int Quantity { get; set; }
    }

    public class ListingGroup
    {
        public string ModelNumber { get; set; }
        public string Title { get; set; }
        public List<ListingWithPlatform> Listings { get; set; }
    }

    public class InventorySyncResult
    {
        public string PlatformName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ModelNumber { get; set; }
        public string ListingTitle { get; set; }
        public int ListingsRetrieved { get; set; }
    }
}
