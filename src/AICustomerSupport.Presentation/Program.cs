using System.Text;
using AICustomerSupport.Application.Interfaces;
using AICustomerSupport.Application.Services; 
using AICustomerSupport.Domain.Entities;
using AICustomerSupport.Infrastructure.Persistence;
using AICustomerSupport.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5145");

// 1. Register Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Register Application & Infrastructure Services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IChatService, ChatService>(); 
builder.Services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();

// 3. Configure Redis Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "AICustomerSupport_Session:";
});

// 4. Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret missing");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };

    // === Thêm phần sự kiện này để kiểm tra Token Blacklist trên Redis ===
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            // Lấy service kiểm tra blacklist từ DI container của HttpContext
            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
            
            // Lấy claim 'jti' (JWT ID) từ token
            var jti = context.Principal?.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

            if (!string.IsNullOrEmpty(jti) && await blacklistService.IsTokenBlacklistedAsync(jti))
            {
                // Từ chối request nếu token đã bị thu hồi (đăng xuất)
                context.Fail("Token này đã bị thu hồi.");
            }
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

// ==========================================
// AUTOMATIC ACCOUNT SEEDING (Admin & Agent)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Tự động tạo database và chạy toàn bộ migration khi container khởi động
    await context.Database.MigrateAsync();

    var passwordHasher = new PasswordHasher<object>();

    // 1. Seed tài khoản Admin
    var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "System Administrator");
    if (adminRole != null)
    {
        var adminEmail = "admin@company.com";
        if (!await context.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var hashedPassword = passwordHasher.HashPassword(new object(), "Admin@123456");
            var adminUser = new User
            {
                UserId = Guid.NewGuid(),
                FullName = "System Administrator",
                Email = adminEmail,
                PasswordHash = hashedPassword,
                RoleId = adminRole.RoleId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
            Console.WriteLine(">>> Đã tự động tạo tài khoản Admin: admin@company.com / Admin@123456");
        }
    }
    else
    {
        Console.WriteLine(">>> Cảnh báo: Không tìm thấy role 'System Administrator' trong database!");
    }

    // 2. Seed tài khoản Customer Support Agent
    var agentRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer Support Agent");
    if (agentRole != null)
    {
        var agentEmail = "agent@company.com";
        if (!await context.Users.AnyAsync(u => u.Email == agentEmail))
        {
            var hashedPassword = passwordHasher.HashPassword(new object(), "Admin@123456");
            var agentUser = new User
            {
                UserId = Guid.NewGuid(),
                FullName = "Support Agent",
                Email = agentEmail,
                PasswordHash = hashedPassword,
                RoleId = agentRole.RoleId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(agentUser);
            await context.SaveChangesAsync();
            Console.WriteLine(">>> Đã tự động tạo tài khoản Agent: agent@company.com / Admin@123456");
        }
    }
    else
    {
        Console.WriteLine(">>> Cảnh báo: Không tìm thấy role 'Customer Support Agent' trong database!");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
    
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();