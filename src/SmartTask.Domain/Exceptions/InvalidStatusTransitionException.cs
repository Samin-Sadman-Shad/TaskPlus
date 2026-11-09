using SmartTask.Domain.Enums;

namespace SmartTask.Domain.Exceptions;

/// <summary>Thrown when a task status change is not allowed by the transition table.</summary>
public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(TaskItemStatus from, TaskItemStatus to)
        : base($"Cannot change task status from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }

    public TaskItemStatus From { get; }

    public TaskItemStatus To { get; }
}
