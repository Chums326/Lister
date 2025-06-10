using ChumsLister.Core.Models;
using System.Net.Http;

namespace ChumsLister.Core.Services
{
    public class WayfairScraperStrategy : IScraperStrategy
    {
        private readonly HttpClient _httpClient;

        public WayfairScraperStrategy(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ScrapedProductData> ScrapeFromUrlAsync(string url)
        {
            // Placeholder implementation
            System.Diagnostics.Debug.WriteLine($"Wayfair URL scraping not yet implemented: {url}");
            return null;
        }

        public async Task<ScrapedProductData> ScrapeFromModelNumberAsync(string modelNumber)
        {
            // Placeholder implementation
            System.Diagnostics.Debug.WriteLine($"Wayfair model number scraping not yet implemented: {modelNumber}");
            return null;
        }
    }
}