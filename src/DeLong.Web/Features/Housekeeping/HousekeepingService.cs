using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Rooms;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DeLong.Web.Features.Housekeeping;

public sealed class HousekeepingService(AppDbContext db, IRoomImageStorage? imageStorage = null)
{
    private static readonly BookingStatus[] ScheduleStatuses =
        [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed];
    private static readonly (string Name, string Category)[] DefaultConditionTags =
    [
        ("Phòng sạch", "Vệ sinh"),
        ("Đã thay ga", "Vệ sinh"),
        ("Đã bổ sung khăn", "Đồ dùng"),
        ("Thiếu đồ dùng", "Đồ dùng"),
        ("Thiết bị hoạt động tốt", "Trang thiết bị"),
        ("Hỏng đèn", "Hư hỏng"),
        ("Có mùi", "Vệ sinh"),
        ("Cần bảo trì", "Hư hỏng")
    ];

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

    public async Task<HousekeepingSettingsDto?> GetSettingsAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default) =>
        await db.Properties.AsNoTracking()
            .Where(x => x.Id == propertyId && x.IsActive)
            .Select(x => new HousekeepingSettingsDto(
                x.HousekeepingBeforeCheckInMinutes,
                x.HousekeepingAfterCheckOutMinutes))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(HousekeepingSettingsDto? Settings, string? Error)> SaveSettingsAsync(
        Guid propertyId,
        UpdateHousekeepingSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BeforeCheckInMinutes is < 0 or > 1440 || request.AfterCheckOutMinutes is < 0 or > 1440)
            return (null, "Số phút phải nằm trong khoảng 0–1440.");

        var property = await db.Properties.SingleOrDefaultAsync(
            x => x.Id == propertyId && x.IsActive,
            cancellationToken);
        if (property is null) return (null, "Không tìm thấy cơ sở.");

        property.HousekeepingBeforeCheckInMinutes = request.BeforeCheckInMinutes;
        property.HousekeepingAfterCheckOutMinutes = request.AfterCheckOutMinutes;
        await db.SaveChangesAsync(cancellationToken);
        return (new HousekeepingSettingsDto(
            property.HousekeepingBeforeCheckInMinutes,
            property.HousekeepingAfterCheckOutMinutes), null);
    }

    public async Task<HousekeepingScheduleDto?> GetScheduleAsync(
        Guid propertyId,
        DateOnly from,
        int days,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 7);
        var propertySettings = await db.Properties.AsNoTracking()
            .Where(x => x.Id == propertyId && x.IsActive)
            .Select(x => new
            {
                x.TimeZoneId,
                x.HousekeepingBeforeCheckInMinutes,
                x.HousekeepingAfterCheckOutMinutes
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (propertySettings is null || string.IsNullOrWhiteSpace(propertySettings.TimeZoneId)) return null;

        var timeZoneId = propertySettings.TimeZoneId;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var startUtc = ToUtc(from.ToDateTime(TimeOnly.MinValue), timeZone);
        var endUtc = ToUtc(from.AddDays(days).ToDateTime(TimeOnly.MinValue), timeZone);
        var beforeMinutes = propertySettings.HousekeepingBeforeCheckInMinutes;
        var afterMinutes = propertySettings.HousekeepingAfterCheckOutMinutes;
        var queryStartUtc = startUtc.AddMinutes(-afterMinutes);
        var queryEndUtc = endUtc.AddMinutes(Math.Max(beforeMinutes, 240));

        var rows = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId &&
                        ScheduleStatuses.Contains(x.Status) &&
                        x.CheckInUtc < queryEndUtc &&
                        queryStartUtc < x.CheckOutUtc)
            .Select(x => new ScheduleBookingRow(
                x.Id,
                x.Code,
                x.RoomId,
                x.Room.Code,
                x.Room.Name,
                x.Room.SortOrder,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.Status))
            .ToListAsync(cancellationToken);

        var daysResult = new List<HousekeepingScheduleDayDto>(days);
        for (var offset = 0; offset < days; offset++)
        {
            var date = from.AddDays(offset);
            var tasks = new List<HousekeepingScheduleTaskDto>();

            foreach (var row in rows)
            {
                var prepareAtUtc = row.CheckInUtc.AddMinutes(-beforeMinutes);
                var checkInLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(prepareAtUtc, DateTimeKind.Utc), timeZone);
                if (row.Status != BookingStatus.Completed && DateOnly.FromDateTime(checkInLocal) == date)
                {
                    tasks.Add(ToTask(row, prepareAtUtc, "prepare", "Mở đèn", $"dọn phòng {row.RoomName} mở đèn"));
                }

                var turnoverAtUtc = row.CheckOutUtc.AddMinutes(afterMinutes);
                var checkOutLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(turnoverAtUtc, DateTimeKind.Utc), timeZone);
                if (DateOnly.FromDateTime(checkOutLocal) != date) continue;

                var nextBooking = rows
                    .Where(x => x.RoomId == row.RoomId && x.Id != row.Id && x.Status != BookingStatus.Completed && x.CheckInUtc >= row.CheckOutUtc)
                    .OrderBy(x => x.CheckInUtc)
                    .FirstOrDefault();
                var keepLightsOn = nextBooking is not null && nextBooking.CheckInUtc <= row.CheckOutUtc.AddHours(4);
                tasks.Add(ToTask(
                    row,
                    turnoverAtUtc,
                    "turnover",
                    keepLightsOn ? "Giữ mở đèn" : "Tắt đèn",
                    keepLightsOn
                        ? $"dọn phòng {row.RoomName} giữ mở đèn"
                        : $"dọn phòng {row.RoomName} tắt đèn"));
            }

            daysResult.Add(new HousekeepingScheduleDayDto(
                date,
                tasks.OrderBy(x => x.AtUtc)
                    .ThenBy(x => rows.First(row => row.Id == x.BookingId).RoomSortOrder)
                    .ThenBy(x => x.RoomName)
                    .ToList()));
        }

        return new HousekeepingScheduleDto(
            from,
            days,
            timeZoneId,
            new HousekeepingSettingsDto(beforeMinutes, afterMinutes),
            daysResult);
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

    public async Task<IReadOnlyList<RoomConditionTagDto>> GetConditionTagsAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultConditionTagsAsync(propertyId, cancellationToken);
        return await db.RoomConditionTags.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new RoomConditionTagDto(x.Id, x.Name, x.Category))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomConditionReportDto>> GetConditionReportsAsync(
        Guid propertyId,
        Guid? roomId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = db.RoomConditionReports.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && (!roomId.HasValue || x.RoomId == roomId.Value));

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new
            {
                Report = x,
                x.Room.Code,
                x.Room.Name,
                Reporter = db.Users.Where(user => user.Id == x.ReportedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                Images = x.Images.OrderBy(image => image.SortOrder).Select(image => new RoomConditionReportImageDto(
                    image.Id,
                    image.LargePath,
                    image.ThumbnailPath,
                    image.Width,
                    image.Height)).ToList()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new RoomConditionReportDto(
            x.Report.Id,
            x.Report.RoomId,
            x.Code,
            x.Name,
            x.Report.InspectionType,
            x.Report.Severity,
            x.Report.Status,
            x.Report.Content,
            DeserializeTags(x.Report.TagsJson),
            string.IsNullOrWhiteSpace(x.Reporter) ? "Nhân viên" : x.Reporter,
            x.Report.CreatedAtUtc,
            x.Images)).ToList();
    }

    public async Task<(RoomConditionReportDto? Report, string? Error)> CreateConditionReportAsync(
        Guid propertyId,
        Guid roomId,
        Guid actorUserId,
        RoomInspectionType inspectionType,
        RoomConditionSeverity severity,
        string? content,
        IReadOnlyList<string> selectedTags,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == roomId && x.PropertyId == propertyId && x.IsActive,
            cancellationToken);
        if (room is null) return (null, "Không tìm thấy phòng trong cơ sở đang chọn.");
        if (imageStorage is null) return (null, "Dịch vụ lưu ảnh chưa sẵn sàng.");
        if (files.Count is < 1 or > 12) return (null, "Mỗi báo cáo cần từ 1 đến 12 ảnh.");

        var normalizedTags = selectedTags
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        var normalizedContent = content?.Trim() ?? string.Empty;
        if (normalizedContent.Length > 4000) return (null, "Nội dung tối đa 4.000 ký tự.");
        if (normalizedContent.Length == 0 && normalizedTags.Length == 0)
            return (null, "Hãy nhập nội dung hoặc chọn ít nhất một tag tình trạng.");

        var allowedTags = await db.RoomConditionTags.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.IsActive)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
        normalizedTags = normalizedTags
            .Where(tag => allowedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var report = new RoomConditionReport
        {
            PropertyId = propertyId,
            RoomId = roomId,
            ReportedByUserId = actorUserId,
            InspectionType = inspectionType,
            Severity = severity,
            Status = RoomConditionReportStatus.New,
            Content = normalizedContent,
            TagsJson = JsonSerializer.Serialize(normalizedTags)
        };

        var storedImages = new List<StoredRoomImage>();
        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                var imageId = Guid.CreateVersion7();
                var (stored, error) = await imageStorage.SaveAsync(roomId, imageId, files[index], cancellationToken);
                if (stored is null)
                {
                    foreach (var saved in storedImages)
                        await imageStorage.DeleteAsync(saved, CancellationToken.None);
                    return (null, $"Ảnh {index + 1}: {error}");
                }
                storedImages.Add(stored);
                report.Images.Add(new RoomConditionReportImage
                {
                    Id = imageId,
                    OriginalFileName = stored.OriginalFileName,
                    OriginalStoragePath = stored.OriginalStoragePath,
                    LargePath = stored.LargeUrl,
                    CardPath = stored.CardUrl,
                    ThumbnailPath = stored.ThumbnailUrl,
                    ContentType = stored.ContentType,
                    OriginalBytes = stored.OriginalBytes,
                    Width = stored.Width,
                    Height = stored.Height,
                    SortOrder = index
                });
            }

            db.RoomConditionReports.Add(report);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var stored in storedImages)
                await imageStorage.DeleteAsync(stored, CancellationToken.None);
            throw;
        }

        var created = (await GetConditionReportsAsync(propertyId, roomId, 20, cancellationToken))
            .Single(x => x.Id == report.Id);
        return (created, null);
    }

    public async Task<(RoomConditionTagDto? Tag, string? Error)> CreateConditionTagAsync(
        Guid propertyId,
        CreateRoomConditionTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var category = request.Category?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120) return (null, "Tên tag phải từ 2 đến 120 ký tự.");
        if (category.Length is < 2 or > 80) return (null, "Nhóm tag phải từ 2 đến 80 ký tự.");
        var normalizedName = name.ToUpperInvariant();
        if (await db.RoomConditionTags.AnyAsync(x => x.PropertyId == propertyId && x.NormalizedName == normalizedName, cancellationToken))
            return (null, "Tag này đã tồn tại.");

        var tag = new RoomConditionTag
        {
            PropertyId = propertyId,
            Name = name,
            NormalizedName = normalizedName,
            Category = category,
            SortOrder = await db.RoomConditionTags.CountAsync(x => x.PropertyId == propertyId, cancellationToken)
        };
        db.RoomConditionTags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        return (new RoomConditionTagDto(tag.Id, tag.Name, tag.Category), null);
    }

    public async Task<RoomConditionReportDto?> ChangeConditionReportStatusAsync(
        Guid propertyId,
        Guid reportId,
        RoomConditionReportStatus status,
        CancellationToken cancellationToken = default)
    {
        var report = await db.RoomConditionReports.SingleOrDefaultAsync(
            x => x.Id == reportId && x.PropertyId == propertyId,
            cancellationToken);
        if (report is null) return null;
        report.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return (await GetConditionReportsAsync(propertyId, report.RoomId, 100, cancellationToken))
            .Single(x => x.Id == reportId);
    }

    private async Task EnsureDefaultConditionTagsAsync(Guid propertyId, CancellationToken cancellationToken)
    {
        if (await db.RoomConditionTags.AnyAsync(x => x.PropertyId == propertyId, cancellationToken)) return;
        for (var index = 0; index < DefaultConditionTags.Length; index++)
        {
            var item = DefaultConditionTags[index];
            db.RoomConditionTags.Add(new RoomConditionTag
            {
                PropertyId = propertyId,
                Name = item.Name,
                NormalizedName = item.Name.ToUpperInvariant(),
                Category = item.Category,
                SortOrder = index
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<string> DeserializeTags(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static HousekeepingScheduleTaskDto ToTask(
        ScheduleBookingRow row,
        DateTime atUtc,
        string kind,
        string action,
        string text) =>
        new(row.Id, row.Code, row.RoomId, row.RoomCode, row.RoomName, atUtc, kind, action, text);

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);

    private sealed record ScheduleBookingRow(
        Guid Id,
        string Code,
        Guid RoomId,
        string RoomCode,
        string RoomName,
        int RoomSortOrder,
        DateTime CheckInUtc,
        DateTime CheckOutUtc,
        BookingStatus Status);
}
