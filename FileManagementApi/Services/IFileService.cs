namespace FileManagementApi.Services;

public interface IFileService
{
    Task<string> SaveFileAsync(string directory, string fileName, byte[] data);
    Task<(byte[] Data, string ContentType)> GetFileAsync(string directory, string fileName);
    Task DeleteFileAsync(string directory, string fileName);
}
