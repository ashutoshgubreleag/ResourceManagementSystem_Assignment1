
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ResourceManagementSystem.Utilities;

public static class ExceptionHandlingExtensions
{
    public static IServiceCollection AddUtilitiesExtentions(
        this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
