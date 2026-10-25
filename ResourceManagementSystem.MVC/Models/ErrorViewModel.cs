namespace ResourceManagementSystem.MVC.Models;

/// <summary>Shown on the error and status-code pages. Only identifiers, never exception details.</summary>
public sealed class ErrorViewModel
{
    public int StatusCode { get; init; } = 500;
    public string Title { get; init; } = "Something went wrong";
    public string Message { get; init; } = "An unexpected error occurred.";
    public string? CorrelationId { get; init; }
    public string? TraceId { get; init; }
}
