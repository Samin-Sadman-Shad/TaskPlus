namespace SmartTask.Application.DTOs.Projects;

/// <summary>
/// The editable project fields that <see cref="CreateProjectDto"/> and <see cref="UpdateProjectDto"/>
/// share. It exists so one validator base class holds the rules for both.
/// </summary>
public interface IProjectDetails
{
    string Name { get; }

    string? Description { get; }

    DateOnly StartDate { get; }

    DateOnly? EndDate { get; }
}
