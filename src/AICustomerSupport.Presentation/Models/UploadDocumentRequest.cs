using Microsoft.AspNetCore.Http;

namespace AICustomerSupport.Presentation.Models;

public class UploadDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IFormFile File { get; set; } = null!;
}