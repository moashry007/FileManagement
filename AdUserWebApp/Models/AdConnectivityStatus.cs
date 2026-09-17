namespace AdUserWebApp.Models;

public sealed class AdConnectivityStatus
{
    public bool Connected { get; init; }
    public string? ConnectedServer { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public string? Error { get; init; }
}
