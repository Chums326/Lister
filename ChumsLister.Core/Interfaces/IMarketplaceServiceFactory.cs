namespace ChumsLister.Core.Interfaces
{
    public interface IMarketplaceServiceFactory
    {
        List<string> GetAvailableMarketplaces();
        IMarketplaceService GetMarketplaceService(string marketplaceName);
        IEnumerable<IMarketplaceService> GetAllMarketplaceServices();
        Task<IEnumerable<IMarketplaceService>> GetAuthenticatedMarketplaceServicesAsync();
        IMarketplaceService CreateService(string platformName); // Added method



    }
}