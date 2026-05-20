using FileManagementApi.Models;
using FileManagementApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly ILogger<FileController> _logger;

    public FileController(IFileService fileService, ILogger<FileController> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a file as binary data to a specified directory.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFile([FromBody] UploadFileRequest request)
    {
        if (request.Data == null || request.Data.Length == 0)
            return BadRequest(new { error = "File data must not be empty." });

        try
        {
            var savedPath = await _fileService.SaveFileAsync(request.Directory, request.FileName, request.Data);
            _logger.LogInformation("File saved: {Path}", savedPath);
            return CreatedAtAction(nameof(GetFile), new { directory = request.Directory, fileName = request.FileName },
                new { message = "File uploaded successfully.", path = savedPath });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Download a file from a specified directory.
    /// </summary>
    [HttpGet("{directory}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFile(string directory, string fileName)
    {
        try
        {
            var (data, contentType) = await _fileService.GetFileAsync(directory, fileName);
            _logger.LogInformation("File served: {Directory}/{FileName}", directory, fileName);
            return File(data, contentType, fileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a file from a specified directory.
    /// </summary>
    [HttpDelete("{directory}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteFile(string directory, string fileName)
    {
        try
        {
            await _fileService.DeleteFileAsync(directory, fileName);
            _logger.LogInformation("File deleted: {Directory}/{FileName}", directory, fileName);
            return NoContent();
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
