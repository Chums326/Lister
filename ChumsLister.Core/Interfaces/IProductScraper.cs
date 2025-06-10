using System.Threading.Tasks;
using ChumsLister.Core.Models;

namespace ChumsLister.Core.Interfaces
{
    public interface IProductScraper
    {
        Task<ScrapedProductData> ScrapeProductAsync(string url);
        Task<ProductData> ScrapeProductDataAsync(string url);
        bool ScrapeProductData(string url, out ProductData productData);
    }
}