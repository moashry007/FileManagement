namespace AdUserWebApp.Models;

/// <summary>
/// A full directory read plus when it was taken, so callers can tell how stale a cached
/// result is and decide whether to force a fresh export.
/// </summary>
public sealed class AdUserSnapshot
{
    public IReadOnlyList<AdUserRecord> Users { get; init; } = Array.Empty<AdUserRecord>();
    public DateTimeOffset AsOf { get; init; }
}
