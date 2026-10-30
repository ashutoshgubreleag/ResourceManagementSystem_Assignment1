using ApexPost.Database.Models;
using ApexPost.Repositories;
using ApexPost.Repositories.Specifications;

namespace ApexPost.Services.Authentication;

public sealed class AuthenticationService(IBaseEntityRepository<StaffEntity> staff, IPasswordHasher hasher) : IAuthenticationService
{
    public async Task<StaffEntity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var normalised = email.Trim().ToLowerInvariant();
        var matches = await staff.FindAsync(Specification<StaffEntity>.Where(s => s.Email == normalised), cancellationToken).ConfigureAwait(false);
        var candidate = matches.FirstOrDefault();

        // Always run the hash (dummy hash for unknown users) to avoid a timing side channel.
        var passwordOk = hasher.Verify(password, candidate?.PasswordHash ?? string.Empty);

        // Inactive staff cannot sign in at all.
        return passwordOk && candidate is { IsActive: true } ? candidate : null;
    }
}
