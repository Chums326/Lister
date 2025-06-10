namespace ChumsLister.Core.Interfaces
{
    public interface IEbayTokenStore
    {
        string AccessToken { get; }
        string RefreshToken { get; }
        DateTime TokenExpiry { get; }
        bool IsExpired();
        Task RefreshTokenAsync();
        void SetCurrentUser(string userId);
        string Email { get; }
        string Username { get; }

    }
}