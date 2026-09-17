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
        [FromQuery] int take = 200,
        [FromQuery] bool forceRefresh = false)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 1000);

        try
        {
            var snapshot = _directoryService.GetUsers(includeDisabled, forceRefresh);
            IEnumerable<AdUserRecord> users = snapshot.Users;

            if (!string.IsNullOrWhiteSpace(q))
            {
                users = users.Where(u =>
                    u.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    u.SamAccountName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    u.Department.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = users.ToList();
            var page = filtered.Skip(skip).Take(take).ToList();

            return Ok(new
            {
                total = filtered.Count,
                skip,
                take,
                asOf = snapshot.AsOf,
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

    /// <summary>
    /// Updates an existing user's profile attributes (name, contact info, title, department,
    /// employee ID). Does not touch account state or credentials.
    /// </summary>
    [HttpPatch("{samAccountName}")]
    [ProducesResponseType(typeof(AdUserRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public IActionResult UpdateUser(string samAccountName, [FromBody] AdUserUpdateRequest request) =>
        ExecuteWrite(() => _directoryService.UpdateUser(samAccountName, request), "update user", samAccountName);

    /// <summary>
    /// Enables a previously disabled account.
    /// </summary>
    [HttpPost("{samAccountName}/enable")]
    [ProducesResponseType(typeof(AdUserRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public IActionResult EnableUser(string samAccountName) =>
        ExecuteWrite(() => _directoryService.SetAccountEnabled(samAccountName, enabled: true), "enable account", samAccountName);

    /// <summary>
    /// Disables an account. The account is not deleted and can be re-enabled.
    /// </summary>
    [HttpPost("{samAccountName}/disable")]
    [ProducesResponseType(typeof(AdUserRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public IActionResult DisableUser(string samAccountName) =>
        ExecuteWrite(() => _directoryService.SetAccountEnabled(samAccountName, enabled: false), "disable account", samAccountName);

    /// <summary>
    /// Resets an existing user's password. The password is never logged.
    /// </summary>
    [HttpPost("{samAccountName}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public IActionResult ResetPassword(string samAccountName, [FromBody] AdPasswordResetRequest request) =>
        ExecuteWrite<object?>(() =>
        {
            _directoryService.ResetPassword(samAccountName, request);
            return null;
        }, "reset password for", samAccountName);

    /// <summary>
    /// Creates a new AD user account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdUserRecord), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public IActionResult CreateUser([FromBody] AdUserCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SamAccountName) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.Surname))
            return BadRequest(new { error = "SamAccountName, FirstName, and Surname are required." });

        var result = ExecuteWrite(() => _directoryService.CreateUser(request), "create user", request.SamAccountName);
        if (result is OkObjectResult ok)
            return CreatedAtAction(nameof(GetUsers), null, ok.Value);

        return result;
    }

    private IActionResult ExecuteWrite<T>(Func<T> action, string operationDescription, string samAccountName)
    {
        try
        {
            var value = action();
            return value is null ? NoContent() : Ok(value);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Never log request bodies here - password resets and profile edits carry PII/secrets.
            _logger.LogError(ex, "Failed to {Operation} {SamAccountName} in Active Directory.", operationDescription, samAccountName);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = $"Failed to {operationDescription} in Active Directory. See server logs for details." });
        }
    }
}
