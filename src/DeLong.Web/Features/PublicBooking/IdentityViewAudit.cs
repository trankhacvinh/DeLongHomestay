using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

/// <summary>
/// Viewing a CCCD is a personal-data access (Nghị định 13/2023): keep a trail of who looked.
/// One entry per user/booking/side per 10 minutes, since the viewer reloads the image.
/// </summary>
public static class IdentityViewAudit
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static async Task<bool> RecordAsync(AppDbContext db, Guid propertyId, Guid bookingId, Guid? viewer, string side,
        DateTime utcNow, CancellationToken cancellationToken)
    {
        var recent = utcNow - Window;
        var sideKey = side.Trim().ToLowerInvariant();
        // AfterJson is a jsonb column: PostgreSQL has no LIKE on jsonb (42883), so match the side in memory.
        var recentViews = await db.AuditLogs.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.EntityType == "Booking" &&
                x.EntityId == bookingId && x.Action == "IdentityViewed" && x.ActorUserId == viewer &&
                x.CreatedAtUtc >= recent && x.AfterJson != null)
            .Select(x => x.AfterJson!)
            .ToListAsync(cancellationToken);
        if (recentViews.Any(json => json.Contains($"\"{sideKey}\"", StringComparison.Ordinal)))
            return false;
        new AuditService(db).Add(propertyId, "Booking", bookingId, "IdentityViewed", viewer, after: new { Side = sideKey });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
