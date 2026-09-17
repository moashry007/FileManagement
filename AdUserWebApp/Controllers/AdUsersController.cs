using AdUserWebApp.Models;
using AdUserWebApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdUserWebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdUsersController : ControllerBase
{
    private readonly IAdUserDirectoryService _directoryService;
    private readonly ILogger<AdUsersController> _logger;

    public AdUsersController(IAdUserDirectoryService directoryService, ILogger<AdUsersController> logger)
    {
        _directoryService = directoryService;
        _logger = logger;
    }

    /// <summary>
    /// Verifies connectivity to the configured Active Directory domain, without reading any user data.
    /// </summary>
    [HttpGet("probe")]
    [ProducesResponseType(typeof(AdConnectivityStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdConnectivityStatus), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Probe()
    {
        var status = _directoryService.Probe();
        return status.Connected ? Ok(status) : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
    }

    /// <summary>
    /// Lists Active Directory user accounts. Disabled accounts are excluded unless
    /// <paramref name="includeDisabled"/> is set.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public IActionResult GetUsers(
        [FromQuery] bool includeDisabled = false,
        [FromQuery] string? q = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 1000);

        try
        {
            IEnumerable<AdUserRecord> users = _directoryService.GetAllUsers(includeDisabled);

            if (!string.IsNullOrWhiteSpace(q))
            {
                users = users.Where(u =>
                    u.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    u.SamAccountName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    u.Department.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = users.ToList();
            var page = materialized.Skip(skip).Take(take).ToList();

            return Ok(new
            {
                total = materialized.Count,
                skip,
                take,
                users = page,
            });
        }
        catch (Exception ex)
        {
            // Log only the failure, never the result set - the data is PII (see ad-full-user-export-spec.md §7).
            _logger.LogError(ex, "Failed to read users from Active Directory.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Failed to read users from Active Directory. See server logs for details." });
        }
    }
}
