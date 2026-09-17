namespace AdUserWebApp.Models;

public sealed class AdUserRecord
{
    public string SamAccountName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string Surname { get; init; } = "";
    public string Email { get; init; } = "";
    public string Telephone { get; init; } = "";
    public string Mobile { get; init; } = "";
    public string EmployeeId { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string Department { get; init; } = "";
    public string DistinguishedName { get; init; } = "";
    public bool IsDisabled { get; init; }
}
