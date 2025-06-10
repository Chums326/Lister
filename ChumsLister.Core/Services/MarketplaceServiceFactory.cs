using ChumsLister.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChumsLister.Core.Services
{
    public class MarketplaceServiceFactory : IMarketplaceServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public MarketplaceServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public List<string> GetAvailableMarketplaces()
        {
            var services = _serviceProvider.GetServices<IMarketplaceService>();
            return services.Select(s => s.PlatformName).ToList();
        }

        public IMarketplaceService GetMarketplaceService(string marketplaceName)
        {
            var services = _serviceProvider.GetServices<IMarketplaceService>();
            return services.FirstOrDefault(s =>
                s.PlatformName.Equals(marketplaceName, StringComparison.OrdinalIgnoreCase));
        }

        public IMarketplaceService CreateService(string platformName)
        {
            switch (platformName?.ToLower())
            {
                case "ebay":
                    return _serviceProvider.GetRequiredService<IMarketplaceService>();
                default:
                    throw new NotSupportedException($"Marketplace '{platformName}' is not supported.");
            }
        }


        public IEnumerable<IMarketplaceService> GetAllMarketplaceServices()
        {
            return _serviceProvider.GetServices<IMarketplaceService>();
        }

        public async Task<IEnumerable<IMarketplaceService>> GetAuthenticatedMarketplaceServicesAsync()
        {
            var allServices = _serviceProvider.GetServices<IMarketplaceService>();
            var authenticatedServices = new List<IMarketplaceService>();
            foreach (var service in allServices)
            {
                if (await service.IsAuthenticatedAsync())
                {
                    authenticatedServices.Add(service);
                }
            }
            return authenticatedServices;
        }


    }
}
