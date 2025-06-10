namespace ChumsLister.Core.Models
{
    public class UserSettings
    {
        public string ApiKey { get; set; }
        public string UserName { get; set; }
        public string EmailAddress { get; set; }
        public string DefaultCategory { get; set; }
        public bool UseDarkMode { get; set; }
        public bool AutoSyncInventory { get; set; }
        public bool EnableAIFeatures { get; set; }
        public string DescriptionStyle { get; set; }
        public bool IncludeShippingByDefault { get; set; }
        public bool AutoCalculateShipping { get; set; }
        public bool AutomaticUpdates { get; set; }
        public bool CheckInventoryBeforeListing { get; set; }
        public Dictionary<string, Dictionary<string, string>> MarketplaceSettings { get; set; }
        public Dictionary<string, object> CustomSettings { get; set; }
        public ListingTemplateSettings ListingTemplate { get; set; }

        // eBay specific properties
        public string EbayAccessToken { get; set; }
        public string EbayRefreshToken { get; set; }  // ✅ Add this
        public DateTime? EbayTokenExpiry { get; set; } // ✅ Add this

        public UserSettings()
        {
            // Set default values
            DefaultCategory = "Other";
            UseDarkMode = false;
            AutoSyncInventory = true;
            EnableAIFeatures = true;
            DescriptionStyle = "Detailed";
            IncludeShippingByDefault = true;
            AutoCalculateShipping = true;
            AutomaticUpdates = true;
            CheckInventoryBeforeListing = true;
            MarketplaceSettings = new Dictionary<string, Dictionary<string, string>>();
            CustomSettings = new Dictionary<string, object>();
            ListingTemplate = new ListingTemplateSettings();
        }

        /// <summary>
        /// Creates a deep copy of this UserSettings instance
        /// </summary>
        /// <returns>A new UserSettings instance with the same values</returns>
        public UserSettings Clone()
        {
            var clone = new UserSettings
            {
                ApiKey = this.ApiKey,
                UserName = this.UserName,
                EmailAddress = this.EmailAddress,
                DefaultCategory = this.DefaultCategory,
                UseDarkMode = this.UseDarkMode,
                AutoSyncInventory = this.AutoSyncInventory,
                EnableAIFeatures = this.EnableAIFeatures,
                DescriptionStyle = this.DescriptionStyle,
                IncludeShippingByDefault = this.IncludeShippingByDefault,
                AutoCalculateShipping = this.AutoCalculateShipping,
                AutomaticUpdates = this.AutomaticUpdates,
                CheckInventoryBeforeListing = this.CheckInventoryBeforeListing,
                EbayAccessToken = this.EbayAccessToken,
                EbayRefreshToken = this.EbayRefreshToken,     
                EbayTokenExpiry = this.EbayTokenExpiry         
            };

            // Deep copy MarketplaceSettings
            if (this.MarketplaceSettings != null)
            {
                clone.MarketplaceSettings = new Dictionary<string, Dictionary<string, string>>();
                foreach (var kvp in this.MarketplaceSettings)
                {
                    var innerDict = new Dictionary<string, string>();
                    if (kvp.Value != null)
                    {
                        foreach (var innerKvp in kvp.Value)
                        {
                            innerDict[innerKvp.Key] = innerKvp.Value;
                        }
                    }
                    clone.MarketplaceSettings[kvp.Key] = innerDict;
                }
            }

            // Deep copy CustomSettings
            if (this.CustomSettings != null)
            {
                clone.CustomSettings = new Dictionary<string, object>();
                foreach (var kvp in this.CustomSettings)
                {
                    clone.CustomSettings[kvp.Key] = kvp.Value;
                }
            }

            return clone;

            ListingTemplate = new ListingTemplateSettings
            {
                
                ShippingPolicyId = this.ListingTemplate.ShippingPolicyId,
                PaymentPolicyId = this.ListingTemplate.PaymentPolicyId,
                ReturnPolicyId = this.ListingTemplate.ReturnPolicyId,
                MainEbayCategory = this.ListingTemplate.MainEbayCategory,
                StoreCategory = this.ListingTemplate.StoreCategory,
                ListingType = this.ListingTemplate.ListingType,
                Duration = this.ListingTemplate.Duration,
                HandlingTimeDays = this.ListingTemplate.HandlingTimeDays,
                ItemLocation = this.ListingTemplate.ItemLocation,
                PaymentMethods = new List<string>(this.ListingTemplate.PaymentMethods),
                DescriptionTemplateHtml = this.ListingTemplate.DescriptionTemplateHtml

            };
            
            return clone;

        }
    }
}