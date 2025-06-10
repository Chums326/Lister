using System;
using System.Collections.Generic;

namespace ChumsLister.Core.Models
{
    public class ListingWizardData
    {

        // Image handling properties
        public List<string> ImageUrls { get; set; } = new List<string>();
        public List<string> LocalImagePaths { get; set; } = new List<string>();
        public List<string> UploadedImageUrls { get; set; } = new List<string>();

        // Account Selection
        public string SelectedAccountId { get; set; }
        public string SelectedAccountName { get; set; }

        // Basic Product Info
        public string Title { get; set; }
        public string SummaryTitle { get; set; }
        public string Subtitle { get; set; }
        public string CustomSku { get; set; }
        public string UPC { get; set; }
        public string EAN { get; set; }
        public string ISBN { get; set; }
        public string MPN { get; set; }
        public string Brand { get; set; }

        // eBay Categories
        public string PrimaryCategoryId { get; set; }
        public string PrimaryCategoryName { get; set; }
        public string SecondaryCategoryId { get; set; }
        public string SecondaryCategoryName { get; set; }
        public List<string> StoreCategoryIds { get; set; } = new List<string>();
        public List<string> StoreCategoryNames { get; set; } = new List<string>();

        // Item Condition
        public string ConditionId { get; set; }
        public string ConditionName { get; set; }
        public string ConditionDescription { get; set; }

        // Description
        public string Description { get; set; }
        public string DescriptionTemplate { get; set; }

        // Item Specifics
        public Dictionary<string, List<string>> ItemSpecifics { get; set; } = new Dictionary<string, List<string>>();
        public List<VariationSpecific> VariationSpecifics { get; set; } = new List<VariationSpecific>();

        // Pricing
        public decimal StartPrice { get; set; }
        public decimal? BuyItNowPrice { get; set; }
        public decimal? ReservePrice { get; set; }
        public int Quantity { get; set; }
        public bool BestOfferEnabled { get; set; }
        public decimal? BestOfferAutoAcceptPrice { get; set; }
        public decimal? BestOfferMinimumPrice { get; set; }

        // Listing Details
        public string ListingType { get; set; } = "FixedPriceItem";
        public string ListingDuration { get; set; } = "GTC";
        public bool PrivateListing { get; set; }
        public int? LotSize { get; set; }
        public string ItemLocation { get; set; }
        public int HandlingTimeDays { get; set; }

        // Shipping
        public string ShippingType { get; set; } = "Flat";
        public List<ShippingService> DomesticShippingServices { get; set; } = new List<ShippingService>();
        public List<ShippingService> InternationalShippingServices { get; set; } = new List<ShippingService>();
        public int HandlingTime { get; set; } = 1;
        public string ShippingPackage { get; set; }
        public PackageDimensions PackageDimensions { get; set; } = new PackageDimensions();
        public decimal PackageWeight { get; set; }
        public bool GlobalShipping { get; set; }
        public List<string> ExcludeShipToLocations { get; set; } = new List<string>();

        // Returns
        public string ReturnsAccepted { get; set; }
        public string ReturnPeriod { get; set; }
        public string RefundOption { get; set; }
        public string ReturnShippingPaidBy { get; set; }
        public string ReturnPolicyDescription { get; set; }

        // Payment
        public bool ImmediatePaymentRequired { get; set; }
        public List<string> PaymentMethods { get; set; } = new List<string>();

        // Business Policies (if using)
        public string PaymentPolicyId { get; set; }
        public string ShippingPolicyId { get; set; }
        public string ReturnPolicyId { get; set; }

        // Promotion
        public bool PromotedListing { get; set; }
        public decimal? AdRate { get; set; }
    }

    public class VariationSpecific
    {
        public string SKU { get; set; }
        public Dictionary<string, string> Specifics { get; set; } = new Dictionary<string, string>();
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string UPC { get; set; }
        public string EAN { get; set; }
        public string ISBN { get; set; }
    }

    

    public class PackageDimensions
    {
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public string Unit { get; set; } = "inches";
    }
}