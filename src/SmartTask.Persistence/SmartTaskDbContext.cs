using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartTask.Domain.Common;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Persistence;

public sealed class SmartTaskDbContext : DbContext
{
    public SmartTaskDbContext(DbContextOptions<SmartTaskDbContext> options)
        : base(options)
    {
        // Cascade at save time, not when Remove() is called. SoftDeleteInterceptor turns a soft-deletable
        // delete into an update before then, so the dependents of a soft-deleted project (members,
        // tasks) are never hard-deleted by accident.
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartTaskDbContext).Assembly);
        ConfigureClientGeneratedIds(modelBuilder);
        ApplySoftDeleteFilters(modelBuilder);
    }

    /// <summary>
    /// <see cref="BaseEntity"/> creates its own Id (<c>Guid.CreateVersion7()</c>). Without this, EF
    /// would treat a new entity that already has an Id, added through a navigation of a tracked
    /// entity (for example a refresh token added with <c>User.AddRefreshToken</c>), as an existing
    /// row and issue an UPDATE instead of an INSERT.
    /// </summary>
    private static void ConfigureClientGeneratedIds(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in entityTypes)
        {
            modelBuilder.Entity(clrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateOnly>().HaveColumnType("date");
        configurationBuilder.Properties<UserRole>().HaveConversion<int>();
        configurationBuilder.Properties<TaskItemStatus>().HaveConversion<int>();
        configurationBuilder.Properties<TaskPriority>().HaveConversion<int>();
    }

    /// <summary>Adds <c>e =&gt; !e.IsDeleted</c> as a global query filter on every <see cref="ISoftDeletable"/> entity.</summary>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        var softDeletableTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in softDeletableTypes)
        {
            var entity = Expression.Parameter(clrType, "e");
            var notDeleted = Expression.Not(Expression.Property(entity, nameof(ISoftDeletable.IsDeleted)));
            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(notDeleted, entity));
        }
    }
}
