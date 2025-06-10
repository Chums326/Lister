using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ChumsLister.Core.Services.Marketplaces
{
    public class EbayMarketplaceService : IMarketplaceService, IEbayService
    {
        private readonly HttpClient _httpClient;
        private readonly IEbayTokenStore _tokenStore;
        private Dictionary<string, string> _cachedSkuLookup;
        private DateTime _cacheExpiration = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromHours(1);

        // TODO: Replace with your actual eBay developer credentials
        private const string EBAY_DEV_ID = "YOUR_EBAY_DEV_ID";
        private const string EBAY_APP_ID = "YOUR_EBAY_APP_ID";
        private const string EBAY_CERT_ID = "YOUR_EBAY_CERT_ID";

        public EbayMarketplaceService(HttpClient httpClient, IEbayTokenStore tokenStore)
        {
            _httpClient = httpClient;
            _tokenStore = tokenStore;
        }

        public string Name => "eBay";
        public string PlatformName => "eBay";
        public bool IsConfigured => true;
        public bool IsAuthenticated => !string.IsNullOrEmpty(_tokenStore.AccessToken);
        public bool SupportsDraftListings => false;

        private async Task EnsureTokenIsValidAsync()
        {
            if (_tokenStore.IsExpired())
                await _tokenStore.RefreshTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        }

        #region — Recent Sales & Orders (Working) —

        public async Task<List<MarketplaceOrderSummary>> GetRecentSalesAsync(DateTime startDate)
        {
            await EnsureTokenIsValidAsync();

            if (_cachedSkuLookup == null || DateTime.UtcNow > _cacheExpiration)
            {
                Debug.WriteLine("Building SKU lookup cache...");
                _cachedSkuLookup = await BuildItemSkuLookupAsync();
                _cacheExpiration = DateTime.UtcNow.Add(_cacheLifetime);
                Debug.WriteLine($"SKU cache built with {_cachedSkuLookup.Count} items. Expires at {_cacheExpiration} UTC");
            }

            var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetOrdersRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <CreateTimeFrom>{startDate:yyyy-MM-ddTHH:mm:ss.fffZ}</CreateTimeFrom>
  <CreateTimeTo>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}</CreateTimeTo>
  <OrderRole>Seller</OrderRole>
  <OrderStatus>All</OrderStatus>
  <DetailLevel>ReturnAll</DetailLevel>
  <Pagination>
    <EntriesPerPage>200</EntriesPerPage>
    <PageNumber>1</PageNumber>
  </Pagination>
</GetOrdersRequest>";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetOrders");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);
            var xml = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);
            var ns = doc.Root?.Name.Namespace;

            var orders = new List<MarketplaceOrderSummary>();

            foreach (var orderElem in doc.Descendants(ns + "Order"))
            {
                var txn = orderElem.Element(ns + "TransactionArray")?.Element(ns + "Transaction");
                if (txn == null) continue;

                var itemId = txn.Element(ns + "Item")?.Element(ns + "ItemID")?.Value;
                var orderId = orderElem.Element(ns + "OrderID")?.Value;
                var title = txn.Element(ns + "Item")?.Element(ns + "Title")?.Value ?? "";
                var sku = txn.Element(ns + "Item")?.Element(ns + "SKU")?.Value;

                // Get SKU from cache if not in order
                if (string.IsNullOrWhiteSpace(sku) && itemId != null &&
                    _cachedSkuLookup.TryGetValue(itemId, out var foundSku))
                {
                    sku = foundSku;
                }

                // Determine shipping status
                string shippingStatus = orderElem.Element(ns + "ShippedTime") != null ? "Shipped" : "";
                if (string.IsNullOrEmpty(shippingStatus))
                {
                    var paymentStatus = orderElem.Element(ns + "CheckoutStatus")?.Element(ns + "PaymentStatus")?.Value ?? "";
                    if (paymentStatus.Equals("Complete", StringComparison.OrdinalIgnoreCase))
                    {
                        shippingStatus = "NotShipped";
                    }
                    else
                    {
                        shippingStatus = "Pending";
                    }
                }

                orders.Add(new MarketplaceOrderSummary
                {
                    OrderId = orderId,
                    BuyerUsername = orderElem.Element(ns + "BuyerUserID")?.Value,
                    ItemTitle = title,
                    SKU = sku,
                    SaleDate = DateTime.TryParse(txn.Element(ns + "CreatedDate")?.Value, out var dt) ? dt : DateTime.MinValue,
                    TotalAmount = decimal.TryParse(orderElem.Element(ns + "Total")?.Value, out var total) ? total : 0m,
                    OrderStatus = orderElem.Element(ns + "OrderStatus")?.Value,
                    PaymentStatus = orderElem.Element(ns + "CheckoutStatus")?.Element(ns + "PaymentStatus")?.Value ?? "",
                    ShippingStatus = shippingStatus
                });
            }

            return orders;
        }

        private async Task<Dictionary<string, string>> BuildItemSkuLookupAsync()
        {
            var lookup = new Dictionary<string, string>();
            const int maxPages = 3;

            for (int page = 1; page <= maxPages; page++)
            {
                var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetSellerListRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <EndTimeFrom>{DateTime.UtcNow.AddDays(-30):yyyy-MM-ddTHH:mm:ss.fffZ}</EndTimeFrom>
  <EndTimeTo>{DateTime.UtcNow.AddDays(30):yyyy-MM-ddTHH:mm:ss.fffZ}</EndTimeTo>
  <Pagination>
    <EntriesPerPage>200</EntriesPerPage>
    <PageNumber>{page}</PageNumber>
  </Pagination>
  <DetailLevel>ReturnAll</DetailLevel>
  <OutputSelector>ItemID</OutputSelector>
  <OutputSelector>SKU</OutputSelector>
</GetSellerListRequest>";

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
                request.Headers.Add("X-EBAY-API-CALL-NAME", "GetSellerList");
                request.Headers.Add("X-EBAY-API-SITEID", "0");
                request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
                request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

                var response = await _httpClient.SendAsync(request);
                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace;

                foreach (var item in doc.Descendants(ns + "Item"))
                {
                    var itemId = item.Element(ns + "ItemID")?.Value;
                    var sku = item.Element(ns + "SKU")?.Value;
                    if (!string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(sku))
                    {
                        lookup[itemId] = sku;
                    }
                }
            }

            return lookup;
        }

        public async Task<OrderDetails> GetOrderDetailsAsync(string orderId)
        {
            await EnsureTokenIsValidAsync();

            var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetOrdersRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <OrderIDArray>
    <OrderID>{orderId}</OrderID>
  </OrderIDArray>
  <OrderRole>Seller</OrderRole>
  <DetailLevel>ReturnAll</DetailLevel>
</GetOrdersRequest>";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetOrders");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);
            var xml = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);
            var ns = doc.Root?.Name.Namespace;

            var orderElem = doc.Descendants(ns + "Order").FirstOrDefault();
            if (orderElem == null) return null;

            var txn = orderElem.Element(ns + "TransactionArray")?.Element(ns + "Transaction");
            if (txn == null) return null;

            var details = new OrderDetails
            {
                OrderId = orderId,
                SKU = txn.Element(ns + "Item")?.Element(ns + "SKU")?.Value,
                BuyerName = orderElem.Element(ns + "BuyerUserID")?.Value,
                ItemTitle = txn.Element(ns + "Item")?.Element(ns + "Title")?.Value,
                OrderDate = DateTime.TryParse(txn.Element(ns + "CreatedDate")?.Value, out var date) ? date : DateTime.MinValue,
                OrderTotal = decimal.TryParse(orderElem.Element(ns + "Total")?.Value, out var total) ? total : 0m
            };

            // Build shipping address
            var shippingAddress = orderElem.Element(ns + "ShippingAddress");
            if (shippingAddress != null)
            {
                var name = shippingAddress.Element(ns + "Name")?.Value ?? "";
                var street1 = shippingAddress.Element(ns + "Street1")?.Value ?? "";
                var street2 = shippingAddress.Element(ns + "Street2")?.Value ?? "";
                var city = shippingAddress.Element(ns + "CityName")?.Value ?? "";
                var state = shippingAddress.Element(ns + "StateOrProvince")?.Value ?? "";
                var zip = shippingAddress.Element(ns + "PostalCode")?.Value ?? "";
                var country = shippingAddress.Element(ns + "CountryName")?.Value ?? "";

                details.BuyerAddress = $"{name}\n{street1}";
                if (!string.IsNullOrEmpty(street2)) details.BuyerAddress += $"\n{street2}";
                details.BuyerAddress += $"\n{city}, {state} {zip}";
                if (!country.Equals("United States", StringComparison.OrdinalIgnoreCase))
                    details.BuyerAddress += $"\n{country}";

                details.BuyerPhone = shippingAddress.Element(ns + "Phone")?.Value ?? "";
            }

            details.PaymentStatus = orderElem.Element(ns + "CheckoutStatus")?.Element(ns + "PaymentStatus")?.Value ?? "";
            details.BuyerEmail = orderElem.Element(ns + "BuyerEmail")?.Value ?? "";

            // Shipping status and tracking
            var shippingDetails = orderElem.Element(ns + "ShippingDetails");
            if (shippingDetails != null)
            {
                var trackElem = shippingDetails.Element(ns + "ShipmentTrackingDetails");
                if (trackElem != null)
                {
                    details.TrackingNumber = trackElem.Element(ns + "ShipmentTrackingNumber")?.Value ?? "";
                    details.ShippingCarrier = trackElem.Element(ns + "ShippingCarrierUsed")?.Value ?? "";
                }
            }

            if (orderElem.Element(ns + "ShippedTime") != null)
            {
                details.ShippingStatus = "Shipped";
                details.ShippedDate = DateTime.TryParse(orderElem.Element(ns + "ShippedTime")?.Value, out var shipDate) ? shipDate : null;
            }
            else if (details.PaymentStatus.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                details.ShippingStatus = "NotShipped";
            }
            else
            {
                details.ShippingStatus = "Pending";
            }

            return details;
        }

        #endregion

        #region — Active Listings (Working) —

        public async Task<List<MarketplaceListingSummary>> GetActiveListingsAsync()
        {
            await EnsureTokenIsValidAsync();

            var listings = new List<MarketplaceListingSummary>();
            int page = 1;
            const int maxPages = 10;

            do
            {
                var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetSellerListRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <EndTimeFrom>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}</EndTimeFrom>
  <EndTimeTo>{DateTime.UtcNow.AddDays(120):yyyy-MM-ddTHH:mm:ss.fffZ}</EndTimeTo>
  <ListingStatus>Active</ListingStatus>
  <Pagination>
    <EntriesPerPage>100</EntriesPerPage>
    <PageNumber>{page}</PageNumber>
  </Pagination>
  <DetailLevel>ReturnAll</DetailLevel>
</GetSellerListRequest>";

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
                request.Headers.Add("X-EBAY-API-CALL-NAME", "GetSellerList");
                request.Headers.Add("X-EBAY-API-SITEID", "0");
                request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
                request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

                var response = await _httpClient.SendAsync(request);
                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace;

                foreach (var item in doc.Descendants(ns + "Item"))
                {
                    listings.Add(new MarketplaceListingSummary
                    {
                        ListingId = item.Element(ns + "ItemID")?.Value,
                        Title = item.Element(ns + "Title")?.Value,
                        Price = decimal.TryParse(item.Element(ns + "SellingStatus")?.Element(ns + "CurrentPrice")?.Value, out var p) ? p : 0m,
                        Quantity = int.TryParse(item.Element(ns + "Quantity")?.Value, out var q) ? q : 0
                    });
                }

                var totalPagesStr = doc.Descendants(ns + "PaginationResult").FirstOrDefault()?.Element(ns + "TotalNumberOfPages")?.Value;
                bool hasMore = int.TryParse(totalPagesStr, out var totalPages) && page < totalPages;
                page++;

                if (!hasMore || page > maxPages) break;
            }
            while (true);

            return listings;
        }

        #endregion

        #region — Shipping Rates (Working with Mock Data) —

        public async Task<List<ShippingRateResult>> GetShippingRatesAsync(ShippingRateRequest request)
        {
            await EnsureTokenIsValidAsync();

            // Validate input
            if (string.IsNullOrWhiteSpace(request.FromPostalCode) || string.IsNullOrWhiteSpace(request.ToPostalCode))
            {
                throw new ArgumentException("From and To postal codes are required");
            }

            // Clean postal codes
            var fromZip = System.Text.RegularExpressions.Regex.Replace(request.FromPostalCode, @"[^\d-]", "").Trim();
            var toZip = System.Text.RegularExpressions.Regex.Replace(request.ToPostalCode, @"[^\d-]", "").Trim();

            if (fromZip.Length < 5 || toZip.Length < 5)
            {
                throw new ArgumentException($"Invalid postal codes: From='{fromZip}', To='{toZip}'");
            }

            // For now, return mock data since eBay's shipping API often requires special permissions
            var rates = new List<ShippingRateResult>
            {
                new ShippingRateResult
                {
                    Carrier = "USPS",
                    ServiceName = "USPS Ground Advantage",
                    Cost = 8.50m,
                    DeliveryDays = 5
                },
                new ShippingRateResult
                {
                    Carrier = "USPS",
                    ServiceName = "USPS Priority Mail",
                    Cost = 12.50m,
                    DeliveryDays = 3
                },
                new ShippingRateResult
                {
                    Carrier = "USPS",
                    ServiceName = "USPS Priority Mail Express",
                    Cost = 28.00m,
                    DeliveryDays = 1
                }
            };

            return rates;
        }

        #endregion

        #region — Category Management (Working) —

        // Replace the entire GetCategoriesAsync method in EbayMarketplaceService.cs:

        public async Task<List<EbayCategory>> GetCategoriesAsync(string accountId, string parentId = null)
        {
            await EnsureTokenIsValidAsync();

            var xmlRequest = new StringBuilder();
            xmlRequest.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetCategoriesRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>" + _tokenStore.AccessToken + @"</eBayAuthToken>
  </RequesterCredentials>
  <CategorySiteID>0</CategorySiteID>
  <DetailLevel>ReturnAll</DetailLevel>");

            // Modified approach: If we have a parentId, get ALL categories and filter client-side
            if (!string.IsNullOrEmpty(parentId))
            {
                // First, let's try getting the category features to see if it's actually a leaf
                var isLeaf = await IsCategoryActuallyLeafAsync(parentId);
                if (isLeaf)
                {
                    Debug.WriteLine($"Category {parentId} is a leaf category - no children available");
                    return new List<EbayCategory>();
                }

                // Get all categories with deeper level limit to ensure we capture children
                xmlRequest.AppendLine($"  <CategoryParent>{SecurityElement.Escape(parentId)}</CategoryParent>");
                xmlRequest.AppendLine("  <LevelLimit>3</LevelLimit>"); // Increased from 1 to 3
            }
            else
            {
                // For root categories, get level 1 only
                xmlRequest.AppendLine("  <LevelLimit>1</LevelLimit>");
            }

            xmlRequest.AppendLine("  <ViewAllNodes>true</ViewAllNodes>");
            xmlRequest.AppendLine("</GetCategoriesRequest>");

            Debug.WriteLine("=== GetCategories Request ===");
            Debug.WriteLine($"Parent ID: {parentId ?? "null (root)"}");
            Debug.WriteLine("Request XML:");
            Debug.WriteLine(xmlRequest.ToString());

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetCategories");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest.ToString(), Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);
            var xml = await response.Content.ReadAsStringAsync();

            Debug.WriteLine("=== GetCategories Response ===");
            Debug.WriteLine($"Status: {response.StatusCode}");
            Debug.WriteLine("Response XML (first 2000 chars):");
            Debug.WriteLine(xml.Length > 2000 ? xml.Substring(0, 2000) + "..." : xml);

            var doc = XDocument.Parse(xml);
            var ns = doc.Root?.Name.Namespace;

            // Check for errors
            var ack = doc.Root?.Element(ns + "Ack")?.Value;
            if (ack == "Failure")
            {
                var errorMessage = doc.Descendants(ns + "LongMessage").FirstOrDefault()?.Value ?? "Unknown error";
                Debug.WriteLine($"eBay API Error: {errorMessage}");
                throw new Exception($"eBay API Error: {errorMessage}");
            }

            var categories = new List<EbayCategory>();
            var seenCategoryIds = new HashSet<string>();

            // Count total categories in response
            var allCategoryElements = doc.Descendants(ns + "Category").ToList();
            Debug.WriteLine($"Total Category elements in response: {allCategoryElements.Count}");

            foreach (var categoryElem in allCategoryElements)
            {
                var categoryId = categoryElem.Element(ns + "CategoryID")?.Value;
                var categoryName = categoryElem.Element(ns + "CategoryName")?.Value;
                var categoryParentId = categoryElem.Element(ns + "CategoryParentID")?.Value;
                var categoryLevel = int.TryParse(categoryElem.Element(ns + "CategoryLevel")?.Value, out var lvl) ? lvl : 0;
                var isLeaf = categoryElem.Element(ns + "LeafCategory")?.Value == "true";

                Debug.WriteLine($"Processing: {categoryName} (ID: {categoryId}, Parent: {categoryParentId}, Level: {categoryLevel}, Leaf: {isLeaf})");

                // Skip if we've already seen this category
                if (string.IsNullOrEmpty(categoryId) || !seenCategoryIds.Add(categoryId))
                {
                    Debug.WriteLine($"  -> Skipping (duplicate or no ID)");
                    continue;
                }

                // If we're looking for children of a specific parent
                if (!string.IsNullOrEmpty(parentId))
                {
                    // Skip the parent category itself
                    if (categoryId == parentId)
                    {
                        Debug.WriteLine($"  -> Skipping (is parent itself)");
                        continue;
                    }

                    // For children, we want direct children (parent + 1 level) OR any category with our target as parent
                    bool isDirectChild = categoryParentId == parentId;
                    bool isDescendant = false;

                    // Alternative: Check if this category's path includes our parent
                    if (!isDirectChild)
                    {
                        // Sometimes eBay returns a hierarchy where we need to check the path
                        // Let's check if the parent is in the "path" to this category
                        var pathElement = categoryElem.Element(ns + "CategoryNamePath");
                        if (pathElement != null)
                        {
                            var path = pathElement.Value;
                            Debug.WriteLine($"  -> Category path: {path}");
                            // This is a more complex check - for now, skip non-direct children
                        }
                    }

                    if (!isDirectChild)
                    {
                        Debug.WriteLine($"  -> Skipping (not direct child: parent={categoryParentId}, looking for children of {parentId})");
                        continue;
                    }
                }
                else
                {
                    // For root categories, only include level 1
                    if (categoryLevel != 1)
                    {
                        Debug.WriteLine($"  -> Skipping (not level 1: {categoryLevel})");
                        continue;
                    }
                }

                var category = new EbayCategory
                {
                    CategoryId = categoryId,
                    CategoryName = categoryName,
                    CategoryParentId = categoryParentId,
                    CategoryLevel = categoryLevel,
                    LeafCategory = isLeaf,
                    BestOfferEnabled = categoryElem.Element(ns + "BestOfferEnabled")?.Value == "true"
                };

                categories.Add(category);
                Debug.WriteLine($"  -> Added to results");
            }

            // If we got no results and we were looking for a specific parent, try alternative approach
            if (categories.Count == 0 && !string.IsNullOrEmpty(parentId))
            {
                Debug.WriteLine($"No children found for {parentId}, trying alternative approach...");
                return await GetCategoriesAlternativeApproachAsync(parentId);
            }

            // Sort categories alphabetically by name
            categories = categories.OrderBy(c => c.CategoryName).ToList();

            Debug.WriteLine($"=== Final Result: {categories.Count} categories for parent '{parentId}' ===");
            foreach (var cat in categories.Take(10)) // Show first 10
            {
                Debug.WriteLine($"  - {cat.CategoryName} (ID: {cat.CategoryId}, Leaf: {cat.LeafCategory})");
            }
            if (categories.Count > 10)
            {
                Debug.WriteLine($"  ... and {categories.Count - 10} more");
            }

            return categories;
        }

        // Helper method to check if a category is actually a leaf
        private async Task<bool> IsCategoryActuallyLeafAsync(string categoryId)
        {
            try
            {
                await EnsureTokenIsValidAsync();

                var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetCategoryInfoRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <CategoryID>{categoryId}</CategoryID>
</GetCategoryInfoRequest>";

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
                request.Headers.Add("X-EBAY-API-CALL-NAME", "GetCategoryInfo");
                request.Headers.Add("X-EBAY-API-SITEID", "0");
                request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
                request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var xml = await response.Content.ReadAsStringAsync();
                    var doc = XDocument.Parse(xml);
                    var ns = doc.Root?.Name.Namespace;

                    var isLeaf = doc.Descendants(ns + "LeafCategory").FirstOrDefault()?.Value == "true";
                    Debug.WriteLine($"GetCategoryInfo for {categoryId}: LeafCategory = {isLeaf}");
                    return isLeaf;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if category {categoryId} is leaf: {ex.Message}");
            }

            return false; // Assume it's not a leaf if we can't determine
        }

        // Alternative approach: Get all categories and filter
        private async Task<List<EbayCategory>> GetCategoriesAlternativeApproachAsync(string parentId)
        {
            Debug.WriteLine($"Trying alternative approach for parent {parentId}");

            await EnsureTokenIsValidAsync();

            // Get ALL categories (this might be a large response)
            var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetCategoriesRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <CategorySiteID>0</CategorySiteID>
  <DetailLevel>ReturnAll</DetailLevel>
  <ViewAllNodes>true</ViewAllNodes>
</GetCategoriesRequest>";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetCategories");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);
            var xml = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);
            var ns = doc.Root?.Name.Namespace;

            var categories = new List<EbayCategory>();
            var childrenFound = 0;

            foreach (var categoryElem in doc.Descendants(ns + "Category"))
            {
                var categoryId = categoryElem.Element(ns + "CategoryID")?.Value;
                var categoryParentId = categoryElem.Element(ns + "CategoryParentID")?.Value;
                var categoryName = categoryElem.Element(ns + "CategoryName")?.Value;
                var categoryLevel = int.TryParse(categoryElem.Element(ns + "CategoryLevel")?.Value, out var lvl) ? lvl : 0;
                var isLeaf = categoryElem.Element(ns + "LeafCategory")?.Value == "true";

                // Look for direct children of our parent
                if (categoryParentId == parentId && categoryId != parentId)
                {
                    childrenFound++;
                    if (childrenFound <= 50) // Limit to prevent huge responses
                    {
                        categories.Add(new EbayCategory
                        {
                            CategoryId = categoryId,
                            CategoryName = categoryName,
                            CategoryParentId = categoryParentId,
                            CategoryLevel = categoryLevel,
                            LeafCategory = isLeaf,
                            BestOfferEnabled = categoryElem.Element(ns + "BestOfferEnabled")?.Value == "true"
                        });
                    }
                }
            }

            Debug.WriteLine($"Alternative approach found {childrenFound} children for parent {parentId} (returning {Math.Min(childrenFound, 50)})");

            return categories.OrderBy(c => c.CategoryName).ToList();
        }
        #endregion

        #region — Create Listing (FIXED for Business Policies) —

        
        // Updated method in EbayMarketplaceService class
        public async Task<MarketplaceListingResult> CreateListingAsync(string accountId, ListingWizardData data)
        {
            try
            {
                await EnsureTokenIsValidAsync();

                // Get business policy IDs
                var policyIds = GetHardcodedPolicyIds();
                if (string.IsNullOrEmpty(policyIds.ShippingPolicyId) ||
                    string.IsNullOrEmpty(policyIds.PaymentPolicyId) ||
                    string.IsNullOrEmpty(policyIds.ReturnPolicyId))
                {
                    return new MarketplaceListingResult
                    {
                        Success = false,
                        ErrorMessage = "Business policy IDs not configured. Please update GetHardcodedPolicyIds() method with your actual policy IDs from eBay Seller Hub."
                    };
                }

                // Step 1: Upload local images to eBay first
                var allImageUrls = new List<string>();

                // Add existing URLs
                if (data.ImageUrls != null)
                {
                    allImageUrls.AddRange(data.ImageUrls.Where(url => Uri.IsWellFormedUriString(url, UriKind.Absolute)));
                }

                // Upload local images
                if (data.LocalImagePaths != null && data.LocalImagePaths.Count > 0)
                {
                    Debug.WriteLine($"Uploading {data.LocalImagePaths.Count} local images to eBay...");

                    foreach (var localPath in data.LocalImagePaths.Take(12)) // eBay max 12 images
                    {
                        try
                        {
                            if (File.Exists(localPath))
                            {
                                var imageData = await File.ReadAllBytesAsync(localPath);
                                var fileName = Path.GetFileName(localPath);

                                var uploadedUrl = await UploadImageAsync(imageData, fileName);
                                if (!string.IsNullOrEmpty(uploadedUrl))
                                {
                                    allImageUrls.Add(uploadedUrl);
                                    Debug.WriteLine($"✅ Uploaded: {fileName} -> {uploadedUrl}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ Failed to upload image {localPath}: {ex.Message}");
                            // Continue with other images - don't fail the entire listing
                        }
                    }
                }

                // Step 2: Create the listing with all image URLs
                var xmlRequest = BuildCreateListingXmlFromWizardData(data, policyIds, allImageUrls);

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
                request.Headers.Add("X-EBAY-API-CALL-NAME", "AddFixedPriceItem");
                request.Headers.Add("X-EBAY-API-SITEID", "0");
                request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
                request.Headers.Add("X-EBAY-API-DEV-NAME", EBAY_DEV_ID);
                request.Headers.Add("X-EBAY-API-APP-NAME", EBAY_APP_ID);
                request.Headers.Add("X-EBAY-API-CERT-NAME", EBAY_CERT_ID);

                request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

                var response = await _httpClient.SendAsync(request);
                var responseXml = await response.Content.ReadAsStringAsync();

                Debug.WriteLine("=== CreateListing Response ===");
                Debug.WriteLine(responseXml);

                var result = ParseCreateListingResponse(responseXml);

                // Store uploaded image URLs back in wizard data for reference
                if (result.Success && allImageUrls.Count > 0)
                {
                    data.UploadedImageUrls.Clear();
                    data.UploadedImageUrls.AddRange(allImageUrls);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating eBay listing: {ex.Message}");
                return new MarketplaceListingResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create listing: {ex.Message}"
                };
            }
        }

        

        public async Task<MarketplaceListingResult> CreateListingAsync(ProductData product)
        {
            // Convert ProductData to ListingWizardData
            var wizardData = new ListingWizardData
            {
                Title = product.Title,
                Description = product.Description,
                StartPrice = product.Price,
                Quantity = product.AvailableQuantity,
                CustomSku = product.ModelNumber,
                Brand = product.BrandModel,
                UPC = product.UPC,
                MPN = product.MPN,

                // Set default condition if not specified
                ConditionId = "1000", // Default to "New"

                // Copy image paths
                ImageUrls = product.ImagePaths?.Where(p => p.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>(),
                LocalImagePaths = product.ImagePaths?.Where(p => !p.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>(),

                // Set default listing settings
                ListingType = "FixedPriceItem",
                ListingDuration = "GTC",

                // Convert item specifics if available
                ItemSpecifics = new Dictionary<string, List<string>>()
            };

            // Convert item specifics from ProductData format to ListingWizardData format
            if (product.ItemSpecifics != null)
            {
                foreach (var kvp in product.ItemSpecifics)
                {
                    wizardData.ItemSpecifics[kvp.Key] = new List<string> { kvp.Value };
                }
            }

            // Use the default account ID (you might want to make this configurable)
            string accountId = "default";

            // Call the existing CreateListingAsync method
            return await CreateListingAsync(accountId, wizardData);
        }

        private string BuildCreateListingXmlFromWizardData(ListingWizardData data, PolicyIds policyIds, List<string> imageUrls)
        {
            string titleEsc = SecurityElement.Escape(data.Title ?? "");
            string descCData = data.Description ?? "";
            string priceStr = (data.StartPrice <= 0m) ? "0.01" : data.StartPrice.ToString("F2", CultureInfo.InvariantCulture);
            string qtyStr = (data.Quantity <= 0) ? "1" : data.Quantity.ToString();
            string skuEsc = SecurityElement.Escape(data.CustomSku ?? "");

            // Use selected category from wizard
            string categoryID = data.PrimaryCategoryId ?? "260556"; // Fallback category

            // Build item specifics from wizard data
            var specificsXml = new StringBuilder();

            // Add Brand if available
            if (!string.IsNullOrEmpty(data.Brand))
            {
                specificsXml.Append($@"
        <NameValueList>
          <Name>Brand</Name>
          <Value>{SecurityElement.Escape(data.Brand)}</Value>
        </NameValueList>");
            }

            // Add MPN if available
            if (!string.IsNullOrEmpty(data.MPN))
            {
                specificsXml.Append($@"
        <NameValueList>
          <Name>MPN</Name>
          <Value>{SecurityElement.Escape(data.MPN)}</Value>
        </NameValueList>");
            }

            // Add other item specifics from wizard
            if (data.ItemSpecifics != null)
            {
                foreach (var kvp in data.ItemSpecifics)
                {
                    if (kvp.Key.Equals("Brand", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("MPN", StringComparison.OrdinalIgnoreCase))
                        continue; // Already added above

                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        string nameEsc = SecurityElement.Escape(kvp.Key);
                        string valEsc = SecurityElement.Escape(string.Join(", ", kvp.Value));
                        specificsXml.Append($@"
        <NameValueList>
          <Name>{nameEsc}</Name>
          <Value>{valEsc}</Value>
        </NameValueList>");
                    }
                }
            }

            // Build picture URLs
            string pictureXml = "";
            if (imageUrls != null && imageUrls.Count > 0)
            {
                pictureXml = "<PictureDetails>";
                foreach (var imageUrl in imageUrls.Take(12)) // eBay allows max 12 images
                {
                    pictureXml += $"<PictureURL>{SecurityElement.Escape(imageUrl)}</PictureURL>";
                }
                pictureXml += "</PictureDetails>";
            }
            else
            {
                // Add placeholder if no images
                pictureXml = @"<PictureDetails>
      <PictureURL>https://via.placeholder.com/400x300.jpg?text=No+Image</PictureURL>
    </PictureDetails>";
            }

            // Use condition from wizard data
            string conditionId = data.ConditionId ?? "1000"; // Default to "New"

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<AddFixedPriceItemRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <Item>
    <Title>{titleEsc}</Title>
    <Description><![CDATA[{descCData}]]></Description>
    <PrimaryCategory>
      <CategoryID>{categoryID}</CategoryID>
    </PrimaryCategory>
    <StartPrice currencyID=""USD"">{priceStr}</StartPrice>
    <CategoryMappingAllowed>true</CategoryMappingAllowed>
    <ConditionID>{conditionId}</ConditionID>
    <Country>US</Country>
    <Location>San Jose, CA</Location>
    <Currency>USD</Currency>
    <DispatchTimeMax>3</DispatchTimeMax>
    <ListingDuration>GTC</ListingDuration>
    <ListingType>FixedPriceItem</ListingType>
    {pictureXml}
    <PostalCode>95125</PostalCode>
    <Quantity>{qtyStr}</Quantity>
    <Site>US</Site>
    <SKU>{skuEsc}</SKU>
    <ItemSpecifics>{specificsXml}</ItemSpecifics>
    <BusinessSellerDetails>
      <SellerShippingProfileID>{policyIds.ShippingPolicyId}</SellerShippingProfileID>
      <SellerPaymentProfileID>{policyIds.PaymentPolicyId}</SellerPaymentProfileID>
      <SellerReturnProfileID>{policyIds.ReturnPolicyId}</SellerReturnProfileID>
    </BusinessSellerDetails>
  </Item>
</AddFixedPriceItemRequest>";
        }

        private PolicyIds GetHardcodedPolicyIds()
        {
            // TODO: Replace these with YOUR actual policy IDs from eBay Seller Hub
            // Go to https://www.ebay.com/sh/settings/policies to find your policy IDs
            return new PolicyIds
            {
                ShippingPolicyId = "332163085023",    // Replace with actual ID
                PaymentPolicyId = "332163108023",      // Replace with actual ID  
                ReturnPolicyId = "332163226023"         // Replace with actual ID
            };
        }

        private string BuildCreateListingXml(ProductData product, PolicyIds policyIds)
        {
            string titleEsc = SecurityElement.Escape(product.Title ?? "");
            string descCData = product.Description ?? "";
            string priceStr = (product.Price <= 0m) ? "0.01" : product.Price.ToString("F2", CultureInfo.InvariantCulture);
            string qtyStr = (product.AvailableQuantity <= 0) ? "1" : product.AvailableQuantity.ToString();

            // Use a safe default category - you should determine this based on your product
            string categoryID = "260556"; // Bathroom Sinks & Vanities - change as needed

            // Build item specifics
            var specificsXml = new StringBuilder();

            // Add required Brand specific
            specificsXml.Append($@"
        <NameValueList>
          <Name>Brand</Name>
          <Value>{SecurityElement.Escape(product.BrandModel ?? "Unbranded")}</Value>
        </NameValueList>");

            // Add any additional item specifics
            if (product.ItemSpecifics != null && product.ItemSpecifics.Count > 0)
            {
                foreach (var kvp in product.ItemSpecifics)
                {
                    if (kvp.Key.Equals("Brand", StringComparison.OrdinalIgnoreCase))
                        continue; // Skip Brand as we already added it

                    string nameEsc = SecurityElement.Escape(kvp.Key);
                    string valEsc = SecurityElement.Escape(kvp.Value);
                    specificsXml.Append($@"
        <NameValueList>
          <Name>{nameEsc}</Name>
          <Value>{valEsc}</Value>
        </NameValueList>");
                }
            }

            // Build picture URLs if available
            string pictureXml = "";
            if (product.ImagePaths != null && product.ImagePaths.Count > 0)
            {
                pictureXml = "<PictureDetails>";
                foreach (var imagePath in product.ImagePaths.Take(12)) // eBay allows max 12 images
                {
                    // Check if it's already a URL or a local path
                    string imageUrl = imagePath;
                    if (!imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        // For local paths, you would need to upload them first
                        // For now, we'll skip local paths and only use URLs
                        continue;
                    }
                    pictureXml += $"<PictureURL>{SecurityElement.Escape(imageUrl)}</PictureURL>";
                }
                pictureXml += "</PictureDetails>";
            }

            // If no valid image URLs found, add placeholder
            if (string.IsNullOrEmpty(pictureXml) || !pictureXml.Contains("<PictureURL>"))
            {
                pictureXml = @"<PictureDetails>
      <PictureURL>https://via.placeholder.com/400x300.jpg?text=No+Image</PictureURL>
    </PictureDetails>";
            }

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<AddFixedPriceItemRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <Item>
    <Title>{titleEsc}</Title>
    <Description><![CDATA[{descCData}]]></Description>
    <PrimaryCategory>
      <CategoryID>{categoryID}</CategoryID>
    </PrimaryCategory>
    <StartPrice currencyID=""USD"">{priceStr}</StartPrice>
    <CategoryMappingAllowed>true</CategoryMappingAllowed>
    <ConditionID>1000</ConditionID>
    <Country>US</Country>
    <Location>San Jose, CA</Location>
    <Currency>USD</Currency>
    <DispatchTimeMax>3</DispatchTimeMax>
    <ListingDuration>GTC</ListingDuration>
    <ListingType>FixedPriceItem</ListingType>
    {pictureXml}
    <PostalCode>95125</PostalCode>
    <Quantity>{qtyStr}</Quantity>
    <Site>US</Site>
    <ItemSpecifics>{specificsXml}</ItemSpecifics>
    <BusinessSellerDetails>
      <SellerShippingProfileID>{policyIds.ShippingPolicyId}</SellerShippingProfileID>
      <SellerPaymentProfileID>{policyIds.PaymentPolicyId}</SellerPaymentProfileID>
      <SellerReturnProfileID>{policyIds.ReturnPolicyId}</SellerReturnProfileID>
    </BusinessSellerDetails>
  </Item>
</AddFixedPriceItemRequest>";
        }

        private MarketplaceListingResult ParseCreateListingResponse(string responseXml)
        {
            try
            {
                var doc = XDocument.Parse(responseXml);
                XNamespace ns = "urn:ebay:apis:eBLBaseComponents";

                var ack = doc.Root.Element(ns + "Ack")?.Value;
                if (ack == "Success" || ack == "Warning")
                {
                    string newItemId = doc.Root.Element(ns + "ItemID")?.Value;
                    return new MarketplaceListingResult
                    {
                        Success = true,
                        ListingId = newItemId,
                        ErrorMessage = null
                    };
                }
                else
                {
                    var errNode = doc.Descendants(ns + "LongMessage").FirstOrDefault();
                    string error = errNode?.Value ?? "Unknown eBay error.";
                    return new MarketplaceListingResult
                    {
                        Success = false,
                        ListingId = null,
                        ErrorMessage = error
                    };
                }
            }
            catch (Exception ex)
            {
                return new MarketplaceListingResult
                {
                    Success = false,
                    ListingId = null,
                    ErrorMessage = $"Failed to parse response: {ex.Message}"
                };
            }
        }

        private class PolicyIds
        {
            public string ShippingPolicyId { get; set; }
            public string PaymentPolicyId { get; set; }
            public string ReturnPolicyId { get; set; }
        }

        #endregion

        #region — Image Upload (Working) —

        public async Task<string> UploadImageAsync(byte[] imageData, string fileName)
        {
            await EnsureTokenIsValidAsync();

            try
            {
                var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<UploadSiteHostedPicturesRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <PictureData>
    <Data>{Convert.ToBase64String(imageData)}</Data>
    <DataFormat>JPG</DataFormat>
    <PictureName>{SecurityElement.Escape(fileName)}</PictureName>
  </PictureData>
</UploadSiteHostedPicturesRequest>";

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
                request.Headers.Add("X-EBAY-API-CALL-NAME", "UploadSiteHostedPictures");
                request.Headers.Add("X-EBAY-API-SITEID", "0");
                request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
                request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

                var response = await _httpClient.SendAsync(request);
                var xml = await response.Content.ReadAsStringAsync();

                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace;

                var ack = doc.Root?.Element(ns + "Ack")?.Value;
                if (ack == "Success" || ack == "Warning")
                {
                    return doc.Root?.Element(ns + "SiteHostedPictureDetails")?
                                    .Element(ns + "FullURL")?.Value;
                }
                else
                {
                    var error = doc.Descendants(ns + "LongMessage").FirstOrDefault()?.Value;
                    throw new Exception($"Image upload failed: {error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading image: {ex.Message}");
                throw;
            }
        }

        public async Task<MarketplaceListingResult> CreateListingWithImageAsync(ProductData product, byte[] imageData, string fileName)
        {
            try
            {
                var imageUrl = await UploadImageAsync(imageData, fileName);

                // Add the uploaded image URL to the product's ImagePaths
                if (product.ImagePaths == null)
                    product.ImagePaths = new List<string>();

                product.ImagePaths.Insert(0, imageUrl); // Add as first image

                // Call the ProductData overload (fixed the missing parameters)
                return await CreateListingAsync(product);
            }
            catch (Exception ex)
            {
                return new MarketplaceListingResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create listing with image: {ex.Message}"
                };
            }
        }


        /// <summary>
        /// Uploads multiple local image files and creates a listing
        /// </summary>
        public async Task<MarketplaceListingResult> CreateListingWithLocalImagesAsync(ProductData product, string[] localImagePaths)
        {
            try
            {
                var uploadedUrls = new List<string>();

                // Upload each local image
                foreach (var imagePath in localImagePaths.Take(12)) // eBay max 12 images
                {
                    if (File.Exists(imagePath))
                    {
                        var imageData = await File.ReadAllBytesAsync(imagePath);
                        var fileName = Path.GetFileName(imagePath);

                        try
                        {
                            var imageUrl = await UploadImageAsync(imageData, fileName);
                            uploadedUrls.Add(imageUrl);
                            Debug.WriteLine($"Uploaded image: {fileName} -> {imageUrl}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to upload image {fileName}: {ex.Message}");
                            // Continue with other images
                        }
                    }
                }

                if (uploadedUrls.Count == 0)
                {
                    return new MarketplaceListingResult
                    {
                        Success = false,
                        ErrorMessage = "No images were successfully uploaded. At least one image is required."
                    };
                }

                // Replace ImagePaths with uploaded URLs
                product.ImagePaths = uploadedUrls;

                // Call the ProductData overload (fixed the missing parameters)
                return await CreateListingAsync(product);
            }
            catch (Exception ex)
            {
                return new MarketplaceListingResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create listing with local images: {ex.Message}"
                };
            }
        }

        #endregion

        #region — IEbayService Implementation (Working) —

        public async Task<List<EbayAccount>> GetAccountsAsync()
        {
            var accounts = new List<EbayAccount>();

            if (!string.IsNullOrEmpty(_tokenStore.AccessToken))
            {
                accounts.Add(new EbayAccount
                {
                    Id = "default",
                    Username = _tokenStore.Username ?? "eBay User",
                    Email = _tokenStore.Email ?? "user@example.com",
                    IsActive = true,
                    LastUsed = DateTime.Now,
                    StoreName = "eBay Store"
                });
            }

            return accounts;
        }

        public Task<bool> AuthenticateAccountAsync(string accountId)
        {
            return Task.FromResult(!_tokenStore.IsExpired());
        }

        public async Task<List<CategorySpecific>> GetCategorySpecificsAsync(string accountId, string categoryId)
        {
            await EnsureTokenIsValidAsync();

            var specifics = new List<CategorySpecific>();

            var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetCategorySpecificsRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <CategoryID>{categoryId}</CategoryID>
</GetCategorySpecificsRequest>";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetCategorySpecifics");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace;

                foreach (var nameElem in doc.Descendants(ns + "NameRecommendation"))
                {
                    var specific = new CategorySpecific
                    {
                        Name = nameElem.Element(ns + "Name")?.Value,
                        Required = nameElem.Element(ns + "ValidationRules")?.Element(ns + "MinValues")?.Value == "1",
                        SelectionMode = nameElem.Element(ns + "ValidationRules")?.Element(ns + "SelectionMode")?.Value ?? "FreeText",
                        ValueRecommendations = new List<string>(),
                        MaxValues = int.TryParse(nameElem.Element(ns + "ValidationRules")?.Element(ns + "MaxValues")?.Value, out var max) ? max : 1
                    };

                    foreach (var valueElem in nameElem.Descendants(ns + "Value"))
                    {
                        specific.ValueRecommendations.Add(valueElem.Value);
                    }

                    specifics.Add(specific);
                }
            }

            return specifics;
        }

        public async Task<List<ConditionType>> GetCategoryConditionsAsync(string accountId, string categoryId)
        {
            await EnsureTokenIsValidAsync();

            var conditions = new List<ConditionType>();

            var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetCategoryFeaturesRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <CategoryID>{categoryId}</CategoryID>
  <DetailLevel>ReturnAll</DetailLevel>
  <FeatureID>ConditionValues</FeatureID>
</GetCategoryFeaturesRequest>";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetCategoryFeatures");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace;

                foreach (var conditionElem in doc.Descendants(ns + "Condition"))
                {
                    conditions.Add(new ConditionType
                    {
                        ConditionId = conditionElem.Element(ns + "ID")?.Value,
                        ConditionDisplayName = conditionElem.Element(ns + "DisplayName")?.Value
                    });
                }
            }

            // Default conditions if none found
            if (conditions.Count == 0)
            {
                conditions.Add(new ConditionType { ConditionId = "1000", ConditionDisplayName = "New" });
                conditions.Add(new ConditionType { ConditionId = "3000", ConditionDisplayName = "Used" });
                conditions.Add(new ConditionType { ConditionId = "1500", ConditionDisplayName = "New other" });
                conditions.Add(new ConditionType { ConditionId = "2500", ConditionDisplayName = "Refurbished" });
                conditions.Add(new ConditionType { ConditionId = "7000", ConditionDisplayName = "For parts or not working" });
            }

            return conditions;
        }

        

        public async Task<SuggestedCategory> SuggestCategoryAsync(string accountId, string title)
        {
            await EnsureTokenIsValidAsync();

            var xmlRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<GetSuggestedCategoriesRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <RequesterCredentials>
    <eBayAuthToken>{_tokenStore.AccessToken}</eBayAuthToken>
  </RequesterCredentials>
  <Query>{SecurityElement.Escape(title)}</Query>
</GetSuggestedCategoriesRequest>";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/ws/api.dll");
            request.Headers.Add("X-EBAY-API-CALL-NAME", "GetSuggestedCategories");
            request.Headers.Add("X-EBAY-API-SITEID", "0");
            request.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            request.Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var xml = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace;

                var categoryElem = doc.Descendants(ns + "SuggestedCategory").FirstOrDefault();
                if (categoryElem != null)
                {
                    return new SuggestedCategory
                    {
                        CategoryId = categoryElem.Element(ns + "Category")?.Element(ns + "CategoryID")?.Value,
                        CategoryName = categoryElem.Element(ns + "Category")?.Element(ns + "CategoryName")?.Value,
                        CategoryPath = categoryElem.Element(ns + "Category")?.Element(ns + "CategoryName")?.Value,
                        PercentFound = double.TryParse(categoryElem.Element(ns + "PercentItemFound")?.Value, out var pct) ? pct : 0
                    };
                }
            }

            return new SuggestedCategory
            {
                CategoryId = "260556", // Bathroom Sinks & Vanities as default
                CategoryName = "Bathroom Sinks & Vanities",
                CategoryPath = "Home & Garden > Bath > Bathroom Sinks & Vanities",
                PercentFound = 0
            };
        }

        #endregion

        #region — Stubs for Required Interface Methods —

        public Task<ProductData> GetListingDetailsAsync(string listingId)
        {
            throw new NotImplementedException();
        }

        public Task<MarketplaceListingResult> UpdateListingAsync(string listingId, ListingWizardData updateData)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteListingAsync(string listingId)
        {
            throw new NotImplementedException();
        }

        public Task<MarketplaceListingResult> PublishDraftListingAsync(string draftId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> AuthenticateAsync(string apiKey, string secretKey = null)
        {
            throw new NotImplementedException("OAuth is handled via IEbayTokenStore.");
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(IsAuthenticated);
        }

        public bool ValidateProductData(ProductData product, out List<string> missingFields)
        {
            missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(product.Title)) missingFields.Add("Title");
            if (string.IsNullOrWhiteSpace(product.ModelNumber)) missingFields.Add("Model Number");
            if (product.Price <= 0) missingFields.Add("Price");
            if (string.IsNullOrWhiteSpace(product.Condition)) missingFields.Add("Condition");
            return missingFields.Count == 0;
        }

        public Task<ShippingLabelResult> CreateShippingLabelAsync(ShippingLabelRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<List<EbayCategory>> GetStoreCategoriesAsync(string accountId)
        {
            return Task.FromResult(new List<EbayCategory>());
        }

        public async Task<bool> ValidateSkuAsync(string accountId, string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return false;
            // For now, just return true - you can implement SKU validation if needed
            return true;
        }

        public async Task<List<ShippingService>> GetShippingServicesAsync(string accountId, bool international = false)
        {
            var services = new List<ShippingService>();

            if (!international)
            {
                services.Add(new ShippingService { ShippingServiceCode = "USPSPriority", ShippingServiceName = "USPS Priority Mail" });
                services.Add(new ShippingService { ShippingServiceCode = "USPSFirstClass", ShippingServiceName = "USPS First Class" });
                services.Add(new ShippingService { ShippingServiceCode = "UPSGround", ShippingServiceName = "UPS Ground" });
                services.Add(new ShippingService { ShippingServiceCode = "FedExHomeDelivery", ShippingServiceName = "FedEx Home Delivery" });
            }
            else
            {
                services.Add(new ShippingService { ShippingServiceCode = "USPSPriorityMailInternational", ShippingServiceName = "USPS Priority Mail International" });
                services.Add(new ShippingService { ShippingServiceCode = "UPSWorldwideExpedited", ShippingServiceName = "UPS Worldwide Expedited" });
            }

            return services;
        }

        #endregion
    }
}