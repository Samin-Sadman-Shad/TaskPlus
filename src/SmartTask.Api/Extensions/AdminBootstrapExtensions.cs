using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Api.Extensions;

/// <summary>
/// Ensures the deployment's first admin exists, from the <c>Bootstrap:Admin</c> settings
/// (<c>FirstName</c>, <c>LastName</c>, <c>Email</c>, <c>Password</c>). Supply them as environment
/// variables (<c>Bootstrap__Admin__Email</c>, …; the Docker <c>.env</c>) or user-secrets, never in
/// <c>appsettings*.json</c>. Without <c>Email</c> and <c>Password</c> this does nothing.
/// </summary>
/// <remarks>
/// The rules (create only when no active admin exists, never overwrite, never promote) live in
/// <see cref="IAdminBootstrapService"/>. Invalid settings stop the host: a deployment that was told to
/// have an admin should not start without one silently.
/// </remarks>
public static class AdminBootstrapExtensions
{
    public const string SectionName = "Bootstrap:Admin";

    public static async Task BootstrapAdminAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var section = app.Configuration.GetSection(SectionName);
        var email = section["Email"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var admin = new CreateUserDto
        {
            FirstName = section["FirstName"] ?? string.Empty,
            LastName = section["LastName"] ?? string.Empty,
            Email = email,
            Password = password,
        };

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>()
            .EnsureAdminAsync(admin, app.Lifetime.ApplicationStopping);
    }
}
