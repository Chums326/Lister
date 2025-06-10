using System;
using System.Collections.Generic;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Utilities
{
    public static class ListingTemplateApplier
    {
        public static void ApplyTemplate(this ListingWizardData data, ListingTemplateSettings tpl)
        {
            if (tpl == null) return;

            data.ListingType = tpl.ListingType;
            data.ListingDuration = tpl.Duration;
            data.Quantity = tpl.Quantity;
            data.StartPrice = tpl.StartPrice;
            data.BuyItNowPrice = tpl.BuyItNowPrice;
            data.CustomSku = tpl.CustomLabel;

            data.ShippingPolicyId = tpl.ShippingPolicyId;
            data.PaymentPolicyId = tpl.PaymentPolicyId;
            data.ReturnPolicyId = tpl.ReturnPolicyId;

            data.ItemLocation = tpl.ItemLocation;
            data.HandlingTimeDays = tpl.HandlingTimeDays;
            data.PaymentMethods = new List<string>(tpl.PaymentMethods);

            // Prepend the description template
            data.Description = tpl.DescriptionTemplateHtml
                                  + Environment.NewLine
                                  + (data.Description ?? string.Empty);
        }
    }
}