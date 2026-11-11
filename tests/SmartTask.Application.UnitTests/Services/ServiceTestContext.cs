using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Services.Authorization;
using SmartTask.Application.Services.Dashboard;
using SmartTask.Application.Services.Projects;
using SmartTask.Application.Services.Tasks;
using SmartTask.Application.Services.Users;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.UnitTests.Services;

/// <summary>
/// Substitutes for every contract a service depends on, plus the <b>real</b>
/// <see cref="ProjectAccessGuard"/>, so service tests also exercise the permission rules.
/// "Given" methods make the repositories return the entities as the real ones would.
/// </summary>
internal sealed class ServiceTestContext
{
    public ServiceTestContext()
    {
        Tasks.GetCountsByProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ProjectTaskCounts.None);
        Tasks.GetByProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<TaskItem>());
    }

    public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

    public IUserRepository Users { get; } = Substitute.For<IUserRepository>();

    public IProjectRepository Projects { get; } = Substitute.For<IProjectRepository>();

    public ITaskItemRepository Tasks { get; } = Substitute.For<ITaskItemRepository>();

    public IDashboardReadRepository Dashboard { get; } = Substitute.For<IDashboardReadRepository>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IPasswordHasher PasswordHasher { get; } = Substitute.For<IPasswordHasher>();

    public FakeTimeProvider Time { get; } = TestData.Clock();

    public ProjectAccessGuard Access => new(CurrentUser, Projects);

    public ServiceTestContext ActAs(User user)
    {
        CurrentUser.UserId.Returns(user.Id);
        CurrentUser.Role.Returns(user.Role);
        CurrentUser.IsAuthenticated.Returns(true);
        return this;
    }

    public ServiceTestContext Given(params User[] users)
    {
        foreach (var user in users)
        {
            Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
            Users.GetWithActiveRefreshTokensAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        }

        return this;
    }

    public ServiceTestContext Given(Project project)
    {
        Projects.GetDetailsAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        Projects.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        Projects.ExistsAsync(project.Id, Arg.Any<CancellationToken>()).Returns(true);
        Projects.IsUserOwnerOrMemberAsync(project.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => project.HasMember(call.ArgAt<Guid>(1)));
        return this;
    }

    public ServiceTestContext Given(TaskItem task)
    {
        Tasks.GetDetailsAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        Tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        return this;
    }

    public Task SaveCalled(int times) => UnitOfWork.Received(times).SaveChangesAsync(Arg.Any<CancellationToken>());

    public UserService UserService() =>
        new(Users, UnitOfWork, Access, CurrentUser, PasswordHasher, Time, NullLogger<UserService>.Instance);

    public ProjectService ProjectService() =>
        new(Projects, Tasks, Users, UnitOfWork, Access, CurrentUser, Time, NullLogger<ProjectService>.Instance);

    public TaskService TaskService() =>
        new(Tasks, Projects, Users, UnitOfWork, Access, CurrentUser, Time, NullLogger<TaskService>.Instance);

    public DashboardService DashboardService() => new(Dashboard, Access, CurrentUser, Time);
}
