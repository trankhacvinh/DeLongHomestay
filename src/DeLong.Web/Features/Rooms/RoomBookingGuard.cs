using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DeLong.Web.Features.Rooms;

// Share this row lock between booking admission and room lock/unlock. Existing transactions
// (for example voucher reservations) retain ownership and keep the lock until their commit.
public sealed class RoomBookingGuard(Room? room, IDbContextTransaction? transaction) : IAsyncDisposable
{
    public const string ErrorCode = "room_booking_locked";
    public const string ErrorMessage = "Phòng đang tạm ngừng nhận đặt phòng. Vui lòng chọn phòng khác.";
    public Room? Room { get; } = room;
    private bool disposed;

    public static async Task<RoomBookingGuard> AcquireAsync(
        AppDbContext db, Guid propertyId, Guid roomId, CancellationToken cancellationToken)
    {
        var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var query = db.Database.IsNpgsql()
                ? db.Rooms.FromSqlInterpolated($"SELECT * FROM rooms WHERE property_id = {propertyId} AND id = {roomId} FOR UPDATE")
                : db.Rooms.Where(x => x.PropertyId == propertyId && x.Id == roomId);
            return new(await query.AsNoTracking().SingleOrDefaultAsync(cancellationToken), transaction);
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (transaction is null) return;
        await transaction.CommitAsync(cancellationToken);
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (transaction is null || disposed) return;
        disposed = true;
        await transaction.DisposeAsync();
    }
}
