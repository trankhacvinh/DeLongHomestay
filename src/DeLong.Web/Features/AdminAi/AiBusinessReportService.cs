using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Reports;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiBusinessReportRequest(string Period = "month");
public sealed record AiBusinessMetricDto(string Name, decimal Current, decimal Previous, decimal? ChangePercent, string Unit);
public sealed record AiBusinessBreakdownDto(string Label, int BookingCount, decimal BookingValue, double Hours = 0);
public sealed record AiCustomerMixDto(int NewCustomers, int ReturningCustomers);
public sealed record AiBusinessInsightDto(IReadOnlyList<string> Facts, IReadOnlyList<string> Interpretations,
    IReadOnlyList<AiBusinessRecommendationDto> Recommendations, string Confidence);
public sealed record AiBusinessRecommendationDto(string Title, string Rationale, string ProposalPrompt);
public sealed record AiBusinessReportDto(string Period, string TimeZone, string Definition, DateOnly From, DateOnly To,
    DateOnly PreviousFrom, DateOnly PreviousTo, IReadOnlyList<AiBusinessMetricDto> Metrics,
    IReadOnlyList<ReportRoomDto> ByRoom, IReadOnlyList<ReportSourceDto> BySource,
    IReadOnlyList<AiBusinessBreakdownDto> ByBookingType, IReadOnlyList<AiBusinessBreakdownDto> ByRate,
    AiCustomerMixDto Customers, AiBusinessInsightDto Insights, string ReportUrl, bool Cached = false);

public sealed class AiBusinessReportService(AppDbContext db, ReportService reports, AiResponseCacheService cache)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    public async Task<AiBusinessReportDto?> GetAsync(Guid propertyId, string? requestedPeriod, Guid userId, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().Where(x => x.Id == propertyId)
            .Select(x => new { x.TimeZoneId }).SingleOrDefaultAsync(ct);
        if (property is null) return null;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var range = AiReportPeriodResolver.Resolve(requestedPeriod, today);
        var dataVersion = await db.PropertyAiKnowledgeSnapshots.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .Select(x => (long?)x.Version).SingleOrDefaultAsync(ct) ?? 1;
        var cacheParameters = new { range.Period, range.From, range.To };
        var cacheKey = AiResponseCacheService.CreateKey(propertyId, AiAudience.Admin, "business_report", cacheParameters, dataVersion);
        var cachedJson = await cache.GetAsync(propertyId, AiAudience.Admin, cacheKey, ct);
        if (cachedJson is not null)
        {
            var cached = System.Text.Json.JsonSerializer.Deserialize<AiBusinessReportDto>(cachedJson, Json);
            if (cached is not null)
            {
                await LogAsync(propertyId, userId, cacheParameters, true, ct);
                return cached with { Cached = true };
            }
        }
        var currentFromUtc = ToUtc(range.From, timeZone);
        var currentToUtc = ToUtc(range.To.AddDays(1), timeZone);
        var previousFromUtc = ToUtc(range.PreviousFrom, timeZone);
        var previousToUtc = ToUtc(range.PreviousTo.AddDays(1), timeZone);
        var current = await reports.GetAsync(propertyId, currentFromUtc, currentToUtc, timeZone, ct);
        var previous = await reports.GetAsync(propertyId, previousFromUtc, previousToUtc, timeZone, ct);
        var currentAnalytics = await LoadAnalyticsAsync(propertyId, currentFromUtc, currentToUtc, ct);
        var previousAnalytics = await LoadAnalyticsAsync(propertyId, previousFromUtc, previousToUtc, ct);
        var metrics = new[]
        {
            Metric("Thực thu", current.NetReceipts, previous.NetReceipts, "đ"),
            Metric("Hoàn tiền", current.Refunds, previous.Refunds, "đ"),
            Metric("Chi phí", current.Expenses, previous.Expenses, "đ"),
            Metric("Dòng tiền ròng", current.NetCashFlow, previous.NetCashFlow, "đ"),
            Metric("Số booking", current.BookingCount, previous.BookingCount, "booking"),
            Metric("Công suất", (decimal)current.OccupancyRate, (decimal)previous.OccupancyRate, "%"),
            Metric("Tỷ lệ hủy", (decimal)current.CancellationRate, (decimal)previous.CancellationRate, "%"),
            Metric("Thời lượng booking trung bình", currentAnalytics.AverageHours, previousAnalytics.AverageHours, "giờ"),
            Metric("Thời gian đặt trước trung bình", currentAnalytics.AverageLeadDays, previousAnalytics.AverageLeadDays, "ngày")
        };
        var insights = AiBusinessInsightBuilder.Build(current, previous, currentAnalytics.ByRate);
        var result = new AiBusinessReportDto(range.Period, property.TimeZoneId,
            "Thực thu = phiếu thu không hủy - hoàn tiền; dòng tiền ròng = thực thu - chi phí; công suất = tổng giờ booking hợp lệ / tổng giờ phòng hoạt động trong kỳ.",
            range.From, range.To, range.PreviousFrom, range.PreviousTo, metrics, current.ByRoom, current.BySource,
            currentAnalytics.ByType, currentAnalytics.ByRate,
            new AiCustomerMixDto(currentAnalytics.NewCustomers, currentAnalytics.ReturningCustomers),
            insights,
            $"/Admin/Reports?propertyId={propertyId:D}&month={range.From:yyyy-MM}");
        await cache.SetAsync(propertyId, AiAudience.Admin, "business_report", cacheParameters,
            System.Text.Json.JsonSerializer.Serialize(result, Json), "reports", dataVersion, TimeSpan.FromMinutes(5), ct);
        await LogAsync(propertyId, userId, cacheParameters, false, ct);
        return result;
    }

    private async Task LogAsync(Guid propertyId, Guid userId, object parameters, bool cacheHit, CancellationToken ct)
    {
        db.AiToolExecutionLogs.Add(new AiToolExecutionLog
        {
            PropertyId = propertyId, UserId = userId, Audience = AiAudience.Admin,
            ToolName = cacheHit ? "business_report_cache_hit" : "business_report",
            ParametersJson = System.Text.Json.JsonSerializer.Serialize(parameters, Json), IsSuccess = true
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Analytics> LoadAnalyticsAsync(Guid propertyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.CheckInUtc >= fromUtc && x.CheckInUtc < toUtc
                        && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => new { x.Id, x.CustomerId, x.Type, x.RoomAmount, x.SpecialSurchargeAmount, x.ExtraAmount,
                x.DiscountAmount, x.CheckInUtc, x.CheckOutUtc, x.CreatedAtUtc }).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var segments = await db.BookingRateSegments.AsNoTracking()
            .Where(x => ids.Contains(x.BookingId)).Select(x => new { x.BookingId, x.RateName, x.AppliedAmount,
                x.CheckInUtc, x.CheckOutUtc }).ToListAsync(ct);
        var customerIds = rows.Select(x => x.CustomerId).Distinct().ToArray();
        var returningIds = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && customerIds.Contains(x.CustomerId) && x.CheckInUtc < fromUtc
                        && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => x.CustomerId).Distinct().ToArrayAsync(ct);
        var returning = returningIds.ToHashSet();
        var byType = rows.GroupBy(x => x.Type).Select(group => new AiBusinessBreakdownDto(group.Key.ToString(), group.Count(),
            group.Sum(x => x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount),
            Math.Round(group.Sum(x => (x.CheckOutUtc - x.CheckInUtc).TotalHours), 1))).OrderByDescending(x => x.BookingValue).ToArray();
        var byRate = segments.GroupBy(x => x.RateName).Select(group => new AiBusinessBreakdownDto(group.Key, group.Select(x => x.BookingId).Distinct().Count(),
            group.Sum(x => x.AppliedAmount), Math.Round(group.Sum(x => (x.CheckOutUtc - x.CheckInUtc).TotalHours), 1))).OrderByDescending(x => x.BookingValue).ToArray();
        return new Analytics(
            rows.Count == 0 ? 0 : Math.Round(rows.Average(x => (decimal)(x.CheckOutUtc - x.CheckInUtc).TotalHours), 1),
            rows.Count == 0 ? 0 : Math.Round(rows.Average(x => (decimal)Math.Max(0, (x.CheckInUtc - x.CreatedAtUtc).TotalDays)), 1),
            customerIds.Count(x => !returning.Contains(x)), customerIds.Count(x => returning.Contains(x)), byType, byRate);
    }

    private static AiBusinessMetricDto Metric(string name, decimal current, decimal previous, string unit) =>
        new(name, current, previous, previous == 0 ? null : Math.Round((current - previous) / Math.Abs(previous) * 100, 1), unit);
    private static DateTime ToUtc(DateOnly date, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), timeZone);
    private sealed record Analytics(decimal AverageHours, decimal AverageLeadDays, int NewCustomers, int ReturningCustomers,
        IReadOnlyList<AiBusinessBreakdownDto> ByType, IReadOnlyList<AiBusinessBreakdownDto> ByRate);
}

public static class AiBusinessInsightBuilder
{
    public static AiBusinessInsightDto Build(ReportSnapshotDto current, ReportSnapshotDto previous,
        IReadOnlyList<AiBusinessBreakdownDto> rates)
    {
        var facts = new List<string>
        {
            $"Kỳ hiện tại có {current.BookingCount} booking, thực thu {current.NetReceipts:N0} đ và công suất {current.OccupancyRate:N1}%.",
            $"Kỳ trước có {previous.BookingCount} booking, thực thu {previous.NetReceipts:N0} đ và công suất {previous.OccupancyRate:N1}%.",
            $"Kỳ hiện tại hoàn {current.Refunds:N0} đ, chi phí {current.Expenses:N0} đ và dòng tiền ròng {current.NetCashFlow:N0} đ."
        };
        var sample = current.BookingCount + previous.BookingCount;
        if (sample < 10)
            return new AiBusinessInsightDto(facts,
                ["Chưa đủ dữ liệu để kết luận xu hướng; cần tối thiểu 10 booking trong hai kỳ."], [], "Thấp");

        var interpretations = new List<string>();
        var recommendations = new List<AiBusinessRecommendationDto>();
        var revenueChange = previous.NetReceipts == 0 ? (decimal?)null
            : Math.Round((current.NetReceipts - previous.NetReceipts) / Math.Abs(previous.NetReceipts) * 100, 1);
        if (revenueChange is < -10)
        {
            interpretations.Add($"Thực thu giảm {Math.Abs(revenueChange.Value):N1}% so với kỳ trước.");
            recommendations.Add(new AiBusinessRecommendationDto("Rà soát chương trình kích cầu",
                "Thực thu giảm trên 10%; cần kiểm tra phòng hoặc khung bán kém trước khi điều chỉnh.",
                "Dựa trên báo cáo vừa xem, hãy tạo preview một chương trình voucher phù hợp cho phòng hoặc khung bán kém. Không áp dụng ngay."));
        }
        else if (revenueChange is > 10) interpretations.Add($"Thực thu tăng {revenueChange.Value:N1}% so với kỳ trước.");
        else interpretations.Add("Thực thu không biến động quá 10% so với kỳ trước.");

        if (current.OccupancyRate < 40)
        {
            interpretations.Add($"Công suất {current.OccupancyRate:N1}% đang dưới ngưỡng tham khảo 40%.");
            if (rates.Count > 0)
            {
                var weakest = rates.OrderBy(x => x.BookingValue).ThenBy(x => x.BookingCount).First();
                recommendations.Add(new AiBusinessRecommendationDto($"Xem lại khung {weakest.Label}",
                    $"Đây là khung có giá trị booking thấp nhất trong kỳ ({weakest.BookingValue:N0} đ).",
                    $"Hãy phân tích khung {weakest.Label} và tạo preview điều chỉnh giá hoặc voucher nếu hợp lý. Không áp dụng ngay."));
            }
        }
        if (current.CancellationRate >= 20)
            interpretations.Add($"Tỷ lệ hủy {current.CancellationRate:N1}% cần được kiểm tra nguyên nhân trước khi thay đổi chính sách.");
        if (recommendations.Count == 0)
            recommendations.Add(new AiBusinessRecommendationDto("Tiếp tục theo dõi",
                "Chưa có tín hiệu đủ mạnh để đề xuất thay đổi giá hoặc voucher.",
                "Hãy so sánh thêm báo cáo theo phòng và khung giờ trước khi tạo đề xuất cấu hình."));
        return new AiBusinessInsightDto(facts, interpretations, recommendations, sample >= 30 ? "Cao" : "Trung bình");
    }
}

public sealed record AiReportPeriod(string Period, DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo);

public static class AiReportPeriodResolver
{
    public static AiReportPeriod Resolve(string? value, DateOnly today)
    {
        var period = (value ?? "month").Trim().ToLowerInvariant();
        DateOnly from;
        switch (period)
        {
            case "day": from = today; break;
            case "week": from = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); break;
            case "quarter":
                var quarterMonth = ((today.Month - 1) / 3) * 3 + 1;
                from = new DateOnly(today.Year, quarterMonth, 1); break;
            case "year": from = new DateOnly(today.Year, 1, 1); break;
            default: period = "month"; from = new DateOnly(today.Year, today.Month, 1); break;
        }
        var elapsedDays = today.DayNumber - from.DayNumber;
        var previousFrom = period switch
        {
            "day" => today.AddDays(-1),
            "week" => from.AddDays(-7),
            "month" => from.AddMonths(-1),
            "quarter" => from.AddMonths(-3),
            "year" => from.AddYears(-1),
            _ => from.AddMonths(-1)
        };
        var previousPeriodEnd = period switch
        {
            "day" => previousFrom,
            "week" => previousFrom.AddDays(6),
            "month" => previousFrom.AddMonths(1).AddDays(-1),
            "quarter" => previousFrom.AddMonths(3).AddDays(-1),
            "year" => previousFrom.AddYears(1).AddDays(-1),
            _ => previousFrom.AddMonths(1).AddDays(-1)
        };
        var alignedPreviousTo = previousFrom.AddDays(elapsedDays);
        var previousTo = alignedPreviousTo < previousPeriodEnd ? alignedPreviousTo : previousPeriodEnd;
        return new AiReportPeriod(period, from, today, previousFrom, previousTo);
    }
}
