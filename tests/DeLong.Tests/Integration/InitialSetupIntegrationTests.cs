using DeLong.Web.Data;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Setup;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InitialSetupIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task First_setup_seeds_safe_DeLong_configuration_and_second_setup_is_rejected()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var schema = $"setup_{Guid.NewGuid():N}";
        await CreateSchemaAsync(baseConnectionString, schema);
        try
        {
            await using var provider = CreateProvider(baseConnectionString, schema);
            await using (var migrationScope = provider.CreateAsyncScope())
                await migrationScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

            Guid adminId;
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var setup = setupScope.ServiceProvider.GetRequiredService<InitialSetupService>();
                Assert.True(await setup.IsRequiredAsync());

                var result = await setup.CompleteAsync(new InitialSetupRequest(
                    "QA Setup Admin",
                    $"setup-{Guid.NewGuid():N}@localhost.test",
                    "Setup-QA-123!",
                    true));

                Assert.True(result.Succeeded, result.Error);
                adminId = result.AdminUserId!.Value;
            }

            await using (var verificationScope = provider.CreateAsyncScope())
            {
                var db = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var setup = verificationScope.ServiceProvider.GetRequiredService<InitialSetupService>();

                Assert.False(await setup.IsRequiredAsync());
                Assert.Equal(6, await db.Rooms.CountAsync(x => x.PropertyId == DbSeeder.DeLongPropertyId));
                Assert.True(await db.PropertyPricingSettings.AnyAsync(x => x.PropertyId == DbSeeder.DeLongPropertyId));
                Assert.True(await db.PropertyPay2SSettings.AnyAsync(x => x.PropertyId == DbSeeder.DeLongPropertyId && !x.Enabled));
                Assert.True(await db.PropertyNotificationSettings.AnyAsync(x => x.PropertyId == DbSeeder.DeLongPropertyId));
                Assert.True(await db.CustomerAccountSettings.AnyAsync(x => x.PropertyId == DbSeeder.DeLongPropertyId));
                var ai = await db.PropertyAiProfiles.SingleAsync(x => x.PropertyId == DbSeeder.DeLongPropertyId);
                Assert.False(ai.IsEnabled);
                Assert.False(ai.IsPublicAiEnabled);
                Assert.Equal(string.Empty, ai.ProtectedApiKey);
                Assert.True(await db.UserPropertyAccesses.AnyAsync(x => x.UserId == adminId && x.PropertyId == DbSeeder.DeLongPropertyId));

                var second = await setup.CompleteAsync(new InitialSetupRequest(
                    "Second Admin", "second@localhost.test", "Setup-QA-456!", false));
                Assert.False(second.Succeeded);
                Assert.Equal(1, await db.UserRoles.Join(db.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.NormalizedName).CountAsync(name => name == "ADMIN"));
            }
        }
        finally
        {
            await DropSchemaAsync(baseConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Setup_without_seed_creates_only_the_administrator()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var schema = $"setup_{Guid.NewGuid():N}";
        await CreateSchemaAsync(baseConnectionString, schema);
        try
        {
            await using var provider = CreateProvider(baseConnectionString, schema);
            await using (var migrationScope = provider.CreateAsyncScope())
                await migrationScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

            await using var setupScope = provider.CreateAsyncScope();
            var result = await setupScope.ServiceProvider.GetRequiredService<InitialSetupService>().CompleteAsync(
                new InitialSetupRequest("Empty Setup Admin", "empty@localhost.test", "Setup-QA-789!", false));
            Assert.True(result.Succeeded, result.Error);

            var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.Properties.AsNoTracking().ToListAsync());
            Assert.Empty(await db.PropertyAiProfiles.AsNoTracking().ToListAsync());
        }
        finally
        {
            await DropSchemaAsync(baseConnectionString, schema);
        }
    }

    private static ServiceProvider CreateProvider(string baseConnectionString, string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.ConnectionString));
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<InitialSetupService>();
        return services.BuildServiceProvider();
    }

    private static async Task CreateSchemaAsync(string connectionString, string schema)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropSchemaAsync(string connectionString, string schema)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }
}
