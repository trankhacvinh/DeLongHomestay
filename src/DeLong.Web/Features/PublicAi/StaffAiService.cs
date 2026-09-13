using System.Diagnostics;
using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicAi;

public sealed record StaffAiRequest(string Message, string? ContextPeriod = null);
public sealed record StaffAiTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record StaffAiResponse(string Message, string Period, string PeriodKey, string TimeZone, IReadOnlyList<StaffAiTable> Tables);

public sealed class StaffAiService(AppDbContext db, AiAccessGateway gateway)
{
    private static readonly BookingStatus[] OccupyingStatuses =
        [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<(StaffAiResponse? Value, string? Error)> AskAsync(
        Guid propertyId, Guid userId, string? message, string? contextPeriod, bool canViewFinance, CancellationToken ct)
    {
        var text = message?.Trim() ?? string.Empty;
        if (text.Length is < 2 or > 1000) return (null, "Nội dung phải từ 2 đến 1.000 ký tự.");
        var access = await gateway.AuthorizeAsync(propertyId, AiAudience.Staff, ct);
        if (!access.IsAllowed) return (null, access.Error);

        var started = Stopwatch.StartNew();
        try
        {
            var property = await db.Properties.AsNoTracking().Where(x => x.Id == propertyId)
                .Select(x => new { x.TimeZoneId }).SingleOrDefaultAsync(ct);
            if (property is null) return (null, "Không tìm thấy cơ sở.");
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var period = StaffAiPeriodResolver.Resolve(text, contextPeriod, DateOnly.FromDateTime(nowLocal));
            var (fromDate, toDate, periodLabel) = (period.From, period.To, period.Label);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.MinValue), timeZone);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

            var bookings = await db.Bookings.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow
                            && x.CheckInUtc < toUtc && x.CheckOutUtc >= fromUtc)
                .OrderBy(x => x.CheckInUtc)
                .Select(x => new
                {
                    x.Id, x.Code, x.Status, x.CheckInUtc, x.CheckOutUtc,
                    Room = x.Room.Name, Customer = x.Customer.Name, Phone = x.Customer.Phone
                }).ToListAsync(ct);
            var rooms = await db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId && x.IsActive)
                .OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.HousekeepingStatus }).ToListAsync(ct);
            var nowUtc = DateTime.UtcNow;
            var occupiedRoomIds = await db.Bookings.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && OccupyingStatuses.Contains(x.Status) &&
                            x.CheckInUtc <= nowUtc && nowUtc < x.CheckOutUtc)
                .Select(x => x.RoomId).Distinct().ToListAsync(ct);
            var unresolvedReports = await db.RoomConditionReports.AsNoTracking()
                .Where(x => x.PropertyId == propertyId && x.Status != RoomConditionReportStatus.Resolved &&
                            x.Severity != RoomConditionSeverity.Normal)
                .GroupBy(x => x.RoomId)
                .Select(g => new { RoomId = g.Key, Urgent = g.Any(x => x.Severity == RoomConditionSeverity.Urgent), Count = g.Count() })
                .ToListAsync(ct);
            var occupiedSet = occupiedRoomIds.ToHashSet();
            var reportByRoom = unresolvedReports.ToDictionary(x => x.RoomId);

            var tables = new List<StaffAiTable>();
            var scheduleRows = bookings.Select(x => (IReadOnlyList<string>)[
                x.Code, x.Room, x.Customer, x.Phone,
                FormatLocal(x.CheckInUtc, timeZone), FormatLocal(x.CheckOutUtc, timeZone), StatusLabel(x.Status)
            ]).ToList();
            tables.Add(new StaffAiTable("Lịch booking", ["Mã", "Phòng", "Khách", "Điện thoại", "Nhận", "Trả", "Trạng thái"], scheduleRows));

            var roomRows = rooms.Select(x => (IReadOnlyList<string>)[x.Name, HousekeepingLabel(x.HousekeepingStatus)]).ToList();
            tables.Add(new StaffAiTable("Tình trạng dọn phòng", ["Phòng", "Tình trạng"], roomRows));
            var currentRoomRows = rooms.Select(room =>
            {
                reportByRoom.TryGetValue(room.Id, out var report);
                return (IReadOnlyList<string>)[
                    room.Name,
                    occupiedSet.Contains(room.Id) ? "Đang có khách" : "Đang trống",
                    HousekeepingLabel(room.HousekeepingStatus),
                    report is null ? "Không có" : report.Urgent ? $"Khẩn cấp ({report.Count})" : $"Cần kiểm tra ({report.Count})"
                ];
            }).ToList();
            tables.Add(new StaffAiTable("Trạng thái phòng hiện tại", ["Phòng", "Sử dụng", "Dọn phòng", "Cảnh báo hiện trạng"], currentRoomRows));

            decimal? netCollected = null;
            if (canViewFinance && ContainsFinanceIntent(text))
            {
                netCollected = await db.Payments.AsNoTracking()
                    .Where(x => x.PropertyId == propertyId && !x.IsVoided && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
                    .SumAsync(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount, ct);
                tables.Add(new StaffAiTable("Thực thu", ["Kỳ", "Thu ròng"],
                    [[periodLabel, $"{netCollected.Value:N0} đ"]]));
            }

            var checkIns = bookings.Count(x => x.CheckInUtc >= fromUtc && x.CheckInUtc < toUtc);
            var checkOuts = bookings.Count(x => x.CheckOutUtc >= fromUtc && x.CheckOutUtc < toUtc);
            var pending = bookings.Count(x => x.Status is BookingStatus.Requested or BookingStatus.Held);
            var dirty = rooms.Count(x => x.HousekeepingStatus == HousekeepingStatus.Dirty);
            var occupied = rooms.Count(x => occupiedSet.Contains(x.Id));
            var conditionWarnings = rooms.Count(x => reportByRoom.ContainsKey(x.Id));
            var summary = $"{periodLabel}: {bookings.Count} booking liên quan, {checkIns} lượt nhận phòng, {checkOuts} lượt trả phòng, {pending} booking chờ xử lý và {dirty} phòng cần dọn. Hiện tại có {occupied} phòng đang có khách, {rooms.Count - occupied} phòng trống và {conditionWarnings} phòng có cảnh báo hiện trạng chưa xử lý.";
            if (ContainsFinanceIntent(text) && !canViewFinance)
                summary += " Tài khoản của bạn không có quyền xem dữ liệu tài chính.";
            else if (netCollected.HasValue)
                summary += $" Thực thu ròng {netCollected.Value:N0} đ.";

            await LogAsync(propertyId, userId, "operations_summary", new { fromDate, toDate, canViewFinance, includesCurrentRoomState = true }, true, null, started.ElapsedMilliseconds, ct);
            return (new StaffAiResponse(summary, $"{fromDate:dd/MM/yyyy}–{toDate:dd/MM/yyyy}", period.Key, property.TimeZoneId, tables), null);
        }
        catch
        {
            await LogAsync(propertyId, userId, "operations_summary", new { message = text }, false, "query_failed", started.ElapsedMilliseconds, ct);
            throw;
        }
    }

    private async Task LogAsync(Guid propertyId, Guid userId, string tool, object parameters, bool success, string? error,
        long duration, CancellationToken ct)
    {
        db.AiToolExecutionLogs.Add(new AiToolExecutionLog
        {
            PropertyId = propertyId, UserId = userId, Audience = AiAudience.Staff, ToolName = tool,
            ParametersJson = JsonSerializer.Serialize(parameters), IsSuccess = success, ErrorCode = error, DurationMs = duration
        });
        await db.SaveChangesAsync(ct);
    }

    private static bool ContainsFinanceIntent(string text) =>
        text.Contains("doanh thu", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("thực thu", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("thanh toán", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("tiền", StringComparison.OrdinalIgnoreCase);

    private static string FormatLocal(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone).ToString("dd/MM HH:mm");
    private static string StatusLabel(BookingStatus status) => status switch
    {
        BookingStatus.Requested => "Chờ xác nhận", BookingStatus.Held => "Đang giữ", BookingStatus.Confirmed => "Đã xác nhận",
        BookingStatus.CheckedIn => "Đang ở", BookingStatus.Completed => "Hoàn thành", _ => status.ToString()
    };
    private static string HousekeepingLabel(HousekeepingStatus status) => status switch
    {
        HousekeepingStatus.Clean => "Sạch", HousekeepingStatus.Dirty => "Cần dọn", HousekeepingStatus.Cleaning => "Đang dọn", _ => status.ToString()
    };
}

public static class StaffAiPeriodResolver
{
    public static StaffAiResolvedPeriod Resolve(string text, string? contextPeriod, DateOnly today)
    {
        var normalized = text.ToLowerInvariant();
        if (normalized.Contains("ngày mai") || normalized.Trim() is "mai" or "mai?")
            return new("tomorrow", today.AddDays(1), today.AddDays(1), "Ngày mai");
        if (normalized.Contains("cuối tuần"))
        {
            var daysToSaturday = today.DayOfWeek == DayOfWeek.Sunday
                ? -1
                : ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
            var saturday = today.AddDays(daysToSaturday);
            return new("weekend", saturday, saturday.AddDays(1), "Cuối tuần");
        }
        if (normalized.Contains("tuần này"))
        {
            var mondayOffset = ((int)today.DayOfWeek + 6) % 7;
            var monday = today.AddDays(-mondayOffset);
            return new("week", monday, monday.AddDays(6), "Tuần này");
        }
        if (normalized.Contains("hôm nay")) return new("today", today, today, "Hôm nay");
        return contextPeriod switch
        {
            "tomorrow" => new("tomorrow", today.AddDays(1), today.AddDays(1), "Ngày mai"),
            "weekend" => Resolve("cuối tuần", null, today),
            "week" => Resolve("tuần này", null, today),
            _ => new("today", today, today, "Hôm nay")
        };
    }
}

public sealed record StaffAiResolvedPeriod(string Key, DateOnly From, DateOnly To, string Label);
