namespace AdUserWebApp.Models;

public sealed class AdUserCreateRequest
{
    public string SamAccountName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string Surname { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public string? Mobile { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? EmployeeId { get; set; }

    /// <summary>
    /// If set, the account is created with this password. If left blank, the account is
    /// created disabled (AD refuses to create an enabled account with no usable password).
    /// </summary>
    public string? InitialPassword { get; set; }
    public bool RequirePasswordChangeAtNextLogon { get; set; } = true;
    public bool Enabled { get; set; } = true;
}
