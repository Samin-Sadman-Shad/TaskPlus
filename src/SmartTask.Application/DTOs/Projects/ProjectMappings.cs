using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.DTOs.Projects;

/// <summary>One-way mapping from <see cref="Project"/> and <see cref="ProjectListItem"/> to the project read DTOs.</summary>
public static class ProjectMappings
{
    /// <summary>
    /// Maps a project loaded with <c>IProjectRepository.GetDetailsAsync</c>, which includes <c>Owner</c>
    /// and <c>Members.User</c>. The task counts are passed in because the details query does not load tasks.
    /// </summary>
    /// <exception cref="InvalidOperationException">The owner or a member's user was not loaded.</exception>
    public static ProjectDto ToDto(this Project project, int taskCount, int completedTaskCount)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Owner = Loaded(project.Owner, nameof(Project.Owner)).ToSummaryDto(),
            Members = project.Members
                .OrderBy(m => m.JoinedAt)
                .Select(ToDto)
                .ToList(),
            TaskCount = taskCount,
            CompletedTaskCount = completedTaskCount,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
        };
    }

    /// <exception cref="InvalidOperationException">The member's user was not loaded.</exception>
    public static ProjectMemberDto ToDto(this ProjectMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var user = Loaded(member.User, nameof(ProjectMember.User));
        return new ProjectMemberDto(member.UserId, user.FullName, user.Email, member.JoinedAt);
    }

    /// <example>Paged list: <c>pagedItems.Map(p =&gt; p.ToSummaryDto())</c>.</example>
    public static ProjectSummaryDto ToSummaryDto(this ProjectListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ProjectSummaryDto
        {
            Id = item.Id,
            Name = item.Name,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            OwnerName = item.OwnerName,
            TaskCount = item.TaskCount,
            CompletedTaskCount = item.CompletedTaskCount,
            MemberCount = item.MemberCount,
            CreatedAt = item.CreatedAt,
        };
    }

    /// <summary>
    /// Fails loudly when the repository did not include a navigation it promised, instead of
    /// silently returning a DTO with empty names.
    /// </summary>
    private static User Loaded(User? user, string navigation) =>
        user ?? throw new InvalidOperationException(
            $"'{navigation}' was not loaded. Load the project with IProjectRepository.GetDetailsAsync before mapping.");
}
