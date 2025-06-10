using System.Text.RegularExpressions;

namespace ChumsLister.Core.Utilities
{
    /// <summary>
    /// Common string manipulation helpers
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Formats a product title for eBay
        /// </summary>
        public static string FormatEbayTitle(string brandModel, string title, int maxLength = 80)
        {
            // Make sure we have valid inputs
            if (string.IsNullOrWhiteSpace(brandModel)) brandModel = "Product";
            if (string.IsNullOrWhiteSpace(title)) title = "Listing";

            // Combine brand and title
            string ebayTitle = brandModel + " - " + title;

            // Truncate to maxLength characters if needed
            if (ebayTitle.Length > maxLength)
            {
                int cutPoint = ebayTitle.LastIndexOf(' ', maxLength - 3);
                if (cutPoint < 20) cutPoint = maxLength - 3;
                ebayTitle = ebayTitle.Substring(0, cutPoint) + "...";
            }

            return ebayTitle;
        }

        /// <summary>
        /// Safely extracts the first decimal found in a string
        /// </summary>
        public static decimal? ExtractDecimal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            Match match = Regex.Match(text, @"(\d+\.?\d*)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal value))
            {
                return value;
            }

            return null;
        }
    }
}