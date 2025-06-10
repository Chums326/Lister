using ChumsLister.Core.Models;

namespace ChumsLister.Core.Services
{
    public interface IScraperStrategy
    {
        Task<ScrapedProductData> ScrapeFromUrlAsync(string url);
        Task<ScrapedProductData> ScrapeFromModelNumberAsync(string modelNumber);
    }
}