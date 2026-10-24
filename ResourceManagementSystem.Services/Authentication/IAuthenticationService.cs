using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Authentication;

public interface IAuthenticationService
{
    StaffEntity? AuthenticateStaff(string email, string password);
}
