using System.Net;
using NSubstitute;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Users;

public sealed class UserServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly User _admin = Build.User(UserRole.Admin);
    private readonly User _user = Build.User(firstName: "Tom");

    public UserServiceTests() => _ctx.Given(_admin, _user);

    [Fact]
    public async Task GetCurrentAsync_ReturnsCallersProfile()
    {
        _ctx.ActAs(_user);

        var response = await _ctx.UserService().GetCurrentAsync();

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.Id.ShouldBe(_user.Id);
    }

    [Fact]
    public async Task GetCurrentAsync_Anonymous_Returns401() =>
        (await _ctx.UserService().GetCurrentAsync()).Status.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task UpdateCurrentProfileAsync_Returns200AndSavesOnce()
    {
        _ctx.ActAs(_user);

        var response = await _ctx.UserService().UpdateCurrentProfileAsync(new UpdateUserProfileDto { FirstName = "Thomas", LastName = "Lee" });

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.FullName.ShouldBe("Thomas Lee");
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task GetPagedAsync_NonAdmin_Returns403()
    {
        _ctx.ActAs(Build.User(UserRole.ProjectManager));

        (await _ctx.UserService().GetPagedAsync(new UserQueryParameters())).Status.ShouldBe(HttpStatusCode.Forbidden);
        await _ctx.Users.DidNotReceive().GetPagedAsync(Arg.Any<UserQueryParameters>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPagedAsync_Admin_MapsPage()
    {
        _ctx.ActAs(_admin);
        _ctx.Users.GetPagedAsync(Arg.Any<UserQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<User>([_user], 1, 10, 1));

        var response = await _ctx.UserService().GetPagedAsync(new UserQueryParameters());

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.Items.ShouldHaveSingleItem().Id.ShouldBe(_user.Id);
    }

    [Fact]
    public async Task LookupAsync_TeamMember_Returns403()
    {
        _ctx.ActAs(_user);

        (await _ctx.UserService().LookupAsync("a")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LookupAsync_ProjectManager_SearchesActiveUsersCapped()
    {
        _ctx.ActAs(Build.User(UserRole.ProjectManager));
        _ctx.Users.GetPagedAsync(Arg.Any<UserQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<User>([_user], 1, PagingConstraints.LookupMaxResults, 1));

        var response = await _ctx.UserService().LookupAsync("  tom ");

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.ShouldHaveSingleItem().FullName.ShouldBe("Tom User");
        await _ctx.Users.Received(1).GetPagedAsync(
            Arg.Is<UserQueryParameters>(p => p.IsActive == true && p.PageSize == PagingConstraints.LookupMaxResults && p.Search == "tom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_Admin_Missing_Returns404()
    {
        _ctx.ActAs(_admin);

        (await _ctx.UserService().GetByIdAsync(Guid.NewGuid())).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeRoleAsync_Admin_PromotesUser()
    {
        _ctx.ActAs(_admin);

        var response = await _ctx.UserService().ChangeRoleAsync(_user.Id, new ChangeUserRoleDto { Role = UserRole.ProjectManager });

        response.Status.ShouldBe(HttpStatusCode.OK);
        _user.Role.ShouldBe(UserRole.ProjectManager);
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task ChangeRoleAsync_AdminDemotingSelf_Returns400()
    {
        _ctx.ActAs(_admin);

        var response = await _ctx.UserService().ChangeRoleAsync(_admin.Id, new ChangeUserRoleDto { Role = UserRole.TeamMember });

        response.Status.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe(ResponseMessages.CannotDemoteSelf);
        _admin.Role.ShouldBe(UserRole.Admin);
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task ChangeRoleAsync_NonAdmin_Returns403()
    {
        _ctx.ActAs(Build.User(UserRole.ProjectManager));

        (await _ctx.UserService().ChangeRoleAsync(_user.Id, new ChangeUserRoleDto { Role = UserRole.Admin }))
            .Status.ShouldBe(HttpStatusCode.Forbidden);
        _user.Role.ShouldBe(UserRole.TeamMember);
    }

    [Fact]
    public async Task ChangeRoleAsync_UnknownUser_Returns404()
    {
        _ctx.ActAs(_admin);

        (await _ctx.UserService().ChangeRoleAsync(Guid.NewGuid(), new ChangeUserRoleDto { Role = UserRole.Admin }))
            .Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateAsync_Admin_DeactivatesAndRevokesTokens()
    {
        var now = _ctx.Time.GetUtcNow();
        var token = RefreshToken.Create(_user.Id, "hash", now.AddDays(7), now, null);
        _user.AddRefreshToken(token);
        _ctx.ActAs(_admin);

        var response = await _ctx.UserService().DeactivateAsync(_user.Id);

        response.Status.ShouldBe(HttpStatusCode.NoContent);
        _user.IsActive.ShouldBeFalse();
        token.IsActive(now).ShouldBeFalse();
        token.RevokedReason.ShouldBe("User deactivated");
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task DeactivateAsync_Self_Returns400()
    {
        _ctx.ActAs(_admin);

        (await _ctx.UserService().DeactivateAsync(_admin.Id)).Status.ShouldBe(HttpStatusCode.BadRequest);
        _admin.IsActive.ShouldBeTrue();
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task ActivateAsync_Admin_Returns204()
    {
        _user.Deactivate();
        _ctx.ActAs(_admin);

        (await _ctx.UserService().ActivateAsync(_user.Id)).Status.ShouldBe(HttpStatusCode.NoContent);
        _user.IsActive.ShouldBeTrue();
        await _ctx.SaveCalled(1);
    }

    [Fact]
    public async Task ActivateAsync_NonAdmin_Returns403()
    {
        _ctx.ActAs(_user);

        (await _ctx.UserService().ActivateAsync(_user.Id)).Status.ShouldBe(HttpStatusCode.Forbidden);
        await _ctx.SaveCalled(0);
    }
}
