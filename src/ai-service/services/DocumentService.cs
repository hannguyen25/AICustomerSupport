public async Task ReprocessDocumentAsync(Guid documentId, IFormFile newFile, Guid userId)
{
    // 1. Kiểm tra Document tồn tại
    var document = await _context.Documents.FindAsync(documentId);
    if (document == null) throw new KeyNotFoundException("Document not found");

    // 2. Xác định version tiếp theo
    var latestVersionNumber = await _context.DocumentVersions
        .Where(v => v.DocumentId == documentId)
        .MaxAsync(v => (int?)v.VersionNumber) ?? 0;
    
    int newVersionNumber = latestVersionNumber + 1;
    Guid newVersionId = Guid.NewGuid();

    // 3. Gọi sang Python AI Service TRƯỚC (Sử dụng HttpClient trực tiếp)
    using var client = new HttpClient();
    
    using var content = new MultipartFormDataContent();
    content.Add(new StringContent(documentId.ToString()), "document_id");
    content.Add(new StringContent(newVersionId.ToString()), "version_id");
    
    // Đảm bảo Stream được dispose sau khi gửi xong
    using var fileStream = newFile.OpenReadStream();
    content.Add(new StreamContent(fileStream), "file", newFile.FileName);

    // Gọi API Reprocess bên phía Python (xóa vector cũ và index vector mới)
    var response = await client.PostAsync("http://ai-service:8001/api/ai/reprocess-document", content);
    
    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"AI Service failed to reprocess vectors: {errorContent}");
    }

    // 4. Nếu AI xử lý thành công, tiến hành lưu file vật lý và cập nhật Database SQL Server
    var filePath = await _fileStorageService.SaveFileAsync(newFile);

    var newVersion = new DocumentVersion
    {
        VersionId = newVersionId,
        DocumentId = documentId,
        VersionNumber = newVersionNumber,
        FilePath = filePath,
        FileSize = newFile.Length,
        UploadedAt = DateTime.UtcNow,
        UploadedBy = userId
    };
    
    _context.DocumentVersions.Add(newVersion);
    
    document.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
}