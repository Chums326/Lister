using System.Collections.Generic;

namespace ChumsLister.Core.Models
{
    public static class ListingMapper
    {
        public static ListingDetailsDto FromScrapedData(ProductData data)
        {
            return new ListingDetailsDto
            {
                Title = data.Title ?? "",
                Description = data.Description ?? "",
                Price = data.Price,
                Category = data.Type ?? "",
                Condition = "New", // Default or user-specified
                Location = "Default Location", // Or bind from UI
                PayPalEmail = "you@example.com", // Or bind from config/input
                OfferFreeShipping = true,
                ShippingCost = 0,
                ItemSpecifics = new Dictionary<string, string>
                {
                    { "Model", data.ModelNumber ?? "" },
                    { "Dimensions", data.Dimensions ?? "" }
                    // Add more specifics as needed
                }
            };
        }

        // Additional helper method to create from user input
        public static ListingDetailsDto FromUserInput(
            string title,
            string description,
            decimal price,
            string category,
            string condition,
            List<string> imagePaths = null,
            string location = "",
            string paypalEmail = "",
            bool offerFreeShipping = true,
            decimal shippingCost = 0,
            Dictionary<string, string> itemSpecifics = null)
        {
            return new ListingDetailsDto
            {
                Title = title ?? "",
                Description = description ?? "",
                Price = price,
                Category = category ?? "",
                Condition = condition ?? "New",
                ImagePaths = imagePaths ?? new List<string>(),
                Location = location ?? "",
                PayPalEmail = paypalEmail ?? "",
                OfferFreeShipping = offerFreeShipping,
                ShippingCost = shippingCost,
                ItemSpecifics = itemSpecifics ?? new Dictionary<string, string>()
            };
        }

        // Method to validate a listing before submission
        public static bool ValidateListing(ListingDetailsDto listing, out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(listing.Title))
                errors.Add("Title is required");

            if (string.IsNullOrWhiteSpace(listing.Description))
                errors.Add("Description is required");

            if (listing.Price <= 0)
                errors.Add("Price must be greater than 0");

            if (string.IsNullOrWhiteSpace(listing.Category))
                errors.Add("Category is required");

            if (string.IsNullOrWhiteSpace(listing.Condition))
                errors.Add("Condition is required");

            return errors.Count == 0;
        }
    }
}