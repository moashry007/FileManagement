namespace FileManagementApi.Models;

public class UploadFileRequest
{
    public string FileName { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
