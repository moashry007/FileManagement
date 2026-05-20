using Microsoft.AspNetCore.StaticFiles;

namespace FileManagementApi.Services;

public class FileService : IFileService
{
    private readonly string _baseStoragePath;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public FileService(IConfiguration configuration)
    {
        _baseStoragePath = configuration["FileStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    }

    public async Task<string> SaveFileAsync(string directory, string fileName, byte[] data)
    {
        ValidatePath(directory);
        ValidateFileName(fileName);

        var targetDir = Path.GetFullPath(Path.Combine(_baseStoragePath, directory));
        EnsureWithinBase(targetDir);

        Directory.CreateDirectory(targetDir);

        var filePath = Path.Combine(targetDir, fileName);
        await File.WriteAllBytesAsync(filePath, data);

        return Path.Combine(directory, fileName);
    }

    public async Task<(byte[] Data, string ContentType)> GetFileAsync(string directory, string fileName)
    {
        ValidatePath(directory);
        ValidateFileName(fileName);

        var filePath = ResolveSafePath(directory, fileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File '{fileName}' not found in '{directory}'.");

        var data = await File.ReadAllBytesAsync(filePath);
        var contentType = GetContentType(fileName);

        return (data, contentType);
    }

    public Task DeleteFileAsync(string directory, string fileName)
    {
        ValidatePath(directory);
        ValidateFileName(fileName);

        var filePath = ResolveSafePath(directory, fileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File '{fileName}' not found in '{directory}'.");

        File.Delete(filePath);
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string directory, string fileName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_baseStoragePath, directory, fileName));
        EnsureWithinBase(fullPath);
        return fullPath;
    }

    private void EnsureWithinBase(string fullPath)
    {
        var baseFull = Path.GetFullPath(_baseStoragePath);
        if (!fullPath.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !fullPath.Equals(baseFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access outside the base storage path is not allowed.");
        }
    }

    private static void ValidatePath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory must not be empty.", nameof(directory));

        if (directory.Contains(".."))
            throw new ArgumentException("Directory must not contain '..'.", nameof(directory));
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name must not be empty.", nameof(fileName));

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("File name contains invalid characters.", nameof(fileName));
    }

    private string GetContentType(string fileName)
    {
        if (_contentTypeProvider.TryGetContentType(fileName, out var contentType))
            return contentType;
        return "application/octet-stream";
    }
}
