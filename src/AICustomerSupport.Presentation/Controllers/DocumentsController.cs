using System.Security.Claims;
using AICustomerSupport.Application.DTOs;
using AICustomerSupport.Application.Interfaces;
using AICustomerSupport.Domain.Entities;
using AICustomerSupport.Infrastructure.Persistence;
using AICustomerSupport.Presentation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AICustomerSupport.Presentation.Controllers;

[Route("api/documents")]
[ApiController]
[Authorize(Roles = "System Administrator, Knowledge Base Administrator")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public DocumentsController(
        AppDbContext context, 
        IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
    {
        Guid documentId = Guid.NewGuid();
        Guid versionId = Guid.NewGuid();

        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var uploadedBy))
            {
                return Unauthorized(new { message = "Invalid user session." });
            }

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            // Map presentation model to application DTO
            var dto = new UploadDocumentDto
            {
                Title = request.Title,
                Category = request.Category,
                Description = request.Description
            };

            // Save file via storage service using stream
            using var stream = request.File.OpenReadStream();
            var (filePath, fileSize) = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, "KnowledgeBase");

            // 1. Tạo bản ghi Document với trạng thái ban đầu là "Pending"
            var document = new Document
            {
                DocumentId = documentId,
                Title = dto.Title,
                FileName = request.File.FileName,
                Category = dto.Category,
                Description = dto.Description,
                Status = "Pending", 
                UploadedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var version = new DocumentVersion
            {
                VersionId = versionId,
                DocumentId = document.DocumentId,
                VersionNumber = 1,
                FilePath = filePath,
                FileSize = fileSize,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = uploadedBy
            };

            _context.Documents.Add(document);
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            // 2. Chuyển trạng thái sang "Processing" trước khi gọi AI Service xử lý
            document.Status = "Processing";
            await _context.SaveChangesAsync();

            // 3. Gọi sang Python AI Service (Cổng 8000) sử dụng new HttpClient() trực tiếp
            using var client = new HttpClient();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(documentId.ToString()), "document_id");
            content.Add(new StringContent(versionId.ToString()), "version_id");

            // Đọc lại file stream để gửi qua HTTP và tự động giải phóng (using)
            using var fileStream = request.File.OpenReadStream();
            content.Add(new StreamContent(fileStream), "file", request.File.FileName);

            var aiResponse = await client.PostAsync("http://ai-service:8001/api/ai/index-document", content);

            if (aiResponse.IsSuccessStatusCode)
            {
                // 4. Xử lý thành công -> Đổi trạng thái thành "Completed" (tuân thủ BR-04)
                document.Status = "Completed";
            }
            else
            {
                // 5. Xử lý thất bại từ AI Service -> Đổi trạng thái thành "Failed" (tuân thủ BR-05)
                document.Status = "Failed";
            }
            document.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Document uploaded and processing workflow executed.",
                documentId = document.DocumentId,
                versionId = version.VersionId,
                fileName = document.FileName,
                status = document.Status
            });
        }
        catch (Exception ex)
        {
            // Nếu có lỗi ngoại lệ bất ngờ xảy ra -> Cập nhật trạng thái thành "Failed"
            var existingDoc = await _context.Documents.FindAsync(documentId);
            if (existingDoc != null)
            {
                existingDoc.Status = "Failed";
                existingDoc.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return BadRequest(new { message = ex.Message, status = "Failed" });
        }
    }

    [HttpPost("sync-chunks")]
    [AllowAnonymous] // Cho phép gọi tự do nội bộ giữa Python và C#
    public async Task<IActionResult> SyncDocumentChunks([FromBody] SyncChunksRequest request)
    {
        try
        {
            // 1. Xóa các chunk cũ của version này (nếu có để tránh trùng lặp)
            var existingChunks = _context.DocumentChunks
                .Where(c => c.DocumentId == request.DocumentId && c.VersionId == request.VersionId);
            _context.DocumentChunks.RemoveRange(existingChunks);

            // 2. Thêm các chunk mới từ Python gửi sang
            foreach (var chunk in request.Chunks)
            {
                var documentChunk = new DocumentChunk
                {
                    ChunkId = Guid.NewGuid(),
                    DocumentId = request.DocumentId,
                    VersionId = request.VersionId,
                    ChunkIndex = chunk.ChunkIndex,
                    Content = chunk.Content,
                    PageNumber = chunk.PageNumber,
                    TokenCount = chunk.TokenCount,
                    SectionTitle = chunk.SectionTitle,
                    CreatedAt = DateTime.UtcNow
                };
                _context.DocumentChunks.Add(documentChunk);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Chunks synchronized successfully to SQL Server." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Request Model phụ trợ
    public class SyncChunksRequest
    {
        public Guid DocumentId { get; set; }
        public Guid VersionId { get; set; }
        public List<ChunkDto> Chunks { get; set; } = new();
    }

    public class ChunkDto
    {
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public int TokenCount { get; set; }
        public string? SectionTitle { get; set; }
    }

    [HttpPost("{id}/reprocess")]
    public async Task<IActionResult> ReprocessDocument(Guid id, [FromForm] UploadDocumentRequest request)
    {
        var existingDoc = await _context.Documents.FindAsync(id);
        if (existingDoc == null)
        {
            return NotFound(new { message = "Document not found." });
        }

        Guid versionId = Guid.NewGuid();

        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var uploadedBy))
            {
                return Unauthorized(new { message = "Invalid user session." });
            }

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded for reprocessing." });
            }

            // Lưu file mới qua storage service
            using var stream = request.File.OpenReadStream();
            var (filePath, fileSize) = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, "KnowledgeBase");

            // Lấy số thứ tự version tiếp theo
            var latestVersionNumber = _context.DocumentVersions
                .Where(v => v.DocumentId == id)
                .Max(v => (int?)v.VersionNumber) ?? 0;

            var version = new DocumentVersion
            {
                VersionId = versionId,
                DocumentId = id,
                VersionNumber = latestVersionNumber + 1,
                FilePath = filePath,
                FileSize = fileSize,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = uploadedBy
            };

            _context.DocumentVersions.Add(version);
            
            // Cập nhật lại thông tin cơ bản của document nếu có thay đổi và chuyển sang Processing
            existingDoc.Status = "Processing";
            existingDoc.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Title)) existingDoc.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Category)) existingDoc.Category = request.Category;
            if (!string.IsNullOrEmpty(request.Description)) existingDoc.Description = request.Description;

            await _context.SaveChangesAsync();

            // Gọi sang Python AI Service để re-index và đồng bộ chunks mới
            using var client = new HttpClient();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(id.ToString()), "document_id");
            content.Add(new StringContent(versionId.ToString()), "version_id");

            using var fileStream = request.File.OpenReadStream();
            content.Add(new StreamContent(fileStream), "file", request.File.FileName);

            var aiResponse = await client.PostAsync("http://ai-service:8001/api/ai/index-document", content);

            if (aiResponse.IsSuccessStatusCode)
            {
                existingDoc.Status = "Completed";
            }
            else
            {
                existingDoc.Status = "Failed";
            }
            existingDoc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Document reprocessed successfully.",
                documentId = existingDoc.DocumentId,
                versionId = version.VersionId,
                status = existingDoc.Status
            });
        }
        catch (Exception ex)
        {
            existingDoc.Status = "Failed";
            existingDoc.UpdatedAt = DateTime.UtcNow;
            existingDoc.FileName = request.File.FileName;
            await _context.SaveChangesAsync();

            return BadRequest(new { message = ex.Message, status = "Failed" });
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchKnowledge([FromBody] SearchRequestModel request)
    {
        try
        {
            using var client = new HttpClient();
            
            // Đóng gói request gửi sang Python AI Service (FastAPI)
            var response = await client.PostAsJsonAsync("http://ai-service:8001/api/ai/search", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                // Sửa từ response.StatusMessage thành response.StatusCode
                return StatusCode((int)response.StatusCode, new { message = "AI Search failed", details = errorContent });
            }

            var jsonResult = await response.Content.ReadAsStringAsync();
            return Content(jsonResult, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    // Model phụ trợ cho phần Search (Khởi tạo giá trị mặc định để tránh warning)
    public class SearchRequestModel
    {
        public string Query { get; set; } = string.Empty;
        public int TopK { get; set; } = 3;
    }
}