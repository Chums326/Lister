namespace ChumsLister.Core.Models
{
    public class ProductData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ProductData ScrapedProduct { get; set; }

        public string UPC { get; set; } = string.Empty;
        public string MPN { get; set; } = string.Empty;
        public string BrandModel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ModelNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Dimensions { get; set; } = string.Empty;
        public string Features { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0m;  
        public string Condition { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public string EbayItemId { get; set; } = string.Empty;
        public string AmazonItemId { get; set; } = string.Empty;
        public string FacebookItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal? ItemLength { get; set; }
        public decimal? ItemWidth { get; set; }
        public decimal? ItemHeight { get; set; }
        public decimal? AcquisitionCost { get; set; }
        public int AvailableQuantity { get; set; } = 1;
        public DateTime? ListingDate { get; set; }
        public DateTime? SoldDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> ImagePaths { get; set; } = new List<string>();
        public Dictionary<string, string> ItemSpecifics { get; set; } = new Dictionary<string, string>();
        public void ParseDimensionsFromText()
        {
            if (string.IsNullOrEmpty(Dimensions)) return;

            try
            {
                string[] parts = Dimensions.Split(new[] { 'x', 'X', '*' });
                if (parts.Length >= 3)
                {
                    var numericParts = new List<decimal>();
                    foreach (var part in parts)
                    {
                        if (decimal.TryParse(part.Trim(), out decimal dim))
                        {
                            numericParts.Add(dim);
                        }
                    }

                    if (numericParts.Count >= 3)
                    {
                        ItemLength = numericParts[0];
                        ItemWidth = numericParts[1];
                        ItemHeight = numericParts[2];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing dimensions: {ex.Message}");
            }
        }

        public string GetPlatformId(string platformName)
        {
            return platformName.ToLower() switch
            {
                "ebay" => EbayItemId,
                "amazon" => AmazonItemId,
                "facebook" => FacebookItemId,
                _ => string.Empty
            };
        }

        public void SetPlatformId(string platformName, string id)
        {
            switch (platformName.ToLower())
            {
                case "ebay": EbayItemId = id; break;
                case "amazon": AmazonItemId = id; break;
                case "facebook": FacebookItemId = id; break;
            }
        }

        public decimal CalculateShippingVolume()
        {
            return (ItemLength ?? 0) * (ItemWidth ?? 0) * (ItemHeight ?? 0);
        }

        public Dictionary<string, object> ToWizardData()
        {
            return new Dictionary<string, object>
            {
                ["Title"] = Title,
                ["Brand"] = BrandModel,
                ["ModelNumber"] = ModelNumber,
                ["Condition"] = Condition,
                ["Category"] = ItemType,
                ["Description"] = Description,
                ["ItemType"] = ItemType,
                ["Dimensions"] = Dimensions,
                ["Location"] = Location,
                ["ScrapedProduct"] = this,
                ["Price"] = Price,
                ["Quantity"] = AvailableQuantity,
                ["ItemSpecifics"] = new Dictionary<string, string>(ItemSpecifics),
                ["Features"] = !string.IsNullOrEmpty(Features) ? new List<string> { Features } : new List<string>(),
                ["Specifications"] = !string.IsNullOrEmpty(Specifications) ? new List<string> { Specifications } : new List<string>()
            };
        }

        public static ProductData Clone(ProductData source)
        {
            if (source == null) return null;

            return new ProductData
            {
                Sku = source.Sku,
                Id = source.Id,
                ScrapedProduct = source.ScrapedProduct,
                BrandModel = source.BrandModel,
                Title = source.Title,
                ModelNumber = source.ModelNumber,
                Description = source.Description,
                Dimensions = source.Dimensions,
                Features = source.Features,
                Specifications = source.Specifications,
                Price = source.Price,
                Condition = source.Condition,
                Brand = source.Brand,
                Weight = source.Weight,
                EbayItemId = source.EbayItemId,
                AmazonItemId = source.AmazonItemId,
                FacebookItemId = source.FacebookItemId,
                ItemType = source.ItemType,
                Type = source.Type,
                ItemLength = source.ItemLength,
                ItemWidth = source.ItemWidth,
                ItemHeight = source.ItemHeight,
                AcquisitionCost = source.AcquisitionCost,
                AvailableQuantity = source.AvailableQuantity,
                ListingDate = source.ListingDate,
                SoldDate = source.SoldDate,
                Location = source.Location,
                Status = source.Status,
                ImagePaths = new List<string>(source.ImagePaths),
                ItemSpecifics = new Dictionary<string, string>(source.ItemSpecifics)
                
            };
        }
    }
}
