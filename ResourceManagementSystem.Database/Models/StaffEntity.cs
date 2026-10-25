using ResourceManagementSystem.Models.ValueObjects;

namespace ResourceManagementSystem.Database.Models;

public class StaffEntity : IEntity
{
    public int Id { get; set; }

    public string Name { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    // Emails are compared case-insensitively everywhere, so normalise once on write.
    public string Email { get; set => field = value?.Trim().ToLowerInvariant() ?? string.Empty; } = string.Empty;

    public StaffRole Role { get; set; }

    public BranchCode HomeBranchCode { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>PBKDF2 hash ("PBKDF2$iterations$salt$hash"); never the password itself.</summary>
    public string PasswordHash { get; set; } = string.Empty;
}
