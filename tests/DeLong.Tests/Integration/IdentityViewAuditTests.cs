using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.PublicBooking;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class IdentityViewAuditTests
{
    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Cccd_view_audit_runs_on_real_jsonb_and_dedupes_per_side()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var property = new Property { Code = Guid.NewGuid().ToString("N"), Name = "CCCD audit test" };
        db.Add(property);
        await db.SaveChangesAsync();
        var bookingId = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Regression: after_json is jsonb, a LIKE on it failed with 42883 and every CCCD image returned 500.
        Assert.True(await IdentityViewAudit.RecordAsync(db, property.Id, bookingId, viewer, "front", now, default));
        Assert.False(await IdentityViewAudit.RecordAsync(db, property.Id, bookingId, viewer, " FRONT ", now, default));
        Assert.True(await IdentityViewAudit.RecordAsync(db, property.Id, bookingId, viewer, "back", now, default));
        Assert.True(await IdentityViewAudit.RecordAsync(db, property.Id, bookingId, Guid.NewGuid(), "front", now, default));
        Assert.True(await IdentityViewAudit.RecordAsync(db, property.Id, bookingId, viewer, "front", now.AddMinutes(11), default));

        var sides = await db.AuditLogs.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.EntityId == bookingId && x.Action == "IdentityViewed")
            .Select(x => x.AfterJson!).ToListAsync();
        Assert.Equal(4, sides.Count);
    }
}
