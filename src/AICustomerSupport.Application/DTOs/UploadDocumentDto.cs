namespace AICustomerSupport.Application.DTOs;

public class UploadDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
}