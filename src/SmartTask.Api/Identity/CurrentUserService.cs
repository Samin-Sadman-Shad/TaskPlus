using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Enums;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Api.Identity;

/// <summary>Reads the current user from the validated access token's claims.</summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(AppClaimTypes.Subject), out var id) ? id : null;

    /// <summary>Only a defined role <b>name</b> is accepted; numeric values such as "1" are rejected.</summary>
    public UserRole? Role =>
        IsAuthenticated
        && Principal!.FindFirstValue(AppClaimTypes.Role) is { Length: > 0 } value
        && !char.IsDigit(value[0])
        && Enum.TryParse<UserRole>(value, ignoreCase: false, out var role)
        && Enum.IsDefined(role)
            ? role
            : null;

    /// <summary>
    /// The connection's remote address, normalised so it always fits the IPv6 text limit: an IPv4-mapped
    /// IPv6 address becomes plain IPv4, and the IPv6 zone id is dropped. Behind a reverse proxy, configure
    /// forwarded headers so this is the client's address and not the proxy's.
    /// </summary>
    public string? IpAddress
    {
        get
        {
            var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
            if (address is null)
            {
                return null;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4().ToString();
            }

            // ScopeId exists only for IPv6; reading it on an IPv4 address throws (SocketException 95).
            // Plain IPv4 is normal behind a reverse proxy, where the address comes from X-Forwarded-For.
            if (address.AddressFamily != AddressFamily.InterNetworkV6 || address.ScopeId == 0)
            {
                return address.ToString();
            }

            return new IPAddress(address.GetAddressBytes()).ToString();
        }
    }
}
