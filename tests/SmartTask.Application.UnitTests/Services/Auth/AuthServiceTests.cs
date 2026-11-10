using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Common.Models.Auth;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Services.Auth;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Services.Auth;

public sealed class AuthServiceTests
{
    private const string PresentedRaw = "presented-raw";
    private const string PresentedHash = "presented-hash";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenService _refreshService = Substitute.For<IRefreshTokenService>();
    private readonly IRefreshTokenTransport _transport = Substitute.For<IRefreshTokenTransport>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = TestData.Clock();
    private readonly GeneratedRefreshToken _next;
    private readonly User _user = Build.User(UserRole.TeamMember);

    public AuthServiceTests()
    {
        _next = new GeneratedRefreshToken("next-raw", "next-hash", Now.AddDays(7));
        _refreshService.Generate().Returns(_next);
        _refreshService.Hash(PresentedRaw).Returns(PresentedHash);
        _jwt.CreateAccessToken(Arg.Any<User>()).Returns(new AccessTokenResult("access.jwt", Now.AddMinutes(15)));
        _currentUser.IpAddress.Returns("203.0.113.7");

        _users.GetByEmailAsync(_user.Email, Arg.Any<CancellationToken>()).Returns(_user);
        _users.GetWithActiveRefreshTokensAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private DateTimeOffset Now => _time.GetUtcNow();

    private AuthService Service() => new(
        _users, _refreshTokens, _unitOfWork, _hasher, _jwt, _refreshService, _transport, _currentUser, _time,
        NullLogger<AuthService>.Instance);

    private Task SaveCalled(int times) => _unitOfWork.Received(times).SaveChangesAsync(Arg.Any<CancellationToken>());

    /// <summary>Gives the user a stored token, and makes the transport present it.</summary>
    private RefreshToken PresentToken(DateTimeOffset? expiresAt = null)
    {
        var token = RefreshToken.Create(_user.Id, PresentedHash, expiresAt ?? Now.AddDays(7), Now.AddDays(-8), null);
        _user.AddRefreshToken(token);
        _transport.Read().Returns(PresentedRaw);
        _refreshTokens.GetByTokenHashAsync(PresentedHash, Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    private static RegisterRequestDto Registration(string email = "new.user@example.com") => new()
    {
        FirstName = "New",
        LastName = "User",
        Email = email,
        Password = TestData.ValidPassword,
        ConfirmPassword = TestData.ValidPassword,
    };

    private LoginRequestDto Login(string? email = null) => new() { Email = email ?? _user.Email, Password = TestData.ValidPassword };

    // ---- Register ---------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_NewEmail_Returns201TeamMemberWithHashedPassword()
    {
        User? added = null;
        await _users.AddAsync(Arg.Do<User>(u => added = u), Arg.Any<CancellationToken>());
        _hasher.Hash(Arg.Any<User>(), TestData.ValidPassword).Returns("pbkdf2-hash");

        var response = await Service().RegisterAsync(Registration());

        response.Status.ShouldBe(HttpStatusCode.Created);
        response.Data!.Role.ShouldBe(UserRole.TeamMember);
        added.ShouldNotBeNull().PasswordHash.ShouldBe("pbkdf2-hash");
        added.Role.ShouldBe(UserRole.TeamMember);
        await SaveCalled(1);
    }

    [Fact]
    public async Task RegisterAsync_EmailExists_Returns409WithoutSaving()
    {
        _users.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var response = await Service().RegisterAsync(Registration("taken@example.com"));

        response.Status.ShouldBe(HttpStatusCode.Conflict);
        response.Message.ShouldBe(ResponseMessages.EmailTaken);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await SaveCalled(0);
    }

    [Fact]
    public async Task RegisterAsync_UniqueIndexRaceOnSave_Returns409WithSameMessage()
    {
        _hasher.Hash(Arg.Any<User>(), Arg.Any<string>()).Returns("hash");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictException("duplicate"));

        var response = await Service().RegisterAsync(Registration());

        response.Status.ShouldBe(HttpStatusCode.Conflict);
        response.Message.ShouldBe(ResponseMessages.EmailTaken);
    }

    // ---- Login ------------------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_ValidCredentials_IssuesTokensAndSetsCookie()
    {
        _hasher.Verify(_user, _user.PasswordHash, TestData.ValidPassword).Returns(PasswordCheckResult.Success);

        var response = await Service().LoginAsync(Login());

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.AccessToken.ShouldBe("access.jwt");
        response.Data.User.Id.ShouldBe(_user.Id);
        _transport.Received(1).Write("next-raw", _next.ExpiresAt);
        var stored = _user.RefreshTokens.ShouldHaveSingleItem();
        stored.TokenHash.ShouldBe("next-hash");
        stored.CreatedByIp.ShouldBe("203.0.113.7");
        _user.LastLoginAt.ShouldBe(Now);
        await SaveCalled(1);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Returns401WithoutCookie()
    {
        _hasher.Verify(_user, Arg.Any<string>(), Arg.Any<string>()).Returns(PasswordCheckResult.Failed);

        var response = await Service().LoginAsync(Login());

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        response.Message.ShouldBe(ResponseMessages.InvalidCredentials);
        _transport.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<DateTimeOffset>());
        await SaveCalled(0);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_Returns401WithSameMessageAndStillHashes()
    {
        var response = await Service().LoginAsync(Login("nobody@example.com"));

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        response.Message.ShouldBe(ResponseMessages.InvalidCredentials);
        // A dummy verification runs, so timing doesn't reveal that the email is unknown.
        _hasher.Received(1).Verify(Arg.Any<User>(), Arg.Any<string>(), TestData.ValidPassword);
        await SaveCalled(0);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_Returns401WithSameMessage()
    {
        _user.Deactivate();
        _hasher.Verify(_user, Arg.Any<string>(), Arg.Any<string>()).Returns(PasswordCheckResult.Success);

        var response = await Service().LoginAsync(Login());

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        response.Message.ShouldBe(ResponseMessages.InvalidCredentials);
        _transport.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task LoginAsync_RehashNeeded_UpgradesHashAndKeepsOtherSessions()
    {
        var otherSession = RefreshToken.Create(_user.Id, "other-device", Now.AddDays(3), Now.AddDays(-1), null);
        _user.AddRefreshToken(otherSession);
        _hasher.Verify(_user, Arg.Any<string>(), Arg.Any<string>()).Returns(PasswordCheckResult.SuccessRehashNeeded);
        _hasher.Hash(_user, TestData.ValidPassword).Returns("upgraded-hash");

        var response = await Service().LoginAsync(Login());

        response.Status.ShouldBe(HttpStatusCode.OK);
        _user.PasswordHash.ShouldBe("upgraded-hash");
        otherSession.IsActive(Now).ShouldBeTrue();
        await SaveCalled(1);
    }

    // ---- Refresh ----------------------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_NoCookie_Returns401()
    {
        var response = await Service().RefreshAsync();

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        await _refreshTokens.DidNotReceive().GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _transport.Received(1).Clear();
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_Returns401AndClearsCookie()
    {
        _transport.Read().Returns("forged-token");
        _refreshService.Hash("forged-token").Returns("forged-hash");

        var response = await Service().RefreshAsync();

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        _transport.Received(1).Clear();
        await SaveCalled(0);
    }

    [Fact]
    public async Task RefreshAsync_ActiveToken_RotatesAndWritesNewCookie()
    {
        var presented = PresentToken();

        var response = await Service().RefreshAsync();

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data!.AccessToken.ShouldBe("access.jwt");
        presented.IsRevoked.ShouldBeTrue();
        presented.RevokedReason.ShouldBe("Rotated");
        presented.ReplacedByTokenHash.ShouldBe("next-hash");
        _user.RefreshTokens.ShouldContain(t => t.TokenHash == "next-hash" && t.IsActive(Now));
        _transport.Received(1).Write("next-raw", _next.ExpiresAt);
        await SaveCalled(1);
    }

    [Fact]
    public async Task RefreshAsync_RevokedTokenReused_RevokesAllSessions()
    {
        var presented = PresentToken();
        presented.Revoke(Now.AddMinutes(-5), "Rotated", "successor-hash");
        var successor = RefreshToken.Create(_user.Id, "successor-hash", Now.AddDays(7), Now.AddMinutes(-5), null);
        _user.AddRefreshToken(successor);

        var response = await Service().RefreshAsync();

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        successor.IsActive(Now).ShouldBeFalse();
        successor.RevokedReason.ShouldBe("Reuse detected");
        _transport.Received(1).Clear();
        _transport.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<DateTimeOffset>());
        await SaveCalled(1);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_Returns401WithoutRotating()
    {
        var presented = PresentToken(expiresAt: Now.AddSeconds(-1));

        var response = await Service().RefreshAsync();

        response.Status.ShouldBe(HttpStatusCode.Unauthorized);
        presented.IsRevoked.ShouldBeFalse();
        _transport.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<DateTimeOffset>());
        await SaveCalled(0);
    }

    [Fact]
    public async Task RefreshAsync_InactiveUser_Returns401()
    {
        PresentToken();
        _user.Deactivate();

        (await Service().RefreshAsync()).Status.ShouldBe(HttpStatusCode.Unauthorized);
        _transport.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<DateTimeOffset>());
    }

    // ---- Logout -----------------------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_ActiveToken_RevokesAndClearsCookie()
    {
        var presented = PresentToken();

        var response = await Service().LogoutAsync();

        response.Status.ShouldBe(HttpStatusCode.NoContent);
        presented.RevokedReason.ShouldBe("Logout");
        _transport.Received(1).Clear();
        await SaveCalled(1);
    }

    [Fact]
    public async Task LogoutAsync_NoCookie_StillReturns204()
    {
        var response = await Service().LogoutAsync();

        response.Status.ShouldBe(HttpStatusCode.NoContent);
        _transport.Received(1).Clear();
        await SaveCalled(0);
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedToken_IsIdempotent()
    {
        var presented = PresentToken();
        presented.Revoke(Now.AddMinutes(-1), "Logout");

        (await Service().LogoutAsync()).Status.ShouldBe(HttpStatusCode.NoContent);
        await SaveCalled(0);
    }
}
