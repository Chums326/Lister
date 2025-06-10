using ChumsLister.Core.Services.Marketplaces;
using System;
using System.Collections.Generic;

namespace ChumsLister.Core.Models
{
    public class ListingTemplateSettings
    {
        // ─── Title & Subtitle ─────────────────────────────
        public string TitlePrefix { get; set; }
        public string DefaultSubtitle { get; set; }

        // ─── Category ──────────────────────────────────────
        public string PrimaryCategoryId { get; set; }
        public string SecondaryCategoryId { get; set; }
        public string MainEbayCategory { get; set; }
        public string StoreCategory { get; set; }

        // ─── Condition ─────────────────────────────────────
        public string DefaultConditionId { get; set; }
        public string ConditionDescription { get; set; }

        // ─── Photos ────────────────────────────────────────
        public List<string> DefaultPhotoUrls { get; set; }
        public List<string> DefaultLocalImagePaths { get; set; }

        // ─── Item specifics ───────────────────────────────
        public Dictionary<string, string> DefaultItemSpecifics { get; set; }

        // ─── Format & Price ───────────────────────────────
        public string ListingFormat { get; set; }   // FixedPriceItem / Auction
        public decimal StartPrice { get; set; }
        public decimal BuyItNowPrice { get; set; }
        public decimal ReservePrice { get; set; }
        public int Quantity { get; set; }
        public string Duration { get; set; }
        public bool UseVariations { get; set; }

        // ─── Business Policies ────────────────────────────
        public bool UseBusinessPolicies { get; set; }
        public string PaymentPolicyId { get; set; }
        public string ShippingPolicyId { get; set; }
        public string ReturnPolicyId { get; set; }

        // ─── Shipping Details (legacy) ───────────────────
        public string ItemLocation { get; set; }
        public int HandlingTimeDays { get; set; }
        public List<ShippingRateResult> DefaultShippingRates { get; set; }

        // ─── Payment Methods (legacy) ─────────────────────
        public List<string> PaymentMethods { get; set; }
        public bool RequireImmediatePay { get; set; }

        // ─── Returns (legacy) ─────────────────────────────
        public bool ReturnsAccepted { get; set; }
        public int ReturnWindowDays { get; set; }
        public string ReturnShippingPaidBy { get; set; }  // Buyer / Seller
        public string RefundMethod { get; set; }  // MoneyBack / Exchange
        public decimal RestockingFeePercent { get; set; }

        // ─── Description ──────────────────────────────────
        public string DescriptionTemplateHtml { get; set; }

        // ─── Selling Enhancements ─────────────────────────
        public bool ScheduleListing { get; set; }
        public DateTime? ScheduledStartDate { get; set; }
        public bool GalleryPlus { get; set; }
        public bool BoldTitleUpgrade { get; set; }
        public bool SubtitleUpgrade { get; set; }
        public bool PromotedListingsOptIn { get; set; }
        public bool GiftOptions { get; set; }

        // ─── Product Identifiers ─────────────────────────
        public string UPC { get; set; }
        public string EAN { get; set; }
        public string ISBN { get; set; }
        public string GTIN { get; set; }
        public string EPID { get; set; }

        // ─── Seller‐only fields ──────────────────────────
        public string CustomLabel { get; set; }  // SKU
        public string VATID { get; set; }
        public string ListingType { get; set; }

        public ListingTemplateSettings()
        {
            // sensible defaults
            TitlePrefix = "";
            DefaultSubtitle = "";
            PrimaryCategoryId = "";
            SecondaryCategoryId = "";
            DefaultConditionId = "1000";
            ConditionDescription = "";
            DefaultPhotoUrls = new List<string>();
            DefaultLocalImagePaths = new List<string>();
            DefaultItemSpecifics = new Dictionary<string, string>();
            ListingFormat = "FixedPriceItem";
            StartPrice = 1.00m;
            BuyItNowPrice = 0m;
            ReservePrice = 0m;
            Quantity = 1;
            Duration = "GTC";
            UseVariations = false;
            UseBusinessPolicies = true;
            PaymentPolicyId = "";
            ShippingPolicyId = "";
            ReturnPolicyId = "";
            ItemLocation = "";
            HandlingTimeDays = 3;
            DefaultShippingRates = new List<ShippingRateResult>();
            PaymentMethods = new List<string>();
            RequireImmediatePay = false;
            ReturnsAccepted = true;
            ReturnWindowDays = 30;
            ReturnShippingPaidBy = "Buyer";
            RefundMethod = "MoneyBack";
            RestockingFeePercent = 0;
            DescriptionTemplateHtml = "";
            ScheduleListing = false;
            ScheduledStartDate = null;
            GalleryPlus = false;
            BoldTitleUpgrade = false;
            SubtitleUpgrade = false;
            PromotedListingsOptIn = false;
            GiftOptions = false;
            UPC = "";
            EAN = "";
            ISBN = "";
            GTIN = "";
            EPID = "";
            CustomLabel = "";
            VATID = "";
            MainEbayCategory = "";
            StoreCategory = "";
        }
    }
}
