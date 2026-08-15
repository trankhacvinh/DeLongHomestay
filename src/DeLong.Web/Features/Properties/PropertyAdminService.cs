using System.Security.Claims;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Properties;

public sealed record PropertyAdminDto(
    Guid Id,
    string Code,
    string Name,
    string TimeZoneId,
    string SiteSlug,
    bool IsActive,
    int RoomCount,
    int UserCount);

public sealed class SavePropertyRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = "Asia/Ho_Chi_Minh";
    public bool IsActive { get; init; } = true;
}

public sealed record PropertyAdminError(string Code, string Message);

public sealed class PropertyAdminService(AppDbContext db)
{
    private static readonly BookingStatus[] LockingStatuses =
        [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<IReadOnlyList<PropertyAdminDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await db.Properties.AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.TimeZoneId,
                x.IsActive,
                RoomCount = db.Rooms.Count(r => r.PropertyId == x.Id),
                UserCount = db.UserPropertyAccesses.Count(a => a.PropertyId == x.Id)
            })
            .ToListAsync(ct);

        return items.Select(x => new PropertyAdminDto(
            x.Id,
            x.Code,
            x.Name,
            x.TimeZoneId,
            PublicPropertyResolver.ToSiteSlug(x.Code),
            x.IsActive,
            x.RoomCount,
            x.UserCount)).ToList();
    }

    public async Task<(PropertyAdminDto? Property, PropertyAdminError? Error)> CreateAsync(
        SavePropertyRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is not null) return (null, validation);

        var code = NormalizeCode(request.Code);
        if (await db.Properties.AnyAsync(x => x.Code == code, ct))
            return (null, new("duplicate_code", "Mã cơ sở đã tồn tại."));

        var siteSlug = PublicPropertyResolver.ToSiteSlug(code);
        var activeCodes = await db.Properties.AsNoTracking().Select(x => x.Code).ToListAsync(ct);
        if (activeCodes.Any(existing => string.Equals(PublicPropertyResolver.ToSiteSlug(existing), siteSlug, StringComparison.OrdinalIgnoreCase)))
            return (null, new("duplicate_public_route", "Đường dẫn public sinh từ mã cơ sở đã được sử dụng. Hãy chọn mã cơ sở khác."));

        var property = new Property
        {
            Code = code,
            Name = request.Name.Trim(),
            TimeZoneId = request.TimeZoneId.Trim(),
            IsActive = request.IsActive
        };
        db.Properties.Add(property);

        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdValue, out var userId))
        {
            db.UserPropertyAccesses.Add(new UserPropertyAccess
            {
                UserId = userId,
                PropertyId = property.Id
            });
        }

        db.Set<PropertySiteSettings>().Add(new PropertySiteSettings
        {
            PropertyId = property.Id,
            SiteName = property.Name,
            RobotsIndex = true
        });

        await db.SaveChangesAsync(ct);
        return (new(property.Id, property.Code, property.Name, property.TimeZoneId, siteSlug, property.IsActive, 0, 1), null);
    }

    public async Task<(PropertyAdminDto? Property, PropertyAdminError? Error)> UpdateAsync(
        Guid propertyId,
        SavePropertyRequest request,
        CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is not null) return (null, validation);

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == propertyId, ct);
        if (property is null) return (null, new("not_found", "Không tìm thấy cơ sở."));

        var code = NormalizeCode(request.Code);
        if (!string.Equals(property.Code, code, StringComparison.Ordinal))
            return (null, new("code_immutable", "Mã cơ sở không thể đổi sau khi tạo vì đang dùng làm định danh ổn định cho đường dẫn public."));

        if (property.IsActive && !request.IsActive)
        {
            var hasLockingBookings = await db.Bookings.AsNoTracking()
                .AnyAsync(x => x.PropertyId == propertyId && LockingStatuses.Contains(x.Status), ct);
            if (hasLockingBookings)
                return (null, new("property_in_use", "Cơ sở còn lượt đặt đang giữ, đã xác nhận hoặc đang ở. Hãy xử lý các lượt này trước khi ngừng cơ sở."));
        }

        property.Name = request.Name.Trim();
        property.TimeZoneId = request.TimeZoneId.Trim();
        property.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        var roomCount = await db.Rooms.CountAsync(x => x.PropertyId == propertyId, ct);
        var userCount = await db.UserPropertyAccesses.CountAsync(x => x.PropertyId == propertyId, ct);
        return (new(
            property.Id,
            property.Code,
            property.Name,
            property.TimeZoneId,
            PublicPropertyResolver.ToSiteSlug(property.Code),
            property.IsActive,
            roomCount,
            userCount), null);
    }

    private static PropertyAdminError? Validate(SavePropertyRequest request)
    {
        var code = NormalizeCode(request.Code);
        if (code.Length is < 2 or > 50)
            return new("validation", "Mã cơ sở phải từ 2 đến 50 ký tự.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return new("validation", "Tên cơ sở là bắt buộc và tối đa 200 ký tự.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId.Trim()); }
        catch { return new("validation", "Múi giờ không hợp lệ trên máy chủ."); }
        return null;
    }

    private static string NormalizeCode(string value) =>
        new((value ?? string.Empty).Trim().ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
}
