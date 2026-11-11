using System.Net;
using Microsoft.Extensions.Configuration;
using Shouldly;
using SmartTask.Api.Extensions;

namespace SmartTask.Api.IntegrationTests;

public sealed class ForwardedHeadersExtensionsTests
{
    private static IConfiguration Config(params string[] proxies) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(proxies.Select((p, i) =>
                new KeyValuePair<string, string?>($"{ForwardedHeadersExtensions.KnownProxiesKey}:{i}", p)))
            .Build();

    [Fact]
    public void ReadKnownProxies_returns_nothing_when_not_configured() =>
        ForwardedHeadersExtensions.ReadKnownProxies(Config()).ShouldBeEmpty();

    [Fact]
    public void ReadKnownProxies_parses_addresses_and_skips_blanks() =>
        ForwardedHeadersExtensions.ReadKnownProxies(Config("172.31.250.10", " ", "::1"))
            .ShouldBe([IPAddress.Parse("172.31.250.10"), IPAddress.IPv6Loopback]);

    [Fact]
    public void ReadKnownProxies_rejects_a_value_that_is_not_an_ip_address() =>
        Should.Throw<InvalidOperationException>(() => ForwardedHeadersExtensions.ReadKnownProxies(Config("web")))
            .Message.ShouldContain("'web'");
}
