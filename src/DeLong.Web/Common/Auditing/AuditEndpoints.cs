using DeLong.Web.Common.Security;

namespace DeLong.Web.Common.Auditing;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/audit")
            .RequireAuthorization("ViewOperations")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Audit");

        group.MapGet("/{entityType}/{entityId:guid}", async (
            Guid propertyId,
            string entityType,
            Guid entityId,
            AuditService service,
            int? skip,
            CancellationToken cancellationToken) =>
        {
            // Lịch sử của dữ liệu lương chứa số tiền; API audit chung mở cho cả Staff/Viewer nên không trả các loại này.
            if (PayrollAuditTypes.IsRestricted(entityType)) return Results.NotFound();
            var history = await service.GetEntityHistoryAsync(
                propertyId, entityType.Trim(), entityId, cancellationToken, skip ?? 0);
            return Results.Ok(history);
        });

        group.MapGet("/Booking/{bookingId:guid}/activity", async (Guid propertyId, Guid bookingId, AuditService service, CancellationToken ct) =>
            Results.Ok(await service.GetBookingActivityAsync(propertyId, bookingId, ct)));

        return app;
    }
}

public static class PayrollAuditTypes
{
    public const string Employee = "Employee";
    public const string Attendance = "Attendance";
    public const string PayrollAdjustment = "PayrollAdjustment";
    public const string PayrollPeriod = "PayrollPeriod";
    public const string PayrollPolicy = "PayrollPolicy";
    public const string PayrollHoliday = "PayrollHoliday";
    public const string PayrollTransaction = "PayrollTransaction";

    private static readonly HashSet<string> Restricted = new(StringComparer.OrdinalIgnoreCase)
    {
        Employee, Attendance, PayrollAdjustment, PayrollPeriod, PayrollPolicy, PayrollHoliday, PayrollTransaction
    };

    public static bool IsRestricted(string? entityType) =>
        !string.IsNullOrWhiteSpace(entityType) && Restricted.Contains(entityType.Trim());
}
