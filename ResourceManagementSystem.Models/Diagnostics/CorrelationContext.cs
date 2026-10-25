namespace ResourceManagementSystem.Models.Diagnostics;

/// <summary>
/// Ambient correlation ID for the current logical operation. The correlation middleware sets it
/// once per request; <see cref="AsyncLocal{T}"/> flows it through every <c>await</c>, so services
/// can stamp notifications with it without taking a dependency on ASP.NET Core.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? CorrelationId => Current.Value;

    /// <summary>Sets the correlation ID until the returned scope is disposed.</summary>
    public static IDisposable Begin(string correlationId)
    {
        var previous = Current.Value;
        Current.Value = correlationId;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
