using System.IO;
using AICustomerSupport.Application.Interfaces;
using Microsoft.Extensions.Hosting;

namespace AICustomerSupport.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IHostEnvironment _env;

    public LocalFileStorageService(IHostEnvironment env)
    {
        _env = env;
    }

    public async Task<(string filePath, long fileSize)> SaveFileAsync(Stream fileStream, string fileName, string folderName)
    {
        if (fileStream == null || fileStream.Length == 0)
            throw new ArgumentException("File stream is empty or invalid.");

        // Kiểm tra định dạng file cho phép
        var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported file format. Only PDF, DOCX, and TXT are allowed.");
        }

        var uploadsFolder = Path.Combine(_env.ContentRootPath, "Storage", folderName);
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(stream);
        }

        return (filePath, fileStream.Length);
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}