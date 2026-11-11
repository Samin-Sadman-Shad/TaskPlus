using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Services.Users;
using SmartTask.Application.Validators.Users;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Users;

public sealed class AdminBootstrapServiceTests
{
    private const string AdminEmail = "first.admin@example.com";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public AdminBootstrapServiceTests() =>
        _hasher.Hash(Arg.Any<User>(), TestData.ValidPassword).Returns("pbkdf2-hash");

    private AdminBootstrapService Service() =>
        new(_users, _unitOfWork, _hasher, new CreateUserDtoValidator(), TestData.Clock(), NullLogger<AdminBootstrapService>.Instance);

    private static CreateUserDto Settings(string password = TestData.ValidPassword) => new()
    {
        FirstName = "First",
        LastName = "Admin",
        Email = AdminEmail,
        Password = password,
        Role = UserRole.TeamMember, // ignored: the bootstrap user is always an admin
    };

    [Fact]
    public async Task EnsureAdminAsync_NoAdmin_CreatesActiveAdminWithHashedPassword()
    {
        (await Service().EnsureAdminAsync(Settings())).ShouldBe(AdminBootstrapOutcome.Created);

        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == UserRole.Admin && u.IsActive && u.Email == AdminEmail && u.PasswordHash == "pbkdf2-hash"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAdminAsync_ActiveAdminExists_ChangesNothing()
    {
        _users.ActiveUserWithRoleExistsAsync(UserRole.Admin, Arg.Any<CancellationToken>()).Returns(true);

        (await Service().EnsureAdminAsync(Settings())).ShouldBe(AdminBootstrapOutcome.AdminAlreadyExists);

        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAdminAsync_EmailOwnedByNonAdmin_DoesNotPromoteIt()
    {
        // Someone self-registered with the admin's email before the first start: they must not become admin.
        _users.EmailExistsAsync(AdminEmail, Arg.Any<CancellationToken>()).Returns(true);

        (await Service().EnsureAdminAsync(Settings())).ShouldBe(AdminBootstrapOutcome.EmailBelongsToExistingAccount);

        await _users.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAdminAsync_WeakPassword_ThrowsWithoutRevealingIt()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Service().EnsureAdminAsync(Settings("weak")));

        exception.Message.ShouldContain("Bootstrap:Admin");
        exception.Message.ShouldContain("Password");
        exception.Message.ShouldNotContain("weak");
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAdminAsync_ConcurrentInstanceWins_ReportsAdminAlreadyExists()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<int>(_ => throw new ConflictException("duplicate"));

        (await Service().EnsureAdminAsync(Settings())).ShouldBe(AdminBootstrapOutcome.AdminAlreadyExists);
    }
}
