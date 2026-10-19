using AICustomerSupport.Application.DTOs;

namespace AICustomerSupport.Application.Services;

public interface IChatService
{
    Task<ChatResponseDto> ProcessChatMessageAsync(Guid userId, ChatRequestDto request);
}