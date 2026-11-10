using System.Net;
using NSubstitute;
using SmartTask.Application.Common.Models;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Dashboard;

public sealed class DashboardServiceTests
{
    private readonly ServiceTestContext _ctx = new();

    public DashboardServiceTests() =>
        _ctx.Dashboard.GetStatsAsync(Arg.Any<Guid?>(), Arg.Any<UserRole>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new DashboardStatsReadModel
            {
                TotalProjects = 1,
                TotalTasks = 2,
                // Deliberately incomplete: the service output must still contain every key.
                TasksByStatus = new Dictionary<TaskItemStatus, int> { [TaskItemStatus.ToDo] = 2 },
                TasksByPriority = new Dictionary<TaskPriority, int>(),
                CompletedCount = 0,
                PendingCount = 2,
                OverdueCount = 0,
                UpcomingDueTasks = [],
            });

    [Fact]
    public async Task GetStatsAsync_FillsEveryEnumKey()
    {
        _ctx.ActAs(Build.User());

        var response = await _ctx.DashboardService().GetStatsAsync(DashboardConstraints.DefaultUpcomingDays);

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.TasksByStatus.Keys.ShouldBe(Enum.GetValues<TaskItemStatus>(), ignoreOrder: true);
        response.Data.TasksByPriority.Keys.ShouldBe(Enum.GetValues<TaskPriority>(), ignoreOrder: true);
        response.Data.TasksByStatus[TaskItemStatus.ToDo].ShouldBe(2);
        response.Data.TasksByPriority[TaskPriority.High].ShouldBe(0);
    }

    [Fact]
    public async Task GetStatsAsync_Admin_IsGlobal()
    {
        _ctx.ActAs(Build.User(UserRole.Admin));

        await _ctx.DashboardService().GetStatsAsync(7);

        await _ctx.Dashboard.Received(1).GetStatsAsync(null, UserRole.Admin, TestData.Today, 7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.ProjectManager)]
    [InlineData(UserRole.TeamMember)]
    public async Task GetStatsAsync_NonAdmin_IsScopedToCaller(UserRole role)
    {
        var user = Build.User(role);
        _ctx.ActAs(user);

        await _ctx.DashboardService().GetStatsAsync(7);

        await _ctx.Dashboard.Received(1).GetStatsAsync(user.Id, role, TestData.Today, 7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, DashboardConstraints.MinUpcomingDays)]
    [InlineData(-5, DashboardConstraints.MinUpcomingDays)]
    [InlineData(DashboardConstraints.MaxUpcomingDays + 1, DashboardConstraints.MaxUpcomingDays)]
    [InlineData(14, 14)]
    public async Task GetStatsAsync_ClampsUpcomingDays(int requested, int expected)
    {
        _ctx.ActAs(Build.User(UserRole.Admin));

        await _ctx.DashboardService().GetStatsAsync(requested);

        await _ctx.Dashboard.Received(1).GetStatsAsync(Arg.Any<Guid?>(), Arg.Any<UserRole>(), Arg.Any<DateOnly>(), expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatsAsync_Anonymous_Returns401() =>
        (await _ctx.DashboardService().GetStatsAsync(7)).Status.ShouldBe(HttpStatusCode.Unauthorized);
}
