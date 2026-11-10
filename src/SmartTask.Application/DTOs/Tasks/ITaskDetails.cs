using SmartTask.Domain.Enums;

namespace SmartTask.Application.DTOs.Tasks;

/// <summary>
/// The editable task fields that <see cref="CreateTaskDto"/> and <see cref="UpdateTaskDto"/> share.
/// It exists so one validator base class holds the rules for both.
/// </summary>
public interface ITaskDetails
{
    string Title { get; }

    string? Description { get; }

    TaskPriority Priority { get; }

    DateOnly? DueDate { get; }
}
