using System.Reflection;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services;

/// <summary>
/// Builds valid domain entities for service tests. Navigation properties (<c>Owner</c>,
/// <c>Members.User</c>, <c>Assignee</c>) are normally filled in by EF Core. Here they are set by
/// reflection, to mimic what the repositories promise to include.
/// </summary>
internal static class Build
{
    public static User User(UserRole role = UserRole.TeamMember, bool active = true, string firstName = "Test")
    {
        var user = SmartTask.Domain.Entities.User.Create(firstName, "User", $"{Guid.NewGuid():N}@example.com", "hash");
        user.ChangeRole(role);
        if (!active)
        {
            user.Deactivate();
        }

        return user;
    }

    /// <summary>A project owned by <paramref name="owner"/> with the given explicit members, all navigations loaded.</summary>
    public static Project Project(User owner, params User[] members)
    {
        var project = SmartTask.Domain.Entities.Project.Create($"Project {Guid.NewGuid():N}"[..20], null, TestData.Today, null, owner.Id);
        SetNavigation(project, nameof(SmartTask.Domain.Entities.Project.Owner), owner);

        foreach (var member in members)
        {
            project.AddMember(member.Id, TestData.Clock().GetUtcNow());
            SetNavigation(project.Members.Single(m => m.UserId == member.Id), nameof(ProjectMember.User), member);
        }

        return project;
    }

    public static TaskItem Task(Project project, User? assignee = null, TaskItemStatus status = TaskItemStatus.ToDo)
    {
        var task = TaskItem.Create(project.Id, "Write the docs", null, TaskPriority.Medium, null, assignee?.Id);
        SetNavigation(task, nameof(TaskItem.Assignee), assignee);

        if (status is TaskItemStatus.InProgress or TaskItemStatus.Completed or TaskItemStatus.Cancelled)
        {
            task.ChangeStatus(status, TestData.Clock().GetUtcNow());
        }

        return task;
    }

    private static void SetNavigation(object entity, string property, object? value) =>
        entity.GetType()
            .GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, value);
}
