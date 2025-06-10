using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class SummaryPage : Page, IWizardPage
    {
        private readonly IEbayService _ebayService;

        public SummaryPage(IEbayService ebayService)
        {
            InitializeComponent();
            _ebayService = ebayService;
        }

        public bool ValidatePage()
        {
            // No validation needed for summary page
            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            // No data to save on summary page
        }

        public void LoadData(ListingWizardData listingData)
        {
            // Update all summary fields with data from the model

            // Basic information
            txtSummaryTitle.Text = listingData.Title ?? "-";
            txtSummaryBrand.Text = listingData.Brand ?? "-";
            txtSummaryModelNumber.Text = listingData.MPN ?? "-";
            txtSummaryCondition.Text = listingData.ConditionName ?? "-";
            txtSummaryCategory.Text = listingData.PrimaryCategoryName ?? "-";
            txtSummaryPrice.Text = !string.IsNullOrEmpty(listingData.StartPrice.ToString()) ? $"${listingData.StartPrice:F2}" : "-";

            // Item specifics
            var itemSpecifics = new List<KeyValuePair<string, string>>();
            if (listingData.ItemSpecifics != null)
            {
                foreach (var kvp in listingData.ItemSpecifics)
                {
                    itemSpecifics.Add(new KeyValuePair<string, string>(kvp.Key, string.Join(", ", kvp.Value)));
                }
            }
            listItemSpecifics.ItemsSource = itemSpecifics;

            // Marketplace information - just eBay with selected account
            var marketplaces = new List<string> { $"eBay - {listingData.SelectedAccountName}" };
            listMarketplaces.ItemsSource = marketplaces;

            // Shipping options
            var shippingOptions = new List<string>();

            if (listingData.ShippingType == "Calculated")
                shippingOptions.Add("Calculated shipping");
            else if (listingData.ShippingType == "Flat")
                shippingOptions.Add("Flat rate shipping");
            else if (listingData.ShippingType == "LocalPickup")
                shippingOptions.Add("Local pickup only");

            if (listingData.DomesticShippingServices.Count > 0)
                shippingOptions.Add($"{listingData.DomesticShippingServices.Count} domestic services");

            if (listingData.InternationalShippingServices.Count > 0)
                shippingOptions.Add($"{listingData.InternationalShippingServices.Count} international services");

            if (!string.IsNullOrEmpty(listingData.HandlingTime.ToString()))
                shippingOptions.Add($"Handling time: {listingData.HandlingTime} day(s)");

            txtSummaryShipping.Text = shippingOptions.Count > 0
                ? string.Join("\n", shippingOptions)
                : "No shipping options specified";

            // Return policy
            txtSummaryReturnPolicy.Text = listingData.ReturnsAccepted == "ReturnsAccepted"
                ? $"{listingData.ReturnPeriod} returns accepted"
                : "No returns accepted";

            // Images
            UpdateImagesList(listingData.ImageUrls);
        }

        private void UpdateImagesList(List<string> imagePaths)
        {
            var imageList = new List<string>();

            foreach (var path in imagePaths)
            {
                if (File.Exists(path) || Uri.IsWellFormedUriString(path, UriKind.Absolute))
                {
                    imageList.Add(path);
                }
            }

            listImages.ItemsSource = imageList;
            txtImageCount.Text = $"{imageList.Count} images selected";
        }
    }
}