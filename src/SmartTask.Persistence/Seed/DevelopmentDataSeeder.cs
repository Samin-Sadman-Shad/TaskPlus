using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Persistence.Seed;

/// <summary>
/// <b>Development only.</b> Applies pending migrations and creates three sample users (one per role)
/// and two sample projects with tasks. It runs only when <c>Seed:Enabled</c> is true and the host
/// environment is Development (the caller checks the environment). Passwords come from configuration
/// (user-secrets), never from source. It is idempotent: if any user exists, it does nothing.
/// </summary>
public sealed class DevelopmentDataSeeder(
    SmartTaskDbContext context,
    IPasswordHasher passwordHasher,
    TimeProvider time,
    IConfiguration configuration,
    ILogger<DevelopmentDataSeeder> logger)
{
    public const string EnabledKey = "Seed:Enabled";

    private static readonly SeedUser[] Users =
    [
        new("Seed:AdminPassword", "Alex", "Admin", "admin@smarttask.local", UserRole.Admin),
        new("Seed:ManagerPassword", "Morgan", "Manager", "manager@smarttask.local", UserRole.ProjectManager),
        new("Seed:MemberPassword", "Taylor", "Member", "member@smarttask.local", UserRole.TeamMember),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Development seed skipped: the database already has users");
            return;
        }

        var missing = Users.Where(u => string.IsNullOrWhiteSpace(configuration[u.PasswordKey])).Select(u => u.PasswordKey).ToList();
        if (missing.Count > 0)
        {
            logger.LogWarning("Development seed skipped: set {Keys} with dotnet user-secrets", string.Join(", ", missing));
            return;
        }

        var now = time.GetUtcNow();
        var created = Users.ToDictionary(u => u.Role, u => CreateUser(u, now));
        context.Users.AddRange(created.Values);

        var manager = created[UserRole.ProjectManager];
        var member = created[UserRole.TeamMember];
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var website = Project.Create("Website relaunch", "Redesign and relaunch the public website.", today, today.AddDays(60), manager.Id);
        website.AddMember(member.Id, now);
        var mobile = Project.Create("Mobile app MVP", "First version of the mobile app.", today, null, manager.Id);
        context.Projects.AddRange(website, mobile);

        context.TaskItems.AddRange(
            TaskItem.Create(website.Id, "Draft the sitemap", "List every page and its owner.", TaskPriority.High, today.AddDays(3), member.Id),
            TaskItem.Create(website.Id, "Pick a colour palette", null, TaskPriority.Medium, today.AddDays(10), null),
            TaskItem.Create(mobile.Id, "Set up CI pipeline", null, TaskPriority.Critical, today.AddDays(5), null));

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development seed created {UserCount} users and 2 projects", created.Count);
    }

    private User CreateUser(SeedUser seed, DateTimeOffset now)
    {
        var user = User.Create(seed.FirstName, seed.LastName, seed.Email, "pending");
        user.ChangePasswordHash(passwordHasher.Hash(user, configuration[seed.PasswordKey]!), now);
        user.ChangeRole(seed.Role);
        return user;
    }

    private sealed record SeedUser(string PasswordKey, string FirstName, string LastName, string Email, UserRole Role);
}
