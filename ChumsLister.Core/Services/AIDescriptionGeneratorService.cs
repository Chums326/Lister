using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChumsLister.Core.Services
{
    public class AIDescriptionGeneratorService
    {
        private readonly IAIService _aiService;
        private readonly ISettingsService _settingsService;

        public AIDescriptionGeneratorService(
            IAIService aiService,
            ISettingsService settingsService)
        {
            _aiService = aiService;
            _settingsService = settingsService;
        }

        public async Task<string> GenerateDescriptionAsync(ListingWizardData listingData)
        {
            try
            {
                // Build detailed prompt using product information
                var prompt = BuildDescriptionPrompt(listingData);

                // Get completion from AI service
                var description = await _aiService.GetCompletionAsync(prompt);

                return !string.IsNullOrWhiteSpace(description)
                    ? description
                    : "Unable to generate description. Please try again later.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating description: {ex.Message}");
                return $"Error generating description: {ex.Message}";
            }
        }

        public async Task<List<string>> GenerateTitleSuggestionsAsync(ListingWizardData listingData)
        {
            try
            {
                if (_aiService == null)
                    return new List<string>();

                var prompt = BuildTitleSuggestionsPrompt(listingData);
                var response = await _aiService.GetCompletionAsync(prompt);

                if (string.IsNullOrEmpty(response))
                    return new List<string>();

                // Parse response into list of titles
                var titles = new List<string>();
                var lines = response
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line));

                foreach (var line in lines)
                {
                    // Remove numbering and bullet points
                    var title = Regex.Replace(line, @"^[\d\.\)\-\*\•]+\s*", "");
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        titles.Add(title);
                    }
                }

                return titles;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating title suggestions: {ex.Message}");
                return new List<string>();
            }
        }

        private string BuildDescriptionPrompt(ListingWizardData listingData)
        {
            // Get description style from settings
            var descriptionStyle = _settingsService.GetSetting<string>("DescriptionStyle", "Detailed");
            var includeHtml = _settingsService.GetSetting<bool>("IncludeHtmlFormatting", true);

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Generate a professional product description for an online marketplace listing with the following information:");
            promptBuilder.AppendLine();

            if (!string.IsNullOrWhiteSpace(listingData.Title))
                promptBuilder.AppendLine($"Product Title: {listingData.Title}");

            if (!string.IsNullOrWhiteSpace(listingData.Subtitle))
                promptBuilder.AppendLine($"Subtitle: {listingData.Subtitle}");

            if (!string.IsNullOrWhiteSpace(listingData.Brand))
                promptBuilder.AppendLine($"Brand: {listingData.Brand}");

            if (!string.IsNullOrWhiteSpace(listingData.MPN))
                promptBuilder.AppendLine($"Model Number: {listingData.MPN}");

            if (!string.IsNullOrWhiteSpace(listingData.CustomSku))
                promptBuilder.AppendLine($"Custom SKU: {listingData.CustomSku}");

            if (!string.IsNullOrWhiteSpace(listingData.UPC))
                promptBuilder.AppendLine($"UPC: {listingData.UPC}");

            if (!string.IsNullOrWhiteSpace(listingData.EAN))
                promptBuilder.AppendLine($"EAN: {listingData.EAN}");

            if (!string.IsNullOrWhiteSpace(listingData.ISBN))
                promptBuilder.AppendLine($"ISBN: {listingData.ISBN}");

            if (!string.IsNullOrWhiteSpace(listingData.ConditionName))
            {
                var conditionDesc = string.IsNullOrWhiteSpace(listingData.ConditionDescription)
                    ? ""
                    : $". {listingData.ConditionDescription}";
                promptBuilder.AppendLine($"Condition: {listingData.ConditionName}{conditionDesc}");
            }

            if (!string.IsNullOrWhiteSpace(listingData.PrimaryCategoryName))
                promptBuilder.AppendLine($"Primary Category: {listingData.PrimaryCategoryName}");

            if (!string.IsNullOrWhiteSpace(listingData.SecondaryCategoryName))
                promptBuilder.AppendLine($"Secondary Category: {listingData.SecondaryCategoryName}");

            // Add item specifics as features
            if (listingData.ItemSpecifics != null && listingData.ItemSpecifics.Any())
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Item Specifics:");
                foreach (var kvp in listingData.ItemSpecifics)
                {
                    var key = kvp.Key;
                    var values = kvp.Value;
                    var concatenated = values != null && values.Any()
                        ? string.Join(", ", values)
                        : "N/A";
                    promptBuilder.AppendLine($"- {key}: {concatenated}");
                }
            }

            // Add style instructions
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Style Instructions:");

            switch (descriptionStyle.ToLower())
            {
                case "brief":
                    promptBuilder.AppendLine("Create a brief, concise description focusing only on the most important details. Keep it under 100 words.");
                    break;
                case "conversational":
                    promptBuilder.AppendLine("Create a friendly, conversational description that engages potential buyers. Use a personal tone.");
                    break;
                case "technical":
                    promptBuilder.AppendLine("Create a detailed technical description focusing on specifications and features. Use precise language.");
                    break;
                case "seo-optimized":
                    promptBuilder.AppendLine("Create an SEO-optimized description including relevant keywords naturally. Focus on benefits and features.");
                    break;
                default:
                    promptBuilder.AppendLine("Create a detailed description highlighting features, benefits, and specifications. Use persuasive language.");
                    break;
            }

            // HTML formatting
            if (includeHtml)
            {
                promptBuilder.AppendLine("Format the description with basic HTML for better readability. Use <h2> for section headings, <ul> and <li> for lists, <strong> for important points, and <br> for line breaks.");
            }
            else
            {
                promptBuilder.AppendLine("Do not use any HTML formatting, just plain text with proper spacing.");
            }

            return promptBuilder.ToString();
        }

        private string BuildTitleSuggestionsPrompt(ListingWizardData listingData)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Generate 5 SEO-optimized title suggestions for an online marketplace listing with the following information:");
            promptBuilder.AppendLine();

            if (!string.IsNullOrWhiteSpace(listingData.Title))
                promptBuilder.AppendLine($"Current Title: {listingData.Title}");

            if (!string.IsNullOrWhiteSpace(listingData.Brand))
                promptBuilder.AppendLine($"Brand: {listingData.Brand}");

            if (!string.IsNullOrWhiteSpace(listingData.MPN))
                promptBuilder.AppendLine($"Model Number: {listingData.MPN}");

            if (!string.IsNullOrWhiteSpace(listingData.PrimaryCategoryName))
                promptBuilder.AppendLine($"Category: {listingData.PrimaryCategoryName}");

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Guidelines:");
            promptBuilder.AppendLine("1. Each title should be 80 characters or less");
            promptBuilder.AppendLine("2. Include the brand name and model number if available");
            promptBuilder.AppendLine("3. Use keywords that buyers would search for");
            promptBuilder.AppendLine("4. Avoid ALL CAPS and excessive punctuation");
            promptBuilder.AppendLine("5. List each suggestion on a separate line");

            return promptBuilder.ToString();
        }
    }
}
