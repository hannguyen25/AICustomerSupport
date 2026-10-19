using AICustomerSupport.Domain.Entities;

namespace AICustomerSupport.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user, string roleName);
}