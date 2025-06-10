using ChumsLister.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ProductData = ChumsLister.Core.Models.ProductData;





namespace ChumsLister.Core.Services
{
    public class AnthropicAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
        private bool _isConfigured = false;
        private string _availableModel = null;

        public async Task<string> OptimizeImagesAsync(ProductData product)
        {
            try
            {
                var imageAnalysis = AnalyzeImagePaths(product.ImagePaths);

                var prompt = $@"Analyze these product images for an eBay listing and provide optimization suggestions:

Product Details:
- Title: {product.Title}
- Category: {product.ItemType}
- Brand: {product.BrandModel}
- Condition: {product.Condition}
- Number of images: {product.ImagePaths?.Count ?? 0}

Image Analysis:
{imageAnalysis}

Please provide specific suggestions for:
1. Image order optimization (which should be the main image)
2. Missing image types that would improve the listing
3. Image quality and presentation improvements
4. Lighting and composition recommendations

Keep suggestions concise, actionable, and focused on eBay best practices. Format as numbered bullet points.";

                var response = await GetCompletionAsync(prompt);
                return response?.Trim() ?? GetFallbackImageSuggestions(product);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AI image optimization: {ex.Message}");
                return GetFallbackImageSuggestions(product);
            }
        }

        private string AnalyzeImagePaths(List<string> imagePaths)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                return "No images provided";
            }

            var analysis = new List<string>();

            for (int i = 0; i < imagePaths.Count; i++)
            {
                var path = imagePaths[i];
                var fileName = System.IO.Path.GetFileName(path);
                var isUrl = Uri.IsWellFormedUriString(path, UriKind.Absolute);

                analysis.Add($"Image {i + 1}: {fileName} ({(isUrl ? "URL" : "Local file")})");
            }

            return string.Join("\n", analysis);
        }

        private string GetFallbackImageSuggestions(ProductData product)
        {
            var suggestions = new List<string>();
            var imageCount = product.ImagePaths?.Count ?? 0;

            suggestions.Add("Image Optimization Suggestions:");
            suggestions.Add("");

            if (imageCount == 0)
            {
                suggestions.Add("1. Add at least 3-5 high-quality images");
                suggestions.Add("2. Include a main product shot with white background");
                suggestions.Add("3. Add detail shots showing key features");
                suggestions.Add("4. Consider lifestyle images showing product in use");
            }
            else if (imageCount == 1)
            {
                suggestions.Add("1. Your main image should show the entire product clearly");
                suggestions.Add("2. Add close-up detail shots of important features");
                suggestions.Add("3. Include different angles (front, back, sides)");
                suggestions.Add("4. Consider adding packaging or accessories shots");
            }
            else
            {
                suggestions.Add("1. Ensure your first image is the best overall product shot");
                suggestions.Add("2. Use natural lighting or professional studio lighting");
                suggestions.Add("3. Include measurements or size comparison images");
                suggestions.Add("4. Show any included accessories or parts");
            }

            suggestions.Add("");
            suggestions.Add("General Tips:");
            suggestions.Add("• Use clean, uncluttered backgrounds");
            suggestions.Add("• Ensure images are in focus and well-lit");
            suggestions.Add("• Show any flaws honestly for used items");
            suggestions.Add("• Use consistent lighting across all photos");

            return string.Join("\n", suggestions);
        }

        // List of models to try, in order of preference
        private readonly string[] _modelOptions = new string[]
        {
            "claude-3-5-haiku-20241022",    // First try the model from your screenshot 
            "claude-3-7-sonnet-20250219",   // Second model from your screenshot
            "claude-3-opus-20240229",
            "claude-3-haiku-20240307",
            "claude-3-5-sonnet-20240620",
            "claude-instant-1",
            "claude-1.3"
        };

        public AnthropicAIService(IConfiguration configuration)
        {
            try
            {
                Debug.WriteLine("Initializing AnthropicAIService...");

                // Attempt to get the API key
                _apiKey = configuration["Anthropic:ApiKey"];

                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    Debug.WriteLine("ERROR: Anthropic API key is missing in configuration");
                    throw new InvalidOperationException("Anthropic API key is not configured.");
                }

                Debug.WriteLine($"Anthropic API key found successfully: {_apiKey.Substring(0, 10)}...");

                _httpClient = new HttpClient();

                // Configure timeout
                _httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Configure headers with the correct API version
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                );

                _isConfigured = true;
                Debug.WriteLine("AnthropicAIService initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR initializing AnthropicAIService: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't throw - allow service to be created but in a non-functional state
            }
        }

        // Synchronous method implementations added to satisfy the interface
        public string EnhanceText(string text)
        {
            // This is a synchronous wrapper for the async version
            // We'll use the Task.Run to call the async method and wait for it to complete
            try
            {
                Debug.WriteLine("EnhanceText (sync) called");
                if (!_isConfigured)
                {
                    Debug.WriteLine("EnhanceText called but service is not configured");
                    return "Text could not be enhanced: AI service is not properly configured.";
                }

                if (string.IsNullOrEmpty(text))
                {
                    Debug.WriteLine("Text is empty");
                    return text; // Return the original text
                }

                // Create a product data object with just the text
                var product = new ProductData
                {
                    Description = text
                };

                // Call the async method and wait for the result
                var task = Task.Run(() => EnhanceDescriptionAsync(product));
                task.Wait();

                // Return the result
                return task.Result ?? text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EnhanceText: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // Return original text in case of error
                return text;
            }
        }

        public string OptimizeTitle(string title)
        {
            // This is a synchronous wrapper for the async version
            try
            {
                Debug.WriteLine("OptimizeTitle (sync) called");
                if (!_isConfigured)
                {
                    Debug.WriteLine("OptimizeTitle called but service is not configured");
                    return "Title could not be optimized: AI service is not properly configured.";
                }

                if (string.IsNullOrEmpty(title))
                {
                    Debug.WriteLine("Title is empty");
                    return title; // Return the original title
                }

                // Create a product data object with just the title
                var product = new ProductData
                {
                    Title = title
                };

                // Call the async method and wait for the result
                var task = Task.Run(() => SuggestTitleAsync(product));
                task.Wait();

                // Return the result
                return task.Result ?? title;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OptimizeTitle: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // Return original title in case of error
                return title;
            }
        }

        public async Task<string> EnhanceDescriptionAsync(ProductData product)
        {
            Debug.WriteLine("EnhanceDescriptionAsync called");

            if (!_isConfigured)
            {
                Debug.WriteLine("EnhanceDescriptionAsync called but service is not configured");
                throw new InvalidOperationException("AI service is not properly configured.");
            }

            if (string.IsNullOrEmpty(product.Description))
            {
                Debug.WriteLine("Product description is empty");
                throw new ArgumentException("Product description cannot be empty");
            }

            Debug.WriteLine($"Building prompt for product: {product.Title}");
            string prompt = "Enhance the following product description to make it more appealing to buyers. " +
                           "Include keywords and highlight key features. Product details:\n\n" +
                           $"Title: {product.Title}\n" +
                           $"Model: {product.ModelNumber}\n" +
                           $"Original Description: {product.Description}\n" +
                           $"Features: {product.Features}\n" +
                           $"Specifications: {product.Specifications}\n\n" +
                           "Enhanced Description:";

            Debug.WriteLine("Calling SendAnthropicRequest");
            return await SendAnthropicRequest(prompt, 500);
        }

        public async Task<string> GenerateItemSpecificsAsync(ProductData product)
        {
            Debug.WriteLine("GenerateItemSpecificsAsync called");

            if (!_isConfigured)
            {
                Debug.WriteLine("GenerateItemSpecificsAsync called but service is not configured");
                throw new InvalidOperationException("AI service is not properly configured.");
            }

            Debug.WriteLine($"Building prompt for product: {product.Title}");
            string prompt = "Generate a JSON dictionary of key item specifics for this product that would be useful " +
                           "for marketplace listings. Extract factual details only.\n\n" +
                           $"Title: {product.Title}\n" +
                           $"Model: {product.ModelNumber}\n" +
                           $"Description: {product.Description}\n" +
                           $"Features: {product.Features}\n" +
                           $"Specifications: {product.Specifications}\n" +
                           $"Dimensions: {product.Dimensions}\n\n" +
                           "Item specifics in JSON format:";

            Debug.WriteLine("Calling SendAnthropicRequest");
            return await SendAnthropicRequest(prompt, 300);
        }

        public async Task<string> SuggestTitleAsync(ProductData product)
        {
            Debug.WriteLine("SuggestTitleAsync called");

            if (!_isConfigured)
            {
                Debug.WriteLine("SuggestTitleAsync called but service is not configured");
                throw new InvalidOperationException("AI service is not properly configured.");
            }

            Debug.WriteLine($"Building prompt for product: {product.Title}");
            string prompt = "Generate an optimized marketplace listing title (max 80 characters) for this product " +
                           "that includes key search terms:\n\n" +
                           $"Current Title: {product.Title}\n" +
                           $"Brand/Model: {product.BrandModel}\n" +
                           $"Model Number: {product.ModelNumber}\n" +
                           $"Type: {product.ItemType}\n\n" +
                           "Optimized Title:";

            Debug.WriteLine("Calling SendAnthropicRequest");
            return await SendAnthropicRequest(prompt, 80);
        }

        private async Task<string> SendAnthropicRequest(string prompt, int maxTokens, int retryCount = 3)
        {
            Debug.WriteLine($"SendAnthropicRequest called with max tokens: {maxTokens}");
            Debug.WriteLine($"Prompt preview: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");

            // Find a working model if we haven't already
            if (_availableModel == null)
            {
                Debug.WriteLine("No model selected yet, finding available model...");
                await FindAvailableModelAsync();

                if (_availableModel == null)
                {
                    throw new ApplicationException("No available Claude models found for this API key");
                }
            }

            for (int attempt = 1; attempt <= retryCount; attempt++)
            {
                try
                {
                    Debug.WriteLine($"Attempt {attempt} of {retryCount}");
                    Debug.WriteLine("Creating request body");

                    var requestBody = new
                    {
                        model = _availableModel,
                        max_tokens = maxTokens,
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        }
                    };

                    Debug.WriteLine($"Request body created for model: {requestBody.model}");
                    Debug.WriteLine($"API URL: {AnthropicApiUrl}");

                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    string jsonRequest = JsonSerializer.Serialize(requestBody, jsonOptions);
                    Debug.WriteLine($"Request JSON preview: {jsonRequest.Substring(0, Math.Min(500, jsonRequest.Length))}...");

                    Debug.WriteLine("Preparing to send API request");

                    var response = await _httpClient.PostAsJsonAsync(AnthropicApiUrl, requestBody);
                    Debug.WriteLine($"API response status: {response.StatusCode}");

                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Raw response content preview: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"API error: {response.StatusCode}, Content: {responseContent}");

                        // If model not found error, try to find a different model
                        if (responseContent.Contains("not_found_error") && responseContent.Contains(_availableModel))
                        {
                            Debug.WriteLine($"Model {_availableModel} is no longer available, finding a new model...");
                            _availableModel = null;
                            await FindAvailableModelAsync();

                            if (_availableModel == null)
                            {
                                throw new ApplicationException("No available Claude models found for this API key");
                            }

                            Debug.WriteLine($"Found new model: {_availableModel}, retrying request");
                            continue;
                        }

                        if (attempt < retryCount)
                        {
                            int delay = attempt * 1000;
                            Debug.WriteLine($"Will retry in {delay}ms...");
                            await Task.Delay(delay);
                            continue;
                        }

                        throw new HttpRequestException($"Anthropic API returned error: {response.StatusCode}", null, response.StatusCode);
                    }

                    Debug.WriteLine("Response was successful");
                    Debug.WriteLine($"Response content length: {responseContent.Length}");

                    Debug.WriteLine("Parsing JSON response");
                    var responseJson = JsonDocument.Parse(responseContent);

                    Debug.WriteLine("Extracting content from response");

                    if (responseJson.RootElement.TryGetProperty("content", out var contentArray))
                    {
                        Debug.WriteLine($"Content array with {contentArray.GetArrayLength()} items");

                        if (contentArray.GetArrayLength() == 0)
                        {
                            Debug.WriteLine("WARNING: Content array is empty");
                            return string.Empty;
                        }

                        var firstContent = contentArray.EnumerateArray().First();

                        if (!firstContent.TryGetProperty("type", out var typeElement))
                        {
                            Debug.WriteLine("WARNING: Content item does not have 'type' property");
                            throw new JsonException("Content item does not have expected 'type' property");
                        }

                        string contentType = typeElement.GetString();
                        Debug.WriteLine($"First content item type: {contentType}");

                        if (contentType != "text")
                        {
                            Debug.WriteLine($"WARNING: Unexpected content type: {contentType}");
                            throw new JsonException($"Unexpected content type: {contentType}");
                        }

                        if (!firstContent.TryGetProperty("text", out var textElement))
                        {
                            Debug.WriteLine("WARNING: Content item does not have 'text' property");
                            throw new JsonException("Content item does not have expected 'text' property");
                        }

                        var textContent = textElement.GetString();
                        Debug.WriteLine($"Extracted text content length: {textContent?.Length ?? 0}");

                        if (!string.IsNullOrEmpty(textContent))
                        {
                            Debug.WriteLine($"Content preview: {textContent.Substring(0, Math.Min(50, textContent.Length))}...");
                            return textContent.Trim();
                        }
                        else
                        {
                            Debug.WriteLine("WARNING: Extracted content is null or empty");
                            return string.Empty;
                        }
                    }
                    else if (responseJson.RootElement.TryGetProperty("completion", out var completionElement))
                    {
                        // Handle older API format
                        string completion = completionElement.GetString();
                        Debug.WriteLine($"Extracted completion length: {completion?.Length ?? 0}");

                        if (!string.IsNullOrEmpty(completion))
                        {
                            Debug.WriteLine($"Completion preview: {completion.Substring(0, Math.Min(50, completion.Length))}...");
                            return completion.Trim();
                        }
                        else
                        {
                            Debug.WriteLine("WARNING: Extracted completion is null or empty");
                            return string.Empty;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("WARNING: Unable to find content or completion in response");
                        Debug.WriteLine($"Full response: {responseContent}");
                        throw new JsonException("Response does not contain expected content or completion property");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"HTTP Error on attempt {attempt}: {ex.Message}");
                    Debug.WriteLine($"Status code: {ex.StatusCode}");

                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        Debug.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                    }

                    if (attempt < retryCount)
                    {
                        int delay = attempt * 1000;
                        Debug.WriteLine($"Will retry in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    throw new ApplicationException($"Failed to communicate with Anthropic API after {retryCount} attempts", ex);
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"JSON Parsing Error on attempt {attempt}: {ex.Message}");

                    if (attempt < retryCount)
                    {
                        int delay = attempt * 1000;
                        Debug.WriteLine($"Will retry in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    throw new ApplicationException("Failed to parse Anthropic API response", ex);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unexpected Error on attempt {attempt}: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                    if (attempt < retryCount)
                    {
                        int delay = attempt * 1000;
                        Debug.WriteLine($"Will retry in {delay}ms...");
                        await Task.Delay(delay);
                        continue;
                    }

                    throw;
                }
            }

            throw new ApplicationException($"Failed to communicate with Anthropic API after {retryCount} attempts");
        }

        // Implement this method in your AnthropicAIService class
        public async Task<string> GetCompletionAsync(string prompt)
        {
            // Your implementation - this is placeholder code
            var response = await _httpClient.PostAsync(
                "https://api.anthropic.com/v1/complete",
                new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        prompt = prompt,
                        model = "claude-2",
                        max_tokens_to_sample = 1000
                    }),
                    Encoding.UTF8,
                    "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Parse the JSON response to extract the completion
                return content;
            }

            return "Error generating content";
        }

        private async Task FindAvailableModelAsync()
        {
            Debug.WriteLine("Finding available models...");

            foreach (string model in _modelOptions)
            {
                try
                {
                    Debug.WriteLine($"Testing model: {model}");

                    var requestBody = new
                    {
                        model = model,
                        max_tokens = 10,
                        messages = new[]
                        {
                            new { role = "user", content = "Hello, this is a test." }
                        }
                    };

                    var response = await _httpClient.PostAsJsonAsync(AnthropicApiUrl, requestBody);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    Debug.WriteLine($"Model {model} test result: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"Found working model: {model}");
                        _availableModel = model;
                        return;
                    }
                    else
                    {
                        Debug.WriteLine($"Model {model} not available: {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error testing model {model}: {ex.Message}");
                }
            }

            Debug.WriteLine("WARNING: No working models found!");
        }
    }
}