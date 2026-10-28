using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InventoryCashMigrationTests
{
    [RoomLockPostgresFact]
    public async Task Additive_migration_preserves_legacy_expense_and_is_repeatable()
    {
        var source = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION"));
        if (source.Host is not ("127.0.0.1" or "localhost")) throw new InvalidOperationException("Migration rehearsal requires a local test PostgreSQL.");
        var name = "delong_upgrade_" + Guid.NewGuid().ToString("N");
        var adminConnection = new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection.ConnectionString); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin).ExecuteNonQueryAsync();
        try {
            var testConnection = new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false };
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(testConnection.ConnectionString).Options);
            var migrations = db.Database.GetMigrations().ToArray();
            var addedIndex = Array.FindIndex(migrations, x => x.EndsWith("AddSharedInventoryAndCash", StringComparison.Ordinal));
            Assert.True(addedIndex > 0);
            await db.GetService<IMigrator>().MigrateAsync(migrations[addedIndex - 1]);
            var property = new Property { Code = "OLD", Name = "Legacy property" };
            db.Properties.Add(property); await db.SaveChangesAsync();
            var id = Guid.NewGuid(); var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO expenses(id, property_id, occurred_at_utc, category, description, amount, method, is_voided, created_at_utc, updated_at_utc) VALUES ({id}, {property.Id}, {now}, 'Legacy', 'Legacy cash expense', 123.45, 'Cash', false, {now}, {now})");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            var row = await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(123.45m, row.Amount); Assert.Null(row.RoomId); Assert.Null(row.RequestKey); Assert.False(row.IsVoided);
            Assert.False(db.Database.HasPendingModelChanges());
        } finally { await new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin).ExecuteNonQueryAsync(); }
    }
}
