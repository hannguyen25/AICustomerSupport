public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public int TopK { get; set; } = 3;
    public Guid? ConversationId { get; set; }
}