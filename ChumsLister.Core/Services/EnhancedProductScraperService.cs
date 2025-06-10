using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using System.Net.Http;
using System.Text.RegularExpressions;
using ProductData = ChumsLister.Core.Models.ProductData;


namespace ChumsLister.Core.Services
{
    public class EnhancedProductScraperService : IProductScraper
    {
        private readonly HttpClient _httpClient;
        private readonly IAIService _aiService;
        private readonly Dictionary<string, IScraperStrategy> _scraperStrategies;

        public EnhancedProductScraperService(
            HttpClient httpClient,
            IAIService aiService)
        {
            _httpClient = httpClient;
            _aiService = aiService;

            // Initialize strategies for different sites
            _scraperStrategies = new Dictionary<string, IScraperStrategy>(StringComparer.OrdinalIgnoreCase)
            {
                { "home depot", new HomeDepotScraperStrategy(_httpClient) },
                { "lowes", new LowesScraperStrategy(_httpClient) },
                { "amazon", new AmazonScraperStrategy(_httpClient) },
                { "wayfair", new WayfairScraperStrategy(_httpClient) }
            };
        }

        // Fixed method - now uses ConvertScrapedToProductData instead of the non-existent ConvertToProductData
        public bool ScrapeProductData(string input, out ProductData productData)
        {
            // Call async method and wait for result
            var task = Task.Run(async () => await ScrapeProductAsync(input));
            task.Wait();

            // Convert ScrapedProductData to ProductData using the existing method
            productData = ConvertScrapedToProductData(task.Result);
            return productData != null;
        }

        public async Task<ProductData> ScrapeProductDataAsync(string input)
        {
            // Get scraped data first
            var scrapedData = await ScrapeProductAsync(input);

            // Then convert ScrapedProductData to ProductData
            return ConvertScrapedToProductData(scrapedData);
        }

        private ProductData ConvertScrapedToProductData(ScrapedProductData scrapedData)
        {
            if (scrapedData == null)
                return null;

            decimal.TryParse(scrapedData.Price, out decimal price);

            return new ProductData
            {
                Title = scrapedData.Title,
                Brand = scrapedData.Brand,
                ModelNumber = scrapedData.ModelNumber,
                Description = scrapedData.Description,
                Price = price,
                Features = string.Join("\n", scrapedData.Features ?? new List<string>()),
                Specifications = string.Join("\n", scrapedData.Specifications ?? new List<string>()),
                Dimensions = scrapedData.Dimensions,
                ItemType = scrapedData.Type,
                Condition = "New",
                ItemSpecifics = scrapedData.ItemSpecifics ?? new Dictionary<string, string>()
            };
        }


        public async Task<ScrapedProductData> ScrapeProductAsync(string modelNumberOrUrl)
        {
            // Determine if it's a URL or model number
            bool isUrl = Uri.TryCreate(modelNumberOrUrl, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == "http" || uri.Scheme == "https");

            ScrapedProductData result = null;

            if (isUrl)
            {
                // Extract domain to determine which scraper to use
                string domain = uri.Host.ToLower();

                foreach (var strategy in _scraperStrategies)
                {
                    if (domain.Contains(strategy.Key))
                    {
                        result = await strategy.Value.ScrapeFromUrlAsync(modelNumberOrUrl);
                        break;
                    }
                }
            }
            else
            {
                // Try each strategy until we get a result
                foreach (var strategy in _scraperStrategies.Values)
                {
                    result = await strategy.ScrapeFromModelNumberAsync(modelNumberOrUrl);
                    if (result != null && !string.IsNullOrEmpty(result.Title))
                        break;
                }
            }

            // If no results from specific scrapers, try using AI to enhance
            if (result == null || string.IsNullOrEmpty(result.Title))
            {
                result = await EnhanceWithAIAsync(modelNumberOrUrl);
            }

            return result ?? new ScrapedProductData { ModelNumber = modelNumberOrUrl };
        }

        private async Task<ScrapedProductData> EnhanceWithAIAsync(string modelNumberOrUrl)
        {
            try
            {
                var aiPrompt = $"Please provide product information for the model number or item: {modelNumberOrUrl}. " +
                              "Include title, price range, product type, key features, and specifications if available.";

                var aiResponse = await _aiService.GetCompletionAsync(aiPrompt);

                if (string.IsNullOrEmpty(aiResponse))
                    return new ScrapedProductData { ModelNumber = modelNumberOrUrl };

                return ParseAIResponse(aiResponse, modelNumberOrUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enhancing with AI: {ex.Message}");
                return new ScrapedProductData { ModelNumber = modelNumberOrUrl };
            }
        }

        private ScrapedProductData ParseAIResponse(string aiResponse, string modelNumber)
        {
            // Simplified implementation
            var result = new ScrapedProductData
            {
                ModelNumber = modelNumber,
                Features = new List<string>(),
                Specifications = new List<string>(),
                ItemSpecifics = new Dictionary<string, string>()
            };

            // Extract title
            var titleMatch = Regex.Match(aiResponse, @"(?:Title:|Product:)\s*(.+)$", RegexOptions.Multiline);
            if (titleMatch.Success)
            {
                result.Title = titleMatch.Groups[1].Value.Trim();
            }

            // Extract price
            var priceMatch = Regex.Match(aiResponse, @"(?:Price:|Cost:|Range:)\s*\$?(\d+(?:\.\d+)?(?:\s*-\s*\$?\d+(?:\.\d+)?)?)");
            if (priceMatch.Success)
            {
                result.Price = priceMatch.Groups[1].Value.Trim();
            }

            // Extract type
            var typeMatch = Regex.Match(aiResponse, @"(?:Type:|Category:)\s*(.+)$", RegexOptions.Multiline);
            if (typeMatch.Success)
            {
                result.Type = typeMatch.Groups[1].Value.Trim();
            }

            // Extract description
            var descMatch = Regex.Match(aiResponse, @"(?:Description:)([\s\S]*?)(?:(?:Features:|Specifications:|Dimensions:)|$)");
            if (descMatch.Success && descMatch.Groups.Count > 1)
            {
                result.Description = descMatch.Groups[1].Value.Trim();
            }

            return result;
        }
    }
}