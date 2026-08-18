using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Rooms;

public sealed class RoomService(AppDbContext db)
{
    public async Task<IReadOnlyList<RoomDto>> GetAllAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        return await db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new RoomDto(x.Id, x.PropertyId, x.Code, x.Name, x.Capacity, x.SortOrder, x.IsActive, x.IsPublished,
                x.HousekeepingStatus, x.HousekeepingUpdatedAtUtc,
                x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).Select(i => i.ThumbnailPath).FirstOrDefault(),
                x.Images.Count,
                x.Rates.OrderBy(r => r.SortOrder).Select(r => new RoomRateDto(r.Id, r.Name,
                    r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"), r.Type, r.IsOvernight,
                    r.Price, r.IsActive, r.SortOrder)).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomDto?> GetAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken = default)
    {
        return await db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId && x.Id == roomId)
            .Select(x => new RoomDto(x.Id, x.PropertyId, x.Code, x.Name, x.Capacity, x.SortOrder, x.IsActive, x.IsPublished,
                x.HousekeepingStatus, x.HousekeepingUpdatedAtUtc,
                x.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).Select(i => i.ThumbnailPath).FirstOrDefault(),
                x.Images.Count,
                x.Rates.OrderBy(r => r.SortOrder).Select(r => new RoomRateDto(r.Id, r.Name,
                    r.StartTime.ToString("HH:mm"), r.EndTime.ToString("HH:mm"), r.Type, r.IsOvernight,
                    r.Price, r.IsActive, r.SortOrder)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(RoomDto? Room, string? Error)> CreateAsync(Guid propertyId, CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request.Code, request.Name, request.Capacity);
        if (validationError is not null) return (null, validationError);
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId && x.IsActive, cancellationToken)) return (null, "Cơ sở không tồn tại hoặc đã ngừng hoạt động.");
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Code == normalizedCode, cancellationToken)) return (null, "Mã phòng đã tồn tại trong cơ sở này.");

        var name = request.Name.Trim();
        var slug = await CreateUniqueSlugAsync(propertyId, name, normalizedCode, null, cancellationToken);
        var room = new Room
        {
            PropertyId = propertyId,
            Code = normalizedCode,
            Name = name,
            Slug = slug,
            Capacity = request.Capacity,
            SortOrder = request.SortOrder,
            IsActive = true,
            IsPublished = false
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, room.Id, cancellationToken), null);
    }

    public async Task<(RoomDto? Room, string? Error)> UpdateAsync(Guid propertyId, Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request.Code, request.Name, request.Capacity);
        if (validationError is not null) return (null, validationError);
        var room = await db.Rooms.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return (null, "Không tìm thấy phòng.");
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Code == normalizedCode && x.Id != roomId, cancellationToken)) return (null, "Mã phòng đã tồn tại trong cơ sở này.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(room.Slug))
            room.Slug = await CreateUniqueSlugAsync(propertyId, name, normalizedCode, roomId, cancellationToken);

        room.Code = normalizedCode;
        room.Name = name;
        room.Capacity = request.Capacity;
        room.SortOrder = request.SortOrder;
        room.IsActive = request.IsActive;
        if (request.IsPublished.HasValue) room.IsPublished = request.IsPublished.Value;
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, roomId, cancellationToken), null);
    }

    public async Task<bool> ArchiveAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == roomId, cancellationToken);
        if (room is null) return false;
        room.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> CreateUniqueSlugAsync(
        Guid propertyId,
        string name,
        string code,
        Guid? excludeRoomId,
        CancellationToken cancellationToken)
    {
        var baseSlug = RoomContentService.CreateSlug(name);
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = RoomContentService.CreateSlug(code);
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = code.ToLowerInvariant();

        var candidate = baseSlug;
        var suffixNumber = 2;
        while (await SlugExistsAsync(propertyId, candidate, excludeRoomId, cancellationToken))
        {
            var suffix = $"-{suffixNumber++}";
            var maxBaseLength = Math.Max(1, 180 - suffix.Length);
            var trimmedBase = baseSlug.Length > maxBaseLength ? baseSlug[..maxBaseLength].TrimEnd('-') : baseSlug;
            candidate = $"{trimmedBase}{suffix}";
        }

        return candidate;
    }

    private Task<bool> SlugExistsAsync(Guid propertyId, string slug, Guid? excludeRoomId, CancellationToken cancellationToken) =>
        excludeRoomId.HasValue
            ? db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Id != excludeRoomId.Value && x.Slug == slug, cancellationToken)
            : db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Slug == slug, cancellationToken);

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
