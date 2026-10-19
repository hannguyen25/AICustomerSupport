using System.IO;

namespace AICustomerSupport.Application.Interfaces;

public interface IFileStorageService
{
    Task<(string filePath, long fileSize)> SaveFileAsync(Stream fileStream, string fileName, string folderName);
    void DeleteFile(string filePath);
}