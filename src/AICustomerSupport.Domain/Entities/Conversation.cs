using System;
using System.Collections.Generic;

namespace AICustomerSupport.Domain.Entities;

public class Conversation
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "ACTIVE";

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}