namespace SmartTask.Domain.Enums;

/// <summary>
/// Stored as int; never reorder or renumber. Named <c>TaskItemStatus</c> to avoid clashing with
/// <see cref="System.Threading.Tasks.TaskStatus"/>.
/// </summary>
public enum TaskItemStatus
{
    ToDo = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}
