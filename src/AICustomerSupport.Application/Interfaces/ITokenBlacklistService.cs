namespace AICustomerSupport.Application.Interfaces
{
    public interface ITokenBlacklistService
    {
        Task BlacklistTokenAsync(string jti, TimeSpan timeToLive);
        Task<bool> IsTokenBlacklistedAsync(string jti);
    }
}