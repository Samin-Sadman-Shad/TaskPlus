using Shouldly;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.UnitTests.Entities;

public sealed class TaskItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 10);

    public static TheoryData<TaskItemStatus, TaskItemStatus> AllowedTransitions => new()
    {
        { TaskItemStatus.ToDo, TaskItemStatus.InProgress },
        { TaskItemStatus.ToDo, TaskItemStatus.Completed },
        { TaskItemStatus.ToDo, TaskItemStatus.Cancelled },
        { TaskItemStatus.InProgress, TaskItemStatus.ToDo },
        { TaskItemStatus.InProgress, TaskItemStatus.Completed },
        { TaskItemStatus.InProgress, TaskItemStatus.Cancelled },
        { TaskItemStatus.Completed, TaskItemStatus.InProgress },
        { TaskItemStatus.Cancelled, TaskItemStatus.ToDo },
    };

    public static TheoryData<TaskItemStatus, TaskItemStatus> DisallowedTransitions => new()
    {
        { TaskItemStatus.ToDo, TaskItemStatus.ToDo },
        { TaskItemStatus.Completed, TaskItemStatus.ToDo },
        { TaskItemStatus.Completed, TaskItemStatus.Cancelled },
        { TaskItemStatus.Completed, TaskItemStatus.Completed },
        { TaskItemStatus.Cancelled, TaskItemStatus.InProgress },
        { TaskItemStatus.Cancelled, TaskItemStatus.Completed },
    };

    [Fact]
    public void Create_ValidInput_StartsInToDoWithTrimmedText()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "  Write tests  ", "   ", TaskPriority.High, Today, null);

        task.Status.ShouldBe(TaskItemStatus.ToDo);
        task.Title.ShouldBe("Write tests");
        task.Description.ShouldBeNull();
        task.Priority.ShouldBe(TaskPriority.High);
        task.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Create_InvalidTitle_ThrowsDomainException(string title)
    {
        Should.Throw<DomainException>(() => TaskItem.Create(Guid.NewGuid(), title, null, TaskPriority.Low, null, null));
    }

    [Fact]
    public void Create_UndefinedPriority_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() => TaskItem.Create(Guid.NewGuid(), "Valid title", null, (TaskPriority)99, null, null));
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void ChangeStatus_AllowedTransition_ChangesStatus(TaskItemStatus from, TaskItemStatus to)
    {
        var task = CreateTaskIn(from);

        task.ChangeStatus(to, Now);

        task.Status.ShouldBe(to);
    }

    [Theory]
    [MemberData(nameof(DisallowedTransitions))]
    public void ChangeStatus_DisallowedTransition_ThrowsInvalidStatusTransitionException(TaskItemStatus from, TaskItemStatus to)
    {
        var task = CreateTaskIn(from);

        var ex = Should.Throw<InvalidStatusTransitionException>(() => task.ChangeStatus(to, Now));

        ex.From.ShouldBe(from);
        ex.To.ShouldBe(to);
        task.Status.ShouldBe(from);
    }

    [Fact]
    public void ChangeStatus_ToCompleted_SetsCompletedAt()
    {
        var task = CreateTaskIn(TaskItemStatus.InProgress);

        task.ChangeStatus(TaskItemStatus.Completed, Now);

        task.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public void ChangeStatus_ReopenFromCompleted_ClearsCompletedAt()
    {
        var task = CreateTaskIn(TaskItemStatus.Completed);

        task.ChangeStatus(TaskItemStatus.InProgress, Now.AddHours(1));

        task.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo, true)]
    [InlineData(TaskItemStatus.InProgress, true)]
    [InlineData(TaskItemStatus.Completed, false)]
    [InlineData(TaskItemStatus.Cancelled, false)]
    public void IsOverdue_DueDateInPast_DependsOnStatus(TaskItemStatus status, bool expected)
    {
        var task = CreateTaskIn(status, dueDate: Today.AddDays(-1));

        task.IsOverdue(Today).ShouldBe(expected);
    }

    [Fact]
    public void IsOverdue_DueToday_ReturnsFalse()
    {
        CreateTaskIn(TaskItemStatus.ToDo, dueDate: Today).IsOverdue(Today).ShouldBeFalse();
    }

    [Fact]
    public void IsOverdue_NoDueDate_ReturnsFalse()
    {
        CreateTaskIn(TaskItemStatus.ToDo, dueDate: null).IsOverdue(Today).ShouldBeFalse();
    }

    [Fact]
    public void AssignTo_Null_Unassigns()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Valid title", null, TaskPriority.Low, null, Guid.NewGuid());

        task.AssignTo(null);

        task.AssigneeId.ShouldBeNull();
    }

    [Fact]
    public void AssignTo_EmptyGuid_ThrowsDomainException()
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Valid title", null, TaskPriority.Low, null, null);

        Should.Throw<DomainException>(() => task.AssignTo(Guid.Empty));
    }

    /// <summary>Builds a task and walks it to <paramref name="status"/> through allowed transitions.</summary>
    private static TaskItem CreateTaskIn(TaskItemStatus status, DateOnly? dueDate = null)
    {
        var task = TaskItem.Create(Guid.NewGuid(), "Valid title", null, TaskPriority.Medium, dueDate, null);

        if (status != TaskItemStatus.ToDo)
        {
            task.ChangeStatus(status, Now);
        }

        return task;
    }
}
