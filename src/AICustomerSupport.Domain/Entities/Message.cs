using System;
using System.Collections.Generic;

namespace AICustomerSupport.Domain.Entities;

public class Message
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string SenderType { get; set; } = string.Empty; // "USER" hoặc "AI"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
    public ICollection<MessageSource> MessageSources { get; set; } = new List<MessageSource>();
}