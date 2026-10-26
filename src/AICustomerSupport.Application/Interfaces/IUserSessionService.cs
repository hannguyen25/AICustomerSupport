using AICustomerSupport.Application.DTOs;

namespace AICustomerSupport.Application.Interfaces
{
    public interface IUserSessionService
    {
        Task SaveUserSessionAsync(string userId, UserSessionDto sessionData);
        Task<UserSessionDto?> GetUserSessionAsync(string userId);
    }
}