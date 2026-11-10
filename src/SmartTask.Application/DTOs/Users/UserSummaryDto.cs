using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Users;

/// <summary>
/// Short user reference. Returned by <c>GET users/lookup</c>, and nested in
/// <see cref="Projects.ProjectDto.Owner"/> and <see cref="Tasks.TaskDto.Assignee"/>.
/// </summary>
public sealed record UserSummaryDto(Guid Id, string FullName, string Email, UserRole Role);
