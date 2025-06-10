using ChumsLister.Core.Models;
using System.Net.Http;

namespace ChumsLister.Core.Services
{
    public class AmazonScraperStrategy : IScraperStrategy
    {
        private readonly HttpClient _httpClient;

        public AmazonScraperStrategy(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ScrapedProductData> ScrapeFromUrlAsync(string url)
        {
            // Placeholder implementation
            System.Diagnostics.Debug.WriteLine($"Amazon URL scraping not yet implemented: {url}");
            return null;
        }

        public async Task<ScrapedProductData> ScrapeFromModelNumberAsync(string modelNumber)
        {
            // Placeholder implementation
            System.Diagnostics.Debug.WriteLine($"Amazon model number scraping not yet implemented: {modelNumber}");
            return null;
        }
    }
}