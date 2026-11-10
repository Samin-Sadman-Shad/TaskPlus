namespace SmartTask.Application.DTOs.Projects;

/// <summary>One explicit member of a project, nested in <see cref="ProjectDto.Members"/>.</summary>
public sealed record ProjectMemberDto(Guid UserId, string FullName, string Email, DateTimeOffset JoinedAt);
