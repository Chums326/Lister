using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using ChumsLister.Core.Services.Marketplaces;
using CsvHelper;
using System.Globalization;
using System.IO;

namespace ChumsLister.Core.Services
{
    public class BulkListingService
    {
        private readonly IProductScraper _productScraper;
        private readonly AIDescriptionGeneratorService _descriptionGenerator;
        private readonly MultiPlatformPublishingService _publishingService;

        public BulkListingService(
            IProductScraper productScraper,
            AIDescriptionGeneratorService descriptionGenerator,
            MultiPlatformPublishingService publishingService)
        {
            _productScraper = productScraper;
            _descriptionGenerator = descriptionGenerator;
            _publishingService = publishingService;
        }

        public async Task<List<BulkImportResult>> ImportFromCsvAsync(
            string filePath,
            List<string> targetPlatforms,
            IProgress<int> progress = null)
        {
            var results = new List<BulkImportResult>();

            try
            {
                // Read all records from CSV first
                List<BulkListingImport> records;
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    records = csv.GetRecords<BulkListingImport>().ToList();
                }

                int totalRecords = records.Count;
                int processedCount = 0;

                foreach (var record in records)
                {
                    var importResult = new BulkImportResult
                    {
                        ModelNumber = record.ModelNumber,
                        Title = record.Title
                    };

                    try
                    {
                        // Scrape product data if model number is provided
                        ScrapedProductData scrapedData = null;
                        if (!string.IsNullOrEmpty(record.ModelNumber))
                        {
                            scrapedData = await _productScraper.ScrapeProductAsync(record.ModelNumber);
                        }

                        // Prepare ListingWizardData with correct properties
                        var listingData = new ListingWizardData
                        {
                            // Title: prefer record.Title; fallback to scrapedData
                            Title = !string.IsNullOrEmpty(record.Title)
                                ? record.Title
                                : scrapedData?.Title ?? string.Empty,

                            // Brand: prefer record.Brand; fallback to scrapedData
                            Brand = !string.IsNullOrEmpty(record.Brand)
                                ? record.Brand
                                : scrapedData?.Brand ?? string.Empty,

                            // ModelNumber (MPN)
                            MPN = record.ModelNumber ?? string.Empty,

                            // PrimaryCategoryName: use record.Category
                            PrimaryCategoryName = record.Category ?? string.Empty,

                            // ConditionName: use record.Condition or default to "New"
                            ConditionName = !string.IsNullOrEmpty(record.Condition)
                                ? record.Condition
                                : "New",

                            // StartPrice: use record.Price if > 0; fallback to scrapedData.Price parsed
                            StartPrice = record.Price > 0
                                ? record.Price
                                : decimal.TryParse(scrapedData?.Price, out var scrapedPrice) ? scrapedPrice : 0m,

                            // Quantity: use record.Quantity if > 0; fallback to 1
                            Quantity = record.Quantity > 0 ? record.Quantity : 1,

                            // PackageWeight: default to 0 (required field)
                            PackageWeight = 0m,

                            // Description: either record.Description or generate via AI if empty
                            Description = !string.IsNullOrEmpty(record.Description)
                                ? record.Description
                                : (scrapedData != null
                                    ? await _descriptionGenerator.GenerateDescriptionAsync(
                                        new ListingWizardData
                                        {
                                            Title = !string.IsNullOrEmpty(record.Title)
                                                ? record.Title
                                                : scrapedData.Title ?? string.Empty,
                                            Brand = !string.IsNullOrEmpty(record.Brand)
                                                ? record.Brand
                                                : scrapedData.Brand ?? string.Empty,
                                            MPN = record.ModelNumber ?? string.Empty,
                                            PrimaryCategoryName = record.Category ?? string.Empty,
                                            ConditionName = !string.IsNullOrEmpty(record.Condition)
                                                ? record.Condition
                                                : "New",
                                            StartPrice = record.Price > 0
                                                ? record.Price
                                                : decimal.TryParse(scrapedData.Price, out var sp) ? sp : 0m,
                                            Quantity = record.Quantity > 0 ? record.Quantity : 1,
                                            PackageWeight = 0m
                                        })
                                    : string.Empty)
                        };

                        // Publish to platforms
                        var publishResults = await _publishingService.PublishToMultiplePlatformsAsync(
                            listingData, targetPlatforms);

                        importResult.Results = publishResults;
                        importResult.Success = publishResults.Values.Any(r => r.Success);
                    }
                    catch (Exception ex)
                    {
                        importResult.Success = false;
                        importResult.ErrorMessage = ex.Message;
                    }

                    results.Add(importResult);

                    // Update progress
                    processedCount++;
                    progress?.Report((int)((float)processedCount / totalRecords * 100));
                }
            }
            catch (Exception ex)
            {
                results.Add(new BulkImportResult
                {
                    Success = false,
                    ErrorMessage = $"Error importing CSV: {ex.Message}"
                });
            }

            return results;
        }

        public async Task<bool> ExportToExcelAsync(
            List<ListingWizardData> listings,
            string filePath)
        {
            try
            {
                // Create a simple CSV export with fields matching ListingWizardData
                using (var writer = new StreamWriter(filePath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    // Write header
                    csv.WriteField("Title");
                    csv.WriteField("Brand");
                    csv.WriteField("Model Number");
                    csv.WriteField("Price");
                    csv.WriteField("Quantity");
                    csv.WriteField("Condition");
                    csv.WriteField("Category");
                    csv.WriteField("Description");
                    csv.WriteField("Weight");
                    csv.NextRecord();

                    // Write data
                    foreach (var listing in listings)
                    {
                        csv.WriteField(listing.Title);
                        csv.WriteField(listing.Brand);
                        csv.WriteField(listing.MPN);
                        csv.WriteField(listing.StartPrice);
                        csv.WriteField(listing.Quantity);
                        csv.WriteField(listing.ConditionName);
                        csv.WriteField(listing.PrimaryCategoryName);
                        csv.WriteField(listing.Description);
                        csv.WriteField(listing.PackageWeight);
                        csv.NextRecord();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting to Excel: {ex.Message}");
                return false;
            }
        }
    }

    public class BulkListingImport
    {
        public string ModelNumber { get; set; }
        public string Title { get; set; }
        public string Brand { get; set; }
        public string Description { get; set; }
        public string Condition { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    public class BulkImportResult
    {
        public string ModelNumber { get; set; }
        public string Title { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, MarketplaceListingResult> Results { get; set; } = new();
    }
}
