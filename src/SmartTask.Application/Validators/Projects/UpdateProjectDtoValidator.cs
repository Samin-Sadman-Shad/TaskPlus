using SmartTask.Application.DTOs.Projects;

namespace SmartTask.Application.Validators.Projects;

/// <summary>
/// Rules for <c>PUT projects/{id}</c>: the shared project rules only. There is no past-date rule, so a
/// project that already started can still be edited.
/// </summary>
public sealed class UpdateProjectDtoValidator : ProjectDetailsValidator<UpdateProjectDto>;
