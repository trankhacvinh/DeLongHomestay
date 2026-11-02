using System.Globalization;
using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Finance;

namespace DeLong.Web.Features.Cancellations;

public static class CancellationEndpoints
{
    public static IEndpointRouteBuilder MapCancellationEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/admin/cancellations").RequireAuthorization("ViewCancellations").WithTags("Cancellations");

        read.MapGet("/", async (Guid? propertyId, string? scope, string? from, string? to, string? dateField, string? status, string? search,
            Guid? staffId, int? page, int? pageSize, ClaimsPrincipal user, CurrentPropertyService properties, CancellationService service, CancellationToken ct) =>
        {
            var resolved = await service.ResolveScopeAsync(user, properties, propertyId, scope, ct);
            if (resolved is null) return Results.Forbid();
            var filter = BuildFilter(resolved, from, to, dateField, status, search, staffId, page, pageSize);
            if (filter is null) return Results.Problem(detail: "Ngày không hợp lệ. Dùng định dạng yyyy-MM-dd.", statusCode: 400);
            var (result, error) = await service.ListAsync(resolved, filter, false, ct);
            return error is null ? Results.Ok(result) : Results.Problem(detail: error.Message, statusCode: 400);
        });

        read.MapGet("/export", async (Guid? propertyId, string? scope, string? from, string? to, string? dateField, string? status, string? search,
            Guid? staffId, ClaimsPrincipal user, CurrentPropertyService properties, CancellationService service, CancellationToken ct) =>
        {
            var resolved = await service.ResolveScopeAsync(user, properties, propertyId, scope, ct);
            if (resolved is null) return Results.Forbid();
            var filter = BuildFilter(resolved, from, to, dateField, status, search, staffId, 1, 200);
            if (filter is null) return Results.Problem(detail: "Ngày không hợp lệ. Dùng định dạng yyyy-MM-dd.", statusCode: 400);
            var (result, error) = await service.ListAsync(resolved, filter, true, ct);
            if (error is not null || result is null) return Results.Problem(detail: error?.Message, statusCode: 400);
            var zones = resolved.Accessible.ToDictionary(x => x.Id, x => x.TimeZoneId);
            var file = OperationalExcelExport.Create(
                new("Tổng hợp", ["Số booking hủy", "Tổng giá trị", "Đã thanh toán", "Đã hoàn", "Còn phải hoàn", "Giữ lại", "Booking chờ hoàn"],
                    [new object?[] { result.Totals.Count, result.Totals.TotalAmount, result.Totals.PaidAmount, result.Totals.RefundedAmount, result.Totals.RemainingRefundAmount, result.Totals.KeptAmount, result.Totals.PendingCount }]),
                new("Danh sách hủy", ["Chi nhánh", "Mã booking", "Khách", "SĐT", "Phòng", "Nhận phòng", "Thời gian hủy", "Nhân viên hủy", "Lý do", "Tổng tiền", "Đã thanh toán", "Cần hoàn", "Đã hoàn", "Còn phải hoàn", "Giữ lại", "Trạng thái hoàn", "Hoàn lúc", "Nhập hoàn lúc", "Người nhập hoàn", "Ghi chú"],
                    result.Rows.Select(x => new object?[]
                    {
                        x.PropertyName, x.BookingCode, x.CustomerName, x.CustomerPhone, x.RoomName,
                        LocalTime(x.CheckInUtc, zones[x.PropertyId]), LocalTime(x.CancelledAtUtc, zones[x.PropertyId]), x.CancelledByName, x.Reason,
                        x.TotalAmount, x.PaidAmount, x.RefundDueAmount, x.RefundedAmount, x.RemainingRefundAmount, x.KeptAmount, StatusText(x.RefundStatus),
                        x.LastRefundOccurredAtUtc.HasValue ? LocalTime(x.LastRefundOccurredAtUtc.Value, zones[x.PropertyId]) : null,
                        x.LastRefundEnteredAtUtc.HasValue ? LocalTime(x.LastRefundEnteredAtUtc.Value, zones[x.PropertyId]) : null,
                        x.LastRefundByName, x.RefundNote
                    }).ToList()));
            return Results.File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Huy-phong-hoan-tien-{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.xlsx");
        });

        var write = app.MapGroup("/api/admin/properties/{propertyId:guid}/cancellations").RequireAuthorization("ManagePayments")
            .AddEndpointFilter<PropertyAccessFilter>().AddEndpointFilter<ApiAntiforgeryFilter>().WithTags("Cancellations");

        write.MapPost("/{id:guid}/refund-due", async (Guid propertyId, Guid id, UpdateRefundDueRequest request, ClaimsPrincipal user, CancellationService service, CancellationToken ct) =>
            ToResult(await service.UpdateDueAsync(propertyId, id, request, Actor(user), ct)));

        write.MapPost("/{id:guid}/refunds", async (Guid propertyId, Guid id, RecordCancellationRefundRequest request, ClaimsPrincipal user, CancellationService service, CancellationToken ct) =>
            ToResult(await service.AddRefundAsync(propertyId, id, request, Actor(user), ct)));

        write.MapPut("/{id:guid}/refunds/{paymentId:guid}", async (Guid propertyId, Guid id, Guid paymentId, EditCancellationRefundRequest request, ClaimsPrincipal user, CancellationService service, CancellationToken ct) =>
            ToResult(await service.EditRefundAsync(propertyId, id, paymentId, request, Actor(user), ct)));

        return app;
    }

    public static string StatusText(string status) => status switch
    {
        CancellationRefundStatuses.NotRequired => "Không cần hoàn",
        CancellationRefundStatuses.Pending => "Chờ hoàn",
        CancellationRefundStatuses.Partial => "Hoàn một phần",
        CancellationRefundStatuses.Refunded => "Đã hoàn",
        CancellationRefundStatuses.Kept => "Không hoàn (giữ cọc)",
        _ => status
    };

    private static DateTime LocalTime(DateTime utc, string timeZoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

    private static CancellationFilter? BuildFilter(CancellationScope scope, string? from, string? to, string? dateField, string? status,
        string? search, Guid? staffId, int? page, int? pageSize)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(scope.WorkingProperty.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        var start = string.IsNullOrWhiteSpace(from) ? new DateOnly(today.Year, today.Month, 1) : default;
        var end = string.IsNullOrWhiteSpace(to) ? start.AddMonths(1).AddDays(-1) : default;
        if (!string.IsNullOrWhiteSpace(from) && !DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) return null;
        if (!string.IsNullOrWhiteSpace(to) && !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)) return null;
        if (string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(from)) end = start.AddMonths(1).AddDays(-1);
        return new CancellationFilter
        {
            From = start, To = end, DateField = dateField ?? "cancelled", Status = string.IsNullOrWhiteSpace(status) ? null : status,
            Search = search, ActorUserId = staffId, Page = page ?? 1, PageSize = pageSize ?? 50
        };
    }

    private static IResult ToResult(CancellationOperationError? error)
    {
        if (error is null) return Results.Ok(new { ok = true });
        var status = error.Code switch { "not_found" => 404, "refund_exceeds_paid" or "already_voided" => 409, _ => 400 };
        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể cập nhật hoàn tiền",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    private static Guid? Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
