using Microsoft.Extensions.Caching.Distributed;
using AICustomerSupport.Application.Interfaces; 

namespace AICustomerSupport.Infrastructure.Services
{
    public class RedisTokenBlacklistService : ITokenBlacklistService
    {
        private readonly IDistributedCache _cache;

        public RedisTokenBlacklistService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task BlacklistTokenAsync(string jti, TimeSpan timeToLive)
        {
            string cacheKey = $"blacklist:{jti}";
            await _cache.SetStringAsync(cacheKey, "revoked", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = timeToLive
            });
        }

        public async Task<bool> IsTokenBlacklistedAsync(string jti)
        {
            string cacheKey = $"blacklist:{jti}";
            var val = await _cache.GetStringAsync(cacheKey);
            return !string.IsNullOrEmpty(val);
        }
    }
}