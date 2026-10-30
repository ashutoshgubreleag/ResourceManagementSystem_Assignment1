using ApexPost.Database.Models;

namespace ApexPost.Services.Authentication;

public interface IAuthenticationService
{
    /// <summary>Returns the active staff member for valid credentials, otherwise null.</summary>
    Task<StaffEntity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}
