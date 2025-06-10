using ChumsLister.Core.Interfaces;
using System.Net.Http;

namespace ChumsLister.Core.Services
{
    public class PriceOptimizationService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;

        public PriceOptimizationService(
            HttpClient httpClient,
            ISettingsService settingsService)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
        }

        public async Task<PriceRecommendation> GetPriceRecommendationAsync(
            string title,
            string modelNumber,
            string category,
            decimal costPrice)
        {
            try
            {
                // Get competitive prices for similar items
                var competitivePrices = await GetCompetitivePricesAsync(title, modelNumber, category);

                if (competitivePrices.Count == 0)
                {
                    return new PriceRecommendation
                    {
                        Success = false,
                        Message = "No competitive prices found for this item"
                    };
                }

                // Get settings with proper type conversion
                double minMarginPercent;
                if (!double.TryParse(_settingsService.GetSetting<string>("MinMarginPercent", "15.0"), out minMarginPercent))
                    minMarginPercent = 15.0;

                double targetMarginPercent;
                if (!double.TryParse(_settingsService.GetSetting<string>("TargetMarginPercent", "30.0"), out targetMarginPercent))
                    targetMarginPercent = 30.0;

                string pricingStrategy = _settingsService.GetSetting<string>("PricingStrategy", "Competitive");

                // Calculate price recommendations
                decimal minPrice = costPrice * (1 + (decimal)(minMarginPercent / 100));
                decimal competitivePrice = 0;
                decimal marketAverage = 0;

                if (competitivePrices.Count > 0)
                {
                    marketAverage = competitivePrices.Average();

                    // Different strategies
                    switch (pricingStrategy.ToLower())
                    {
                        case "lowest":
                            competitivePrice = competitivePrices.Min() * 0.98m; // Slightly below minimum
                            break;
                        case "highest":
                            competitivePrice = competitivePrices.Max() * 0.95m; // Slightly below maximum
                            break;
                        case "average":
                            competitivePrice = marketAverage;
                            break;
                        case "premium":
                            competitivePrice = marketAverage * 1.1m; // 10% above average
                            break;
                        case "competitive":
                        default:
                            // Lower quartile price
                            var sortedPrices = competitivePrices.OrderBy(p => p).ToList();
                            int lowerQuartileIndex = Math.Max(0, sortedPrices.Count / 4 - 1);
                            competitivePrice = sortedPrices[lowerQuartileIndex];
                            break;
                    }
                }

                // Ensure minimum margin
                decimal recommendedPrice = Math.Max(minPrice, competitivePrice);

                // Calculate projected profit
                decimal projectedProfit = recommendedPrice - costPrice;
                decimal projectedMargin = costPrice > 0 ? (projectedProfit / recommendedPrice) * 100 : 0;

                return new PriceRecommendation
                {
                    Success = true,
                    RecommendedPrice = Math.Round(recommendedPrice, 2),
                    MinimumPrice = Math.Round(minPrice, 2),
                    MarketAverage = Math.Round(marketAverage, 2),
                    MarketMinimum = Math.Round(competitivePrices.Min(), 2),
                    MarketMaximum = Math.Round(competitivePrices.Max(), 2),
                    ProjectedProfit = Math.Round(projectedProfit, 2),
                    ProjectedMargin = Math.Round(projectedMargin, 2),
                    CompetitivePriceCount = competitivePrices.Count
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating price recommendation: {ex.Message}");
                return new PriceRecommendation
                {
                    Success = false,
                    Message = $"Error calculating price recommendation: {ex.Message}"
                };
            }
        }

        private async Task<List<decimal>> GetCompetitivePricesAsync(
            string title,
            string modelNumber,
            string category)
        {
            // In a real implementation, this would query eBay, Amazon, etc.
            // For this example, we'll return dummy data
            // In the future, you would implement real price scraping here
            await Task.Delay(500); // Simulate network delay

            var random = new Random();
            var count = random.Next(5, 15);
            var basePrice = random.Next(50, 200);

            var prices = new List<decimal>();
            for (int i = 0; i < count; i++)
            {
                decimal variation = (decimal)(random.NextDouble() * 0.4 - 0.2); // -20% to +20%
                prices.Add(Math.Round(basePrice * (1 + variation), 2));
            }

            return prices;
        }
    }

    public class PriceRecommendation
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal RecommendedPrice { get; set; }
        public decimal MinimumPrice { get; set; }
        public decimal MarketAverage { get; set; }
        public decimal MarketMinimum { get; set; }
        public decimal MarketMaximum { get; set; }
        public decimal ProjectedProfit { get; set; }
        public decimal ProjectedMargin { get; set; }
        public int CompetitivePriceCount { get; set; }
    }
}