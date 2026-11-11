using System.Net;
using NSubstitute;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Users;

public sealed class UserServiceCreateTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly User _admin = Build.User(UserRole.Admin);

    public UserServiceCreateTests()
    {
        _ctx.Given(_admin);
        _ctx.PasswordHasher.Hash(Arg.Any<User>(), TestData.ValidPassword).Returns("pbkdf2-hash");
    }

    private static CreateUserDto Dto(UserRole role = UserRole.ProjectManager) => new()
    {
        FirstName = "Pat",
        LastName = "Manager",
        Email = "pat.manager@example.com",
        Password = TestData.ValidPassword,
        ConfirmPassword = TestData.ValidPassword,
        Role = role,
    };

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.ProjectManager)]
    [InlineData(UserRole.TeamMember)]
    public async Task CreateAsync_Admin_CreatesUserWithRoleAndHashedPassword(UserRole role)
    {
        _ctx.ActAs(_admin);

        var response = await _ctx.UserService().CreateAsync(Dto(role));

        response.Status.ShouldBe(HttpStatusCode.Created);
        response.Data!.Role.ShouldBe(role);
        response.Data.Email.ShouldBe("pat.manager@example.com");
        await _ctx.Users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == role && u.PasswordHash == "pbkdf2-hash" && u.IsActive && u.MustChangePassword), Arg.Any<CancellationToken>());
        response.Data.MustChangePassword.ShouldBeTrue();
        await _ctx.SaveCalled(1);
    }

    [Theory]
    [InlineData(UserRole.ProjectManager)]
    [InlineData(UserRole.TeamMember)]
    public async Task CreateAsync_NonAdmin_Returns403AndCreatesNothing(UserRole callerRole)
    {
        _ctx.ActAs(Build.User(callerRole));

        (await _ctx.UserService().CreateAsync(Dto())).Status.ShouldBe(HttpStatusCode.Forbidden);

        await _ctx.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_EmailTaken_Returns409WithoutSaving()
    {
        _ctx.ActAs(_admin);
        _ctx.Users.EmailExistsAsync("pat.manager@example.com", Arg.Any<CancellationToken>()).Returns(true);

        (await _ctx.UserService().CreateAsync(Dto())).Status.ShouldBe(HttpStatusCode.Conflict);

        await _ctx.SaveCalled(0);
    }

    [Fact]
    public async Task CreateAsync_UniqueIndexRace_Returns409()
    {
        _ctx.ActAs(_admin);
        _ctx.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<int>(_ => throw new ConflictException("duplicate"));

        (await _ctx.UserService().CreateAsync(Dto())).Status.ShouldBe(HttpStatusCode.Conflict);
    }
}
