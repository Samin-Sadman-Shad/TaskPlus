using System.Net;
using Microsoft.AspNetCore.Http;
using Shouldly;
using SmartTask.Api.Identity;

namespace SmartTask.Api.IntegrationTests;

public sealed class CurrentUserServiceTests
{
    private static string? IpOf(IPAddress? remote)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remote;
        return new CurrentUserService(new HttpContextAccessor { HttpContext = context }).IpAddress;
    }

    [Fact]
    public void IpAddress_is_null_without_a_remote_address() => IpOf(null).ShouldBeNull();

    // Behind a reverse proxy the address comes from X-Forwarded-For and is plain IPv4.
    // Reading IPAddress.ScopeId on it used to throw (SocketException 95) and fail every login.
    [Fact]
    public void IpAddress_handles_plain_ipv4() => IpOf(IPAddress.Parse("172.31.250.1")).ShouldBe("172.31.250.1");

    [Fact]
    public void IpAddress_unmaps_ipv4_mapped_ipv6() => IpOf(IPAddress.Parse("::ffff:10.0.0.5")).ShouldBe("10.0.0.5");

    [Fact]
    public void IpAddress_keeps_plain_ipv6() => IpOf(IPAddress.Parse("2001:db8::1")).ShouldBe("2001:db8::1");

    [Fact]
    public void IpAddress_drops_the_ipv6_zone_id() => IpOf(IPAddress.Parse("fe80::1%12")).ShouldBe("fe80::1");
}
