namespace AdUserWebApp.Models;

/// <summary>
/// PATCH-style update for an existing AD user's profile attributes. A null field is left
/// unchanged; an empty string clears that attribute. Never carries account-state or
/// credential changes - see <see cref="AdPasswordResetRequest"/> and the enable/disable
/// endpoints for those.
/// </summary>
public sealed class AdUserUpdateRequest
{
    public string? FirstName { get; set; }
    public string? Surname { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public string? Mobile { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? EmployeeId { get; set; }
}
