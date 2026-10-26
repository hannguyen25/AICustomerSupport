using System.Net.Http.Json;
using AICustomerSupport.Application.DTOs;          
using AICustomerSupport.Application.Services;      
using AICustomerSupport.Domain.Entities;
using AICustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
namespace AICustomerSupport.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatService> _logger;

    public ChatService(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<ChatService> logger)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _logger = logger;
    }

    public async Task<ChatResponseDto> ProcessChatMessageAsync(Guid userId, ChatRequestDto request)
    {
        // 1. Gọi sang Python AI Service
        var payload = new { query = request.Message, top_k = request.TopK };
        var response = await _httpClient.PostAsJsonAsync("http://ai-service:8001/api/ai/chat", payload);

        if (!response.IsSuccessStatusCode)
        {
            var errorDetails = await response.Content.ReadAsStringAsync();
            _logger.LogError("Lỗi từ AI Service: {Details}", errorDetails);
            throw new HttpRequestException($"AI Service phản hồi lỗi ({response.StatusCode}): {errorDetails}");
        }

        var aiResult = await response.Content.ReadFromJsonAsync<ChatResponseDto>() 
                       ?? throw new InvalidOperationException("Không thể đọc phản hồi từ AI Service.");

        // 2. Sử dụng Database Transaction để đảm bảo tính toàn vẹn dữ liệu (Atomicity)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 3. Xử lý ConversationId (Tạo mới nếu chưa có hoặc rỗng)
            Guid conversationId = request.ConversationId ?? Guid.Empty;
            Conversation conversation;

            if (conversationId == Guid.Empty)
            {
                conversationId = Guid.NewGuid();
                conversation = new Conversation
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    Title = request.Message.Length > 50 ? request.Message.Substring(0, 47) + "..." : request.Message,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = "ACTIVE"
                };
                _context.Conversations.Add(conversation);
            }
            else
            {
                conversation = await _context.Conversations.FindAsync(conversationId)
                               ?? throw new KeyNotFoundException($"Không tìm thấy cuộc hội thoại với ID: {conversationId}");
                conversation.UpdatedAt = DateTime.UtcNow;
            }

            // 4. Tạo Message cho User và AI
            var userMessageId = Guid.NewGuid();
            var aiMessageId = Guid.NewGuid();

            var userMessage = new Message
            {
                MessageId = userMessageId,
                ConversationId = conversationId,
                SenderType = "USER",
                Content = request.Message,
                CreatedAt = DateTime.UtcNow
            };

            var aiMessage = new Message
            {
                MessageId = aiMessageId,
                ConversationId = conversationId,
                SenderType = "AI",
                Content = aiResult.Answer,
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(userMessage);
            _context.Messages.Add(aiMessage);

            // 5. Thêm danh sách nguồn trích dẫn bằng AddRange tối ưu hiệu năng
            if (aiResult.Sources != null && aiResult.Sources.Count > 0)
            {
                var messageSources = aiResult.Sources.Select((src, index) => new MessageSource
                {
                    MessageSourceId = Guid.NewGuid(),
                    MessageId = aiMessageId,
                    ChunkId = Guid.Empty, // Hoặc ánh xạ ID chunk chuẩn nếu có từ Python trả về
                    SimilarityScore = src.Score,
                    Rank = index + 1
                }).ToList();

                _context.MessageSources.AddRange(messageSources);
            }

            // Lưu thay đổi vào SQL Server
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Gắn thêm ConversationId vào response trả về cho Client
            aiResult.ConversationId = conversationId;
            return aiResult;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi lưu lịch sử hội thoại vào Database.");
            throw;
        }
    }
}