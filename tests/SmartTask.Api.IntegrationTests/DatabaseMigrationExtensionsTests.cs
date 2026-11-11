using Microsoft.Extensions.Configuration;
using Shouldly;
using SmartTask.Api.Extensions;

namespace SmartTask.Api.IntegrationTests;

public sealed class DatabaseMigrationExtensionsTests
{
    [Theory]
    [InlineData(null, null, false)]      // plain dotnet run: never migrate by surprise
    [InlineData(null, "true", true)]     // official .NET container image
    [InlineData(null, "false", false)]
    [InlineData("true", null, true)]     // explicit opt-in outside a container
    [InlineData("false", "true", false)] // explicit opt-out wins inside a container
    [InlineData("true", "false", true)]
    public void ShouldApplyMigrations_ExplicitSettingWins_OtherwiseOnlyInContainer(string? setting, string? inContainer, bool expected)
    {
        var values = new Dictionary<string, string?>();
        if (setting is not null)
        {
            values[DatabaseMigrationExtensions.ApplyMigrationsOnStartupKey] = setting;
        }

        if (inContainer is not null)
        {
            values[DatabaseMigrationExtensions.RunningInContainerKey] = inContainer;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        DatabaseMigrationExtensions.ShouldApplyMigrations(configuration).ShouldBe(expected);
    }
}
