using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ProductData = ChumsLister.Core.Models.ProductData;


namespace ChumsLister.Core
{
    public class SeleniumProductScraper
    {
        private readonly ILogger<SeleniumProductScraper> _logger;
        private readonly ChromeDriver _driver;

        public SeleniumProductScraper(ILogger<SeleniumProductScraper> logger)
        {
            _logger = logger;

            var options = new ChromeOptions();
            options.AddArgument("--headless");
            _driver = new ChromeDriver(options);
        }

        public async Task<(bool Success, ProductData Data)> TryScrapeProductDataAsync(string brandModel)
        {
            try
            {
                var data = await ScrapeProductDataAsync(brandModel);
                return (data != null, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping product");
                return (false, null);
            }
        }

        private async Task<ProductData> ScrapeProductDataAsync(string brandModel)
        {
            var url = $"https://example.com/search?q={Uri.EscapeDataString(brandModel)}";
            _driver.Navigate().GoToUrl(url);

            await Task.Delay(2000); // simulate async wait for content load

            var data = new ProductData
            {
                Title = TryFindText(By.CssSelector("h1.product-title")),
                Price = decimal.TryParse(TryFindText(By.CssSelector("span.price")), out var price) ? price : 0m,
                Condition = TryFindText(By.Id("productCondition")) ?? "New",
                Description = TryFindText(By.CssSelector(".description"))
            };

            return data;
        }

        private string TryFindText(By selector)
        {
            try
            {
                return _driver.FindElement(selector)?.Text?.Trim() ?? string.Empty;
            }
            catch (NoSuchElementException)
            {
                return string.Empty;
            }
        }

        public void Dispose()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}
