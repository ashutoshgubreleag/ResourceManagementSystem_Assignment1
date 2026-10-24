using Microsoft.Extensions.Configuration;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IBaseEntityRepository<StaffEntity> _staffRepository;
    private readonly IConfiguration _configuration;

    public AuthenticationService(
        IBaseEntityRepository<StaffEntity> staffRepository,
        IConfiguration configuration)
    {
        _staffRepository = staffRepository;
        _configuration = configuration;
    }

    public StaffEntity? AuthenticateStaff(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var configuredPassword = _configuration["Authentication:DemoPassword"] ?? "ApexPost123!";

        return _staffRepository.GetAll().FirstOrDefault(staff =>
            staff.IsActive
            && string.Equals(staff.Email, email, StringComparison.OrdinalIgnoreCase)
            && string.Equals(password, configuredPassword, StringComparison.Ordinal));
    }
}
