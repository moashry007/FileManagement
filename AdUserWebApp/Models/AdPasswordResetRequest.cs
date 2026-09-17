namespace AdUserWebApp.Models;

public sealed class AdPasswordResetRequest
{
    public string NewPassword { get; set; } = "";
    public bool RequireChangeAtNextLogon { get; set; } = true;
}
