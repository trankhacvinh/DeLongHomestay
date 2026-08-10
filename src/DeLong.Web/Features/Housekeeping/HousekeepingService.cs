using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Housekeeping;

public sealed class HousekeepingService(AppDbContext db)
{
    public async Task<IReadOnlyList<HousekeepingRoomDto>> GetAllAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return await db.Rooms
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new HousekeepingRoomDto(
                x.Id,
                x.Code,
                x.Name,
                x.HousekeepingStatus,
                x.HousekeepingUpdatedAtUtc,
                x.HousekeepingUpdatedByUserId))
            .ToListAsync(cancellationToken);
    }

    public async Task<HousekeepingRoomDto?> ChangeStatusAsync(
        Guid propertyId,
        Guid roomId,
        HousekeepingStatus status,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == roomId && x.IsActive,
            cancellationToken);
        if (room is null) return null;

        room.HousekeepingStatus = status;
        room.HousekeepingUpdatedAtUtc = DateTime.UtcNow;
        room.HousekeepingUpdatedByUserId = actorUserId;
        await db.SaveChangesAsync(cancellationToken);

        return new HousekeepingRoomDto(
            room.Id,
            room.Code,
            room.Name,
            room.HousekeepingStatus,
            room.HousekeepingUpdatedAtUtc,
            room.HousekeepingUpdatedByUserId);
    }
}
