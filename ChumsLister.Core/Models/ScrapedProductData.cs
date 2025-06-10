namespace ChumsLister.Core.Models
{
    public class ScrapedProductData
    {
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ModelNumber { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Dimensions { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new List<string>();
        public List<string> Specifications { get; set; } = new List<string>();
        public Dictionary<string, string> ItemSpecifics { get; set; } = new Dictionary<string, string>();
    }
}