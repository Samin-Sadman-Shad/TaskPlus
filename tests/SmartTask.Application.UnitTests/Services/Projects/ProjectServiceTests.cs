using System.Net;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Projects;

public sealed class ProjectServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly User _owner = Build.User(UserRole.ProjectManager, firstName: "Olivia");

    private static CreateProjectDto NewProject(string name = "Website") =>
        new() { Name = name, StartDate = TestData.Today, EndDate = TestData.Today.AddDays(10) };

    private static UpdateProjectDto Edit(string name = "Renamed") =>
        new() { Name = name, StartDate = TestData.Today };

    // ---- GetPaged ---------------------------------------------------------------------------

    [Fact]
    public async Task GetPagedAsync_NonAdmin_ScopesQueryToCurrentUser()
    {
        _ctx.ActAs(_owner);
        _ctx.Projects.GetPagedAsync(Arg.Any<ProjectQueryParameters>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProjectListItem>([], 1, 10, 0));

        var response = await _ctx.ProjectService().GetPagedAsync(new ProjectQueryParameters());

        response.Status.ShouldBe(HttpStatusCode.OK);
        await _ctx.Projects.Received(1).GetPagedAsync(Arg.Any<ProjectQueryParameters>(), _owner.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPagedAsync_Admin_IsNotScoped()
    {
        _ctx.ActAs(Build.User(UserRole.Admin));
        _ctx.Projects.GetPagedAsync(Arg.Any<ProjectQueryParameters>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProjectListItem>([], 1, 10, 0));

        await _ctx.ProjectService().GetPagedAsync(new ProjectQueryParameters());

        await _ctx.Projects.Received(1).GetPagedAsync(Arg.Any<ProjectQueryParameters>(), null, Arg.Any<CancellationToken>());
    }

    // ---- GetById ----------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Member_ReturnsDetailsWithCounts()
    {
        var member = Build.User(firstName: "Mia");
        var project = Build.Project(_owner, member);
        _ctx.ActAs(member).Given(project);
        _ctx.Tasks.GetCountsByProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(new ProjectTaskCounts(5, 2));

        var response = await _ctx.ProjectService().GetByIdAsync(project.Id);

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.Owner.FullName.ShouldBe("Olivia User");
        response.Data.Members.ShouldHaveSingleItem().FullName.ShouldBe("Mia User");
        response.Data.TaskCount.ShouldBe(5);
        response.Data.CompletedTaskCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetByIdAsync_NotVisible_Returns404()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(Build.User()).Given(project);

        var response = await _ctx.ProjectService().GetByIdAsync(project.Id);

        response.Status.ShouldBe(HttpStatusCode.NotFound);
        response.Message.ShouldBe(ResponseMessages.ProjectNotFound);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_Returns404()
    {
        _ctx.ActAs(Build.User(UserRole.Admin));

        (await _ctx.ProjectService().GetByIdAsync(Guid.NewGuid())).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Create -----------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ProjectManager_Returns201OwnedByCaller()
    {
        _ctx.ActAs(_owner);
        Project? added = null;
        await _ctx.Projects.AddAsync(Arg.Do<Project>(p => added = p), Arg.Any<CancellationToken>());
        _ctx.Projects.GetDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => WithOwner(added!));

        var response = await _ctx.ProjectService().CreateAsync(NewProject());

        response.Status.ShouldBe(HttpStatusCode.Created);
        added.ShouldNotBeNull().OwnerId.ShouldBe(_owner.Id);
        response.Data!.Name.ShouldBe("Website");
        response.Data.Owner.Id.ShouldBe(_owner.Id);
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task CreateAsync_TeamMember_Returns403WithoutSaving()
    {
        _ctx.ActAs(Build.User());

        var response = await _ctx.ProjectService().CreateAsync(NewProject());

        response.Status.ShouldBe(HttpStatusCode.Forbidden);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Returns409WithoutSaving()
    {
        _ctx.ActAs(_owner);
        _ctx.Projects.NameExistsForOwnerAsync(_owner.Id, "Website", null, Arg.Any<CancellationToken>()).Returns(true);

        var response = await _ctx.ProjectService().CreateAsync(NewProject());

        response.Status.ShouldBe(HttpStatusCode.Conflict);
        response.Message.ShouldBe(ResponseMessages.ProjectNameTaken);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_DomainRuleBroken_Returns400()
    {
        _ctx.ActAs(_owner);

        var response = await _ctx.ProjectService().CreateAsync(NewProject() with { EndDate = TestData.Today.AddDays(-1) });

        response.Status.ShouldBe(HttpStatusCode.BadRequest);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_UniqueIndexRaceOnSave_Returns409()
    {
        _ctx.ActAs(_owner);
        _ctx.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictException("duplicate"));

        (await _ctx.ProjectService().CreateAsync(NewProject())).Status.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---- Update -----------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Owner_Returns200AndSavesOnce()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(_owner).Given(project);

        var response = await _ctx.ProjectService().UpdateAsync(project.Id, Edit());

        response.Status.ShouldBe(HttpStatusCode.OK);
        project.Name.ShouldBe("Renamed");
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task UpdateAsync_Member_Returns403()
    {
        var member = Build.User();
        var project = Build.Project(_owner, member);
        _ctx.ActAs(member).Given(project);

        var response = await _ctx.ProjectService().UpdateAsync(project.Id, Edit());

        response.Status.ShouldBe(HttpStatusCode.Forbidden);
        response.Message.ShouldBe(ResponseMessages.ProjectModifyForbidden);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task UpdateAsync_OtherProjectManagerNotMember_Returns404()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(Build.User(UserRole.ProjectManager)).Given(project);

        (await _ctx.ProjectService().UpdateAsync(project.Id, Edit())).Status.ShouldBe(HttpStatusCode.NotFound);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_Returns409()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(_owner).Given(project);
        _ctx.Projects.NameExistsForOwnerAsync(_owner.Id, "Taken", project.Id, Arg.Any<CancellationToken>()).Returns(true);

        (await _ctx.ProjectService().UpdateAsync(project.Id, Edit("Taken"))).Status.ShouldBe(HttpStatusCode.Conflict);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentEdit_Returns409()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(_owner).Given(project);
        _ctx.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictException("changed"));

        (await _ctx.ProjectService().UpdateAsync(project.Id, Edit())).Status.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---- Delete -----------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Owner_RemovesProjectAndTasksInOneSave()
    {
        var project = Build.Project(_owner);
        var tasks = new[] { Build.Task(project), Build.Task(project) };
        _ctx.ActAs(_owner).Given(project);
        _ctx.Tasks.GetByProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(tasks);

        var response = await _ctx.ProjectService().DeleteAsync(project.Id);

        response.Status.ShouldBe(HttpStatusCode.NoContent);
        _ctx.Projects.Received(1).Remove(project);
        _ctx.Tasks.Received(1).Remove(tasks[0]);
        _ctx.Tasks.Received(1).Remove(tasks[1]);
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task DeleteAsync_Member_Returns403()
    {
        var member = Build.User();
        var project = Build.Project(_owner, member);
        _ctx.ActAs(member).Given(project);

        (await _ctx.ProjectService().DeleteAsync(project.Id)).Status.ShouldBe(HttpStatusCode.Forbidden);
        _ctx.Projects.DidNotReceive().Remove(Arg.Any<Project>());
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task DeleteAsync_Missing_Returns404()
    {
        _ctx.ActAs(Build.User(UserRole.Admin));

        (await _ctx.ProjectService().DeleteAsync(Guid.NewGuid())).Status.ShouldBe(HttpStatusCode.NotFound);
        await _ctx.SaveCalled(0);
    }

    // ---- Members ----------------------------------------------------------------------------

    [Fact]
    public async Task AddMemberAsync_ActiveUser_Returns204AndAddsMember()
    {
        var project = Build.Project(_owner);
        var newcomer = Build.User();
        _ctx.ActAs(_owner).Given(project).Given(newcomer);

        var response = await _ctx.ProjectService().AddMemberAsync(project.Id, new AddProjectMemberDto { UserId = newcomer.Id });

        response.Status.ShouldBe(HttpStatusCode.NoContent);
        project.HasMember(newcomer.Id).ShouldBeTrue();
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task AddMemberAsync_InactiveUser_Returns400()
    {
        var project = Build.Project(_owner);
        var inactive = Build.User(active: false);
        _ctx.ActAs(_owner).Given(project).Given(inactive);

        var response = await _ctx.ProjectService().AddMemberAsync(project.Id, new AddProjectMemberDto { UserId = inactive.Id });

        response.Status.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe(ResponseMessages.UserNotFoundOrInactive);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task AddMemberAsync_UnknownUser_Returns400()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(_owner).Given(project);

        (await _ctx.ProjectService().AddMemberAsync(project.Id, new AddProjectMemberDto { UserId = Guid.NewGuid() }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddMemberAsync_Member_Returns403()
    {
        var member = Build.User();
        var project = Build.Project(_owner, member);
        _ctx.ActAs(member).Given(project);

        (await _ctx.ProjectService().AddMemberAsync(project.Id, new AddProjectMemberDto { UserId = Guid.NewGuid() }))
            .Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveMemberAsync_Member_Returns204()
    {
        var member = Build.User();
        var project = Build.Project(_owner, member);
        _ctx.ActAs(_owner).Given(project);

        var response = await _ctx.ProjectService().RemoveMemberAsync(project.Id, member.Id);

        response.Status.ShouldBe(HttpStatusCode.NoContent);
        project.HasMember(member.Id).ShouldBeFalse();
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task RemoveMemberAsync_Owner_Returns400FromDomain()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(Build.User(UserRole.Admin)).Given(project);

        (await _ctx.ProjectService().RemoveMemberAsync(project.Id, _owner.Id)).Status.ShouldBe(HttpStatusCode.BadRequest);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task RemoveMemberAsync_NotAMember_Returns404()
    {
        var project = Build.Project(_owner);
        _ctx.ActAs(_owner).Given(project);

        var response = await _ctx.ProjectService().RemoveMemberAsync(project.Id, Guid.NewGuid());

        response.Status.ShouldBe(HttpStatusCode.NotFound);
        response.Message.ShouldBe(ResponseMessages.MemberNotFound);
    }

    /// <summary>Mimics GetDetailsAsync after the save: EF would load the Owner navigation.</summary>
    private Project WithOwner(Project project)
    {
        typeof(Project).GetProperty(nameof(Project.Owner))!.SetValue(project, _owner);
        return project;
    }
}
