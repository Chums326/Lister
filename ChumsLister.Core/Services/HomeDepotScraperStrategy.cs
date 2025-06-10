using ChumsLister.Core.Models;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace ChumsLister.Core.Services
{
    public class HomeDepotScraperStrategy : IScraperStrategy
    {
        private readonly HttpClient _httpClient;

        public HomeDepotScraperStrategy(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ScrapedProductData> ScrapeFromUrlAsync(string url)
        {
            try
            {
                // Set user agent to avoid blocking
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _httpClient.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                }

                // Download page content
                var htmlContent = await _httpClient.GetStringAsync(url);

                // Parse product data
                var result = new ScrapedProductData
                {
                    Features = new List<string>(),
                    Specifications = new List<string>(),
                    ItemSpecifics = new Dictionary<string, string>()
                };

                // Extract title
                var titleMatch = Regex.Match(htmlContent, @"<h1[^>]*itemprop=""name""[^>]*>(.*?)</h1>");
                if (titleMatch.Success)
                {
                    result.Title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
                }

                // Extract model number
                var modelMatch = Regex.Match(htmlContent, @"Model\s*#\s*</span>\s*<span[^>]*>(.*?)</span>");
                if (modelMatch.Success)
                {
                    result.ModelNumber = modelMatch.Groups[1].Value.Trim();
                }

                // Extract price
                var priceMatch = Regex.Match(htmlContent, @"<span\s+class=""[^""]*price[^""]*""[^>]*>\$?([\d,]+\.\d+)</span>");
                if (priceMatch.Success)
                {
                    result.Price = priceMatch.Groups[1].Value.Trim();
                }

                // Extract description
                var descMatch = Regex.Match(htmlContent, @"<div[^>]*itemprop=""description""[^>]*>(.*?)</div>");
                if (descMatch.Success)
                {
                    result.Description = System.Net.WebUtility.HtmlDecode(descMatch.Groups[1].Value.Trim());
                    // Remove HTML tags
                    result.Description = Regex.Replace(result.Description, @"<.*?>", "");
                }

                // Extract features
                var featuresMatch = Regex.Match(htmlContent, @"<ul[^>]*data-testid=""list-features""[^>]*>(.*?)</ul>");
                if (featuresMatch.Success)
                {
                    var featuresHtml = featuresMatch.Groups[1].Value;
                    var featureMatches = Regex.Matches(featuresHtml, @"<li[^>]*>(.*?)</li>");

                    foreach (Match match in featureMatches)
                    {
                        var feature = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                        // Remove HTML tags
                        feature = Regex.Replace(feature, @"<.*?>", "");
                        if (!string.IsNullOrWhiteSpace(feature))
                        {
                            result.Features.Add(feature);
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scraping Home Depot: {ex.Message}");
                return null;
            }
        }

        public async Task<ScrapedProductData> ScrapeFromModelNumberAsync(string modelNumber)
        {
            try
            {
                // Create search URL for the model number
                var searchUrl = $"https://www.homedepot.com/s/{Uri.EscapeDataString(modelNumber)}";

                // Set user agent to avoid blocking
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _httpClient.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                }

                // Download search results
                var searchHtml = await _httpClient.GetStringAsync(searchUrl);

                // Find product URL in search results
                var productUrlMatch = Regex.Match(searchHtml, @"<a\s+[^>]*href=""(/p/[^""]+)""[^>]*data-type=""product""");
                if (productUrlMatch.Success)
                {
                    var productPath = productUrlMatch.Groups[1].Value;
                    var productUrl = $"https://www.homedepot.com{productPath}";

                    // Scrape product page
                    return await ScrapeFromUrlAsync(productUrl);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scraping Home Depot by model number: {ex.Message}");
                return null;
            }
        }
    }
}