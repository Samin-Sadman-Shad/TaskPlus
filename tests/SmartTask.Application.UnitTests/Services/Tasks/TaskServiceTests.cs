using System.Net;
using NSubstitute;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Tasks;

public sealed class TaskServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly User _owner = Build.User(UserRole.ProjectManager);
    private readonly User _member = Build.User(firstName: "Mia");
    private readonly User _otherMember = Build.User();
    private readonly Project _project;

    public TaskServiceTests()
    {
        _project = Build.Project(_owner, _member, _otherMember);
        _ctx.Given(_project).Given(_owner, _member, _otherMember);
    }

    private static CreateTaskDto NewTask(Guid? assigneeId = null) =>
        new() { Title = "Write docs", Priority = TaskPriority.High, DueDate = TestData.Today.AddDays(3), AssigneeId = assigneeId };

    private TaskItem GivenTask(User? assignee = null, TaskItemStatus status = TaskItemStatus.ToDo)
    {
        var task = Build.Task(_project, assignee, status);
        _ctx.Given(task);
        return task;
    }

    // ---- Reads ------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Member_ReturnsTaskWithProjectName()
    {
        var task = GivenTask(_member);
        _ctx.ActAs(_member);

        var response = await _ctx.TaskService().GetByIdAsync(task.Id);

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.ProjectName.ShouldBe(_project.Name);
        response.Data.Assignee!.FullName.ShouldBe("Mia User");
    }

    [Fact]
    public async Task GetByIdAsync_Outsider_Returns404()
    {
        var task = GivenTask();
        _ctx.ActAs(Build.User());

        var response = await _ctx.TaskService().GetByIdAsync(task.Id);

        response.Status.ShouldBe(HttpStatusCode.NotFound);
        response.Message.ShouldBe(ResponseMessages.TaskNotFound);
    }

    [Fact]
    public async Task GetPagedByProjectAsync_Outsider_Returns404()
    {
        _ctx.ActAs(Build.User());

        (await _ctx.TaskService().GetPagedByProjectAsync(_project.Id, new TaskQueryParameters()))
            .Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPagedByProjectAsync_Member_ForcesRouteProjectIdAndScope()
    {
        _ctx.ActAs(_member);
        _ctx.Tasks.GetPagedAsync(Arg.Any<TaskQueryParameters>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TaskItem>([], 1, 10, 0));

        var response = await _ctx.TaskService().GetPagedByProjectAsync(_project.Id, new TaskQueryParameters { ProjectId = Guid.NewGuid() });

        response.Status.ShouldBe(HttpStatusCode.OK);
        await _ctx.Tasks.Received(1).GetPagedAsync(
            Arg.Is<TaskQueryParameters>(p => p.ProjectId == _project.Id), _member.Id, Arg.Any<CancellationToken>());
    }

    // ---- Create -----------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_OwnerWithMemberAssignee_Returns201()
    {
        _ctx.ActAs(_owner);
        TaskItem? added = null;
        await _ctx.Tasks.AddAsync(Arg.Do<TaskItem>(t => added = t), Arg.Any<CancellationToken>());

        var response = await _ctx.TaskService().CreateAsync(_project.Id, NewTask(_member.Id));

        response.Status.ShouldBe(HttpStatusCode.Created);
        added.ShouldNotBeNull().AssigneeId.ShouldBe(_member.Id);
        added.Status.ShouldBe(TaskItemStatus.ToDo);
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task CreateAsync_AssigneeNotMember_Returns400WithoutSaving()
    {
        var outsider = Build.User();
        _ctx.ActAs(_owner).Given(outsider);

        var response = await _ctx.TaskService().CreateAsync(_project.Id, NewTask(outsider.Id));

        response.Status.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe(ResponseMessages.AssigneeNotMember);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_AssigneeInactiveMember_Returns400()
    {
        _member.Deactivate();
        _ctx.ActAs(_owner);

        (await _ctx.TaskService().CreateAsync(_project.Id, NewTask(_member.Id))).Status.ShouldBe(HttpStatusCode.BadRequest);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_TeamMember_Returns403()
    {
        _ctx.ActAs(_member);

        (await _ctx.TaskService().CreateAsync(_project.Id, NewTask())).Status.ShouldBe(HttpStatusCode.Forbidden);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_ProjectNotVisible_Returns404()
    {
        _ctx.ActAs(Build.User(UserRole.ProjectManager));

        (await _ctx.TaskService().CreateAsync(_project.Id, NewTask())).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Update / delete --------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Owner_Returns200()
    {
        var task = GivenTask();
        _ctx.ActAs(_owner);

        var response = await _ctx.TaskService().UpdateAsync(task.Id, new UpdateTaskDto { Title = "New title", Priority = TaskPriority.Low });

        response.Status.ShouldBe(HttpStatusCode.OK);
        task.Title.ShouldBe("New title");
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task UpdateAsync_Assignee_Returns403()
    {
        // Assignees may change status but not edit the details.
        var task = GivenTask(_member);
        _ctx.ActAs(_member);

        (await _ctx.TaskService().UpdateAsync(task.Id, new UpdateTaskDto { Title = "Hijack" })).Status.ShouldBe(HttpStatusCode.Forbidden);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task UpdateAsync_Missing_Returns404()
    {
        _ctx.ActAs(_owner);

        (await _ctx.TaskService().UpdateAsync(Guid.NewGuid(), new UpdateTaskDto { Title = "x y z" })).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_Owner_Returns204()
    {
        var task = GivenTask();
        _ctx.ActAs(_owner);

        (await _ctx.TaskService().DeleteAsync(task.Id)).Status.ShouldBe(HttpStatusCode.NoContent);
        _ctx.Tasks.Received(1).Remove(task);
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task DeleteAsync_Member_Returns403()
    {
        var task = GivenTask();
        _ctx.ActAs(_member);

        (await _ctx.TaskService().DeleteAsync(task.Id)).Status.ShouldBe(HttpStatusCode.Forbidden);
        _ctx.Tasks.DidNotReceive().Remove(Arg.Any<TaskItem>());
    }

    // ---- Status -----------------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_Assignee_Returns200()
    {
        var task = GivenTask(_member);
        _ctx.ActAs(_member);

        var response = await _ctx.TaskService().UpdateStatusAsync(task.Id, new UpdateTaskStatusDto { Status = TaskItemStatus.Completed });

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.Status.ShouldBe(TaskItemStatus.Completed);
        response.Data.CompletedAt.ShouldBe(_ctx.Time.GetUtcNow());
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_TeamMemberOnSomeoneElsesTask_Returns403()
    {
        var task = GivenTask(_member);
        _ctx.ActAs(_otherMember);

        var response = await _ctx.TaskService().UpdateStatusAsync(task.Id, new UpdateTaskStatusDto { Status = TaskItemStatus.InProgress });

        response.Status.ShouldBe(HttpStatusCode.Forbidden);
        response.Message.ShouldBe(ResponseMessages.TaskStatusForbidden);
        task.Status.ShouldBe(TaskItemStatus.ToDo);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_Returns400()
    {
        var task = GivenTask(_member, TaskItemStatus.Completed);
        _ctx.ActAs(_owner);

        var response = await _ctx.TaskService().UpdateStatusAsync(task.Id, new UpdateTaskStatusDto { Status = TaskItemStatus.Cancelled });

        response.Status.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe("Cannot change task status from 'Completed' to 'Cancelled'.");
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task UpdateStatusAsync_Owner_CanChangeUnassignedTask()
    {
        var task = GivenTask();
        _ctx.ActAs(_owner);

        (await _ctx.TaskService().UpdateStatusAsync(task.Id, new UpdateTaskStatusDto { Status = TaskItemStatus.InProgress }))
            .Status.ShouldBe(HttpStatusCode.OK);
    }

    // ---- Assign -----------------------------------------------------------------------------

    [Fact]
    public async Task AssignAsync_OwnerToMember_Returns200()
    {
        var task = GivenTask();
        _ctx.ActAs(_owner);

        var response = await _ctx.TaskService().AssignAsync(task.Id, new AssignTaskDto { AssigneeId = _otherMember.Id });

        response.Status.ShouldBe(HttpStatusCode.OK);
        task.AssigneeId.ShouldBe(_otherMember.Id);
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task AssignAsync_Null_Unassigns()
    {
        var task = GivenTask(_member);
        _ctx.ActAs(_owner);

        var response = await _ctx.TaskService().AssignAsync(task.Id, new AssignTaskDto { AssigneeId = null });

        response.Status.ShouldBe(HttpStatusCode.OK);
        task.AssigneeId.ShouldBeNull();
    }

    [Fact]
    public async Task AssignAsync_NonMember_Returns400()
    {
        var task = GivenTask();
        _ctx.ActAs(_owner);

        (await _ctx.TaskService().AssignAsync(task.Id, new AssignTaskDto { AssigneeId = Guid.NewGuid() }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task AssignAsync_TeamMember_Returns403()
    {
        var task = GivenTask();
        _ctx.ActAs(_member);

        (await _ctx.TaskService().AssignAsync(task.Id, new AssignTaskDto { AssigneeId = _member.Id }))
            .Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
