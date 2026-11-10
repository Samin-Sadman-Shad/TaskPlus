using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Authorization;

/// <summary>The permission table from CLAUDE.md §4, rule by rule.</summary>
public sealed class ProjectAccessGuardTests
{
    private readonly ServiceTestContext _ctx = new();

    [Fact]
    public async Task Admin_CanViewAndManageAnyProject()
    {
        var admin = Build.User(UserRole.Admin);
        var project = Build.Project(Build.User(UserRole.ProjectManager));
        _ctx.ActAs(admin).Given(project);
        var guard = _ctx.Access;

        guard.IsAdmin.ShouldBeTrue();
        guard.VisibilityScopeUserId.ShouldBeNull();
        guard.CanViewProject(project).ShouldBeTrue();
        (await guard.CanViewProjectAsync(project.Id)).ShouldBeTrue();
        guard.CanManageProject(project).ShouldBeTrue();
    }

    [Fact]
    public void ProjectManagerOwner_CanManageOwnProject()
    {
        var owner = Build.User(UserRole.ProjectManager);
        var project = Build.Project(owner);
        var guard = _ctx.ActAs(owner).Access;

        guard.VisibilityScopeUserId.ShouldBe(owner.Id);
        guard.CanViewProject(project).ShouldBeTrue();
        guard.CanManageProject(project).ShouldBeTrue();
    }

    [Fact]
    public void ProjectManagerMember_CanViewButNotManageOthersProject()
    {
        var pm = Build.User(UserRole.ProjectManager);
        var project = Build.Project(Build.User(UserRole.ProjectManager), pm);
        var guard = _ctx.ActAs(pm).Access;

        guard.CanViewProject(project).ShouldBeTrue();
        guard.CanManageProject(project).ShouldBeFalse();
    }

    [Fact]
    public void TeamMember_CanViewButNotManage()
    {
        var member = Build.User();
        var project = Build.Project(Build.User(UserRole.ProjectManager), member);
        var guard = _ctx.ActAs(member).Access;

        guard.HasManagementRole.ShouldBeFalse();
        guard.CanViewProject(project).ShouldBeTrue();
        guard.CanManageProject(project).ShouldBeFalse();
    }

    [Fact]
    public async Task Outsider_CannotViewProject()
    {
        var outsider = Build.User(UserRole.ProjectManager);
        var project = Build.Project(Build.User(UserRole.ProjectManager));
        _ctx.ActAs(outsider).Given(project);
        var guard = _ctx.Access;

        guard.CanViewProject(project).ShouldBeFalse();
        (await guard.CanViewProjectAsync(project.Id)).ShouldBeFalse();
    }

    [Fact]
    public void DemotedOwner_LosesManagementRights()
    {
        var owner = Build.User(UserRole.ProjectManager);
        var project = Build.Project(owner);
        owner.ChangeRole(UserRole.TeamMember);
        var guard = _ctx.ActAs(owner).Access;

        guard.CanViewProject(project).ShouldBeTrue();
        guard.CanManageProject(project).ShouldBeFalse();
    }

    [Fact]
    public void Assignee_CanChangeStatus_OtherMemberCannot()
    {
        var assignee = Build.User();
        var other = Build.User();
        var project = Build.Project(Build.User(UserRole.ProjectManager), assignee, other);
        var task = Build.Task(project, assignee);

        _ctx.ActAs(assignee).Access.CanChangeTaskStatus(project, task).ShouldBeTrue();
        new ServiceTestContext().ActAs(other).Access.CanChangeTaskStatus(project, task).ShouldBeFalse();
    }

    [Fact]
    public void FormerMemberStillAssigned_CannotChangeStatus()
    {
        var assignee = Build.User();
        var project = Build.Project(Build.User(UserRole.ProjectManager), assignee);
        var task = Build.Task(project, assignee);
        project.RemoveMember(assignee.Id);

        _ctx.ActAs(assignee).Access.CanChangeTaskStatus(project, task).ShouldBeFalse();
    }
}
