using ChumsLister.Core.Models;
namespace ChumsLister.Core.Interfaces
{
    /// <summary>
    /// Interface for AI service operations
    /// </summary>
    public interface IAIService
    {
        Task<string> GetCompletionAsync(string prompt);

        /// <summary>
        /// Enhances a product description asynchronously
        /// </summary>
        /// <param name="productData">The product data containing the description to enhance</param>
        /// <returns>An enhanced description</returns>
        Task<string> EnhanceDescriptionAsync(ProductData productData);

        /// <summary>
        /// Suggests an optimized title for a product asynchronously
        /// </summary>
        /// <param name="productData">The product data containing information to generate the title</param>
        /// <returns>An optimized title</returns>
        Task<string> SuggestTitleAsync(ProductData productData);

        /// <summary>
        /// Generates item specifics for a product in JSON format asynchronously
        /// </summary>
        /// <param name="productData">The product data to generate item specifics for</param>
        /// <returns>A JSON string containing key-value pairs of item specifics</returns>
        Task<string> GenerateItemSpecificsAsync(ProductData productData);

        /// <summary>
        /// Enhances text (synchronous version)
        /// </summary>
        /// <param name="text">The text to enhance</param>
        /// <returns>Enhanced text</returns>
        string EnhanceText(string text);

        /// <summary>
        /// Optimizes a title (synchronous version)
        /// </summary>
        /// <param name="title">The title to optimize</param>
        /// <returns>An optimized title</returns>
        string OptimizeTitle(string title);

        /// <summary>
        /// Analyzes product images and provides optimization suggestions
        /// </summary>
        /// <param name="product">The product data containing image information</param>
        /// <returns>AI-generated suggestions for image optimization</returns>
        Task<string> OptimizeImagesAsync(ProductData product);
    }
}