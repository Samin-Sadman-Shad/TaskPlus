using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SmartTask.Api.IntegrationTests;

/// <summary>
/// SQLite has no native <see cref="DateTimeOffset"/> type, so it can't compare or order such columns
/// (for example <c>ExpiresAt &gt; now</c>). For the tests only, every <see cref="DateTimeOffset"/> is
/// stored as a sortable <c>long</c>. SQLite also has no <c>rowversion</c>, so concurrency tokens get a
/// random default on insert. The production SQL Server mapping is untouched.
/// </summary>
internal sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        var properties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?));

        foreach (var property in properties)
        {
            property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }

        var rowVersions = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(byte[]) && p.IsConcurrencyToken);

        foreach (var property in rowVersions)
        {
            property.SetDefaultValueSql("randomblob(8)");
        }
    }
}
