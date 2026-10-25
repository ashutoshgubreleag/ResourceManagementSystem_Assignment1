using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Authentication;

public interface IAuthenticationService
{
    /// <summary>Returns the active staff member for valid credentials, otherwise null.</summary>
    Task<StaffEntity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}
