using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.Services.Auth;
using SmartTask.Application.Services.Authorization;
using SmartTask.Application.Services.Dashboard;
using SmartTask.Application.Services.Projects;
using SmartTask.Application.Services.Tasks;
using SmartTask.Application.Services.Users;

namespace SmartTask.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer: every FluentValidation validator in this assembly (the API's
    /// global <c>ValidationFilter</c> resolves them per request argument), the access guard and the
    /// feature services. Services are scoped because they depend on the scoped repositories and user context.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IProjectAccessGuard, ProjectAccessGuard>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
