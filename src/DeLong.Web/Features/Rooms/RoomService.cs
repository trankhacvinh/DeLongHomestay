using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Rooms;

public sealed class RoomService(AppDbContext db)
{
    public async Task<IReadOnlyList<RoomDto>> GetAllAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        return await db.Rooms
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new RoomDto(
                x.Id,
                x.PropertyId,
                x.Code,
                x.Name,
                x.Capacity,
                x.SortOrder,
                x.IsActive,
                x.Rates.OrderBy(r => r.SortOrder).Select(r => new RoomRateDto(
                    r.Id,
                    r.Name,
                    r.StartTime.ToString("HH:mm"),
                    r.EndTime.ToString("HH:mm"),
                    r.IsOvernight,
                    r.Price,
                    r.IsActive,
                    r.SortOrder)).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomDto?> GetAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken = default)
    {
        return await db.Rooms
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == roomId)
            .Select(x => new RoomDto(
                x.Id,
                x.PropertyId,
                x.Code,
                x.Name,
                x.Capacity,
                x.SortOrder,
                x.IsActive,
                x.Rates.OrderBy(r => r.SortOrder).Select(r => new RoomRateDto(
                    r.Id,
                    r.Name,
                    r.StartTime.ToString("HH:mm"),
                    r.EndTime.ToString("HH:mm"),
                    r.IsOvernight,
                    r.Price,
                    r.IsActive,
                    r.SortOrder)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(RoomDto? Room, string? Error)> CreateAsync(
        Guid propertyId, CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request.Code, request.Name, request.Capacity);
        if (validationError is not null) return (null, validationError);

        var propertyExists = await db.Properties.AnyAsync(x => x.Id == propertyId && x.IsActive, cancellationToken);
        if (!propertyExists) return (null, "Cơ sở không tồn tại hoặc đã ngừng hoạt động.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var duplicate = await db.Rooms.AnyAsync(
            x => x.PropertyId == propertyId && x.Code == normalizedCode, cancellationToken);
        if (duplicate) return (null, "Mã phòng đã tồn tại trong cơ sở này.");

        var room = new Room
        {
            PropertyId = propertyId,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Capacity = request.Capacity,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, room.Id, cancellationToken), null);
    }

    public async Task<(RoomDto? Room, string? Error)> UpdateAsync(
        Guid propertyId, Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request.Code, request.Name, request.Capacity);
        if (validationError is not null) return (null, validationError);

        var room = await db.Rooms.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return (null, "Không tìm thấy phòng.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var duplicate = await db.Rooms.AnyAsync(
            x => x.PropertyId == propertyId && x.Code == normalizedCode && x.Id != roomId, cancellationToken);
        if (duplicate) return (null, "Mã phòng đã tồn tại trong cơ sở này.");

        room.Code = normalizedCode;
        room.Name = request.Name.Trim();
        room.Capacity = request.Capacity;
        room.SortOrder = request.SortOrder;
        room.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, roomId, cancellationToken), null);
    }

    public async Task<bool> ArchiveAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return false;

        room.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? Validate(string code, string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Mã phòng là bắt buộc.";
        if (code.Trim().Length > 50) return "Mã phòng tối đa 50 ký tự.";
        if (string.IsNullOrWhiteSpace(name)) return "Tên phòng là bắt buộc.";
        if (name.Trim().Length > 200) return "Tên phòng tối đa 200 ký tự.";
        if (capacity is < 1 or > 50) return "Sức chứa phải từ 1 đến 50 người.";
        return null;
    }
}
