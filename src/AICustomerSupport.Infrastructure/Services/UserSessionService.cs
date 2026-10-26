using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using AICustomerSupport.Application.Interfaces;
using AICustomerSupport.Application.DTOs; 

namespace AICustomerSupport.Infrastructure.Services
{
    public class UserSessionService : IUserSessionService
    {
        private readonly IDistributedCache _cache;

        public UserSessionService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task SaveUserSessionAsync(string userId, UserSessionDto sessionData)
        {
            string cacheKey = $"user_session:{userId}";
            string jsonData = JsonSerializer.Serialize(sessionData);

            // Lưu session có thời hạn tự động hủy sau 2 giờ không hoạt động
            await _cache.SetStringAsync(cacheKey, jsonData, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(2) 
            });
        }

        public async Task<UserSessionDto?> GetUserSessionAsync(string userId)
        {
            string cacheKey = $"user_session:{userId}";
            var jsonData = await _cache.GetStringAsync(cacheKey);

            return string.IsNullOrEmpty(jsonData) ? null : JsonSerializer.Deserialize<UserSessionDto>(jsonData);
        }
    }
}