using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChumsLister.Core.Helpers
{
    public static class ImageValidationHelper
    {
        private static readonly string[] ValidImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
        private static readonly long MaxFileSizeBytes = 12 * 1024 * 1024; // 12MB
        private static readonly int MaxImageCount = 12; // eBay limit

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
        }

        public static ValidationResult ValidateLocalImage(string filePath)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(filePath))
            {
                result.IsValid = false;
                result.Errors.Add("File path is empty");
                return result;
            }

            if (!File.Exists(filePath))
            {
                result.IsValid = false;
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            // Check file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!ValidImageExtensions.Contains(extension))
            {
                result.IsValid = false;
                result.Errors.Add($"Unsupported file type: {extension}. Supported types: {string.Join(", ", ValidImageExtensions)}");
                return result;
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                result.IsValid = false;
                result.Errors.Add($"File too large: {fileInfo.Length / (1024 * 1024)}MB. Maximum size: {MaxFileSizeBytes / (1024 * 1024)}MB");
                return result;
            }

            // Warnings for optimization
            if (fileInfo.Length > 5 * 1024 * 1024) // 5MB
            {
                result.Warnings.Add("Large file size may slow down listing creation");
            }

            return result;
        }

        public static ValidationResult ValidateImageUrl(string imageUrl)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                result.IsValid = false;
                result.Errors.Add("Image URL is empty");
                return result;
            }

            if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            {
                result.IsValid = false;
                result.Errors.Add("Invalid URL format");
                return result;
            }

            var uri = new Uri(imageUrl);
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                result.IsValid = false;
                result.Errors.Add("URL must use HTTP or HTTPS protocol");
                return result;
            }

            // Check if URL looks like an image
            var path = uri.AbsolutePath.ToLowerInvariant();
            if (!ValidImageExtensions.Any(ext => path.EndsWith(ext)))
            {
                result.Warnings.Add("URL does not appear to point to an image file");
            }

            return result;
        }

        public static ValidationResult ValidateImageCollection(List<string> imagePaths, List<string> imageUrls)
        {
            var result = new ValidationResult { IsValid = true };
            int totalImages = (imagePaths?.Count ?? 0) + (imageUrls?.Count ?? 0);

            if (totalImages == 0)
            {
                result.Warnings.Add("No images added - eBay listings perform better with images");
                return result;
            }

            if (totalImages > MaxImageCount)
            {
                result.IsValid = false;
                result.Errors.Add($"Too many images: {totalImages}. Maximum allowed: {MaxImageCount}");
                return result;
            }

            // Validate each local image
            if (imagePaths != null)
            {
                foreach (var path in imagePaths)
                {
                    var validation = ValidateLocalImage(path);
                    if (!validation.IsValid)
                    {
                        result.IsValid = false;
                        result.Errors.AddRange(validation.Errors.Select(e => $"Local image '{Path.GetFileName(path)}': {e}"));
                    }
                    result.Warnings.AddRange(validation.Warnings.Select(w => $"Local image '{Path.GetFileName(path)}': {w}"));
                }
            }

            // Validate each URL
            if (imageUrls != null)
            {
                foreach (var url in imageUrls)
                {
                    var validation = ValidateImageUrl(url);
                    if (!validation.IsValid)
                    {
                        result.IsValid = false;
                        result.Errors.AddRange(validation.Errors.Select(e => $"Image URL '{url}': {e}"));
                    }
                    result.Warnings.AddRange(validation.Warnings.Select(w => $"Image URL '{url}': {w}"));
                }
            }

            return result;
        }

        public static bool IsImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return ValidImageExtensions.Contains(extension);
        }

        public static string GetImageFileFilter()
        {
            return "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|" +
                   "JPEG Files|*.jpg;*.jpeg|" +
                   "PNG Files|*.png|" +
                   "All Files|*.*";
        }
    }
}