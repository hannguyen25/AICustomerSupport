public class ChatResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceDto> Sources { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public Guid ConversationId { get; set; }
}