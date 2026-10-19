using AICustomerSupport.Application.Services;
using AICustomerSupport.Presentation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AICustomerSupport.Presentation.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto request)
    {
        try
        {
            // Lấy UserId từ JWT Claim (nếu có hệ thống Auth) hoặc gán mặc định cho test MVP
            Guid userId = Guid.Empty;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim != null && Guid.TryParse(userIdClaim, out var parsedId))
            {
                userId = parsedId;
            }

            var result = await _chatService.ProcessChatMessageAsync(userId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { message = "Lỗi kết nối tới AI Service.", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống.", detail = ex.Message });
        }
    }
}