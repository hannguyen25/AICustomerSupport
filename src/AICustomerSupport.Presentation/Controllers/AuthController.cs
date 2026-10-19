using AICustomerSupport.Application.Interfaces;
using AICustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization; 
using AICustomerSupport.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AICustomerSupport.Presentation.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly PasswordHasher<object> _passwordHasher = new();

    public AuthController(AppDbContext context, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // 1. Find user by email and include their Role
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || user.Status != "Active")
        {
            return Unauthorized(new { message = "Invalid email or inactive account." });
        }

        // 2. Verify password hash
        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid password." });
        }

        // 3. Update LastLoginAt audit timestamp
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // 4. Generate JWT Token
        var token = _jwtTokenService.GenerateToken(user, user.Role.RoleName);

        return Ok(new
        {
            token,
            email = user.Email,
            fullName = user.FullName,
            role = user.Role.RoleName
        });
    }

    [HttpPost("register")]
    [Authorize(Roles = "System Administrator")] // Chỉ Admin mới được tạo tài khoản nội bộ
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // 1. Kiểm tra xem email đã tồn tại chưa
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new { message = "Email already exists." });
        }

        // 2. Tìm Role dựa theo giá trị 'Role' truyền từ Postman lên (mặc định nếu để trống thì lấy 'Support Agent')
        var targetRoleName = string.IsNullOrEmpty(dto.Role) ? "Support Agent" : dto.Role;
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == targetRoleName);
        
        if (role == null)
        {
            return BadRequest(new { message = $"Role '{targetRoleName}' not found in database." });
        }

        // 3. Khởi tạo User mới
        var newUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = dto.FullName,
            Email = dto.Email,
            Status = "Active",
            RoleId = role.RoleId
        };
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

        // 4. Lưu vào cơ sở dữ liệu
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Account with role '{targetRoleName}' created successfully!" });
    }
}

public record LoginDto(string Email, string Password);
public record RegisterDto(string FullName, string Email, string Password, string Role);