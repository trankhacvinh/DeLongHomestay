using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Finance;

namespace DeLong.Web.Features.Cash;

public static class CashEndpoints
{
    public static IEndpointRouteBuilder MapCashEndpoints(this IEndpointRouteBuilder app)
    {
        var report = app.MapGroup("/api/admin/cash").RequireAuthorization("ViewFinance").WithTags("Cash");
        report.MapGet("/", async (Guid? propertyId, string? scope, string month, Guid? roomId, ClaimsPrincipal user, CurrentPropertyService properties, CashService service, CancellationToken ct) => {
            var resolved = await properties.ResolveReportingScopeAsync(user, propertyId, scope, ct);
            if (resolved is null) return Results.Forbid();
            var (result, error) = await service.GetAsync(resolved, month, roomId, ct);
            return error is null ? Results.Ok(result) : Results.Problem(detail: error, statusCode: 400);
        });
        report.MapGet("/export", async (Guid? propertyId, string? scope, string month, Guid? roomId, ClaimsPrincipal user, CurrentPropertyService properties, CashService service, CancellationToken ct) => {
            var resolved = await properties.ResolveReportingScopeAsync(user, propertyId, scope, ct);
            if (resolved is null) return Results.Forbid();
            var (result, error) = await service.GetAsync(resolved, month, roomId, ct);
            if (error is not null) return Results.Problem(detail: error, statusCode: 400);
            var zones = resolved.Properties.ToDictionary(x => x.Id, x => TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId));
            OperationalSheet Groups(string name, IReadOnlyList<CashGroupDto> rows) => new(name, ["Nhóm", "Thu tiền mặt", "Hoàn tiền mặt", "Chi tiền mặt", "Chênh lệch"], rows.Select(x => new object?[] { x.Name, x.Inflow, x.Refunds, x.Expenses, x.Net }).ToList());
            var file = OperationalExcelExport.Create(
                new("Tổng hợp", ["Tháng", "Thu booking", "Thu ngoài", "Hoàn", "Chi", "Chênh lệch thu chi"], [new object?[] { month, result!.Totals.BookingReceipts, result.Totals.OtherReceipts, result.Totals.Refunds, result.Totals.Expenses, result.Totals.Net }]),
                new("Giao dịch", ["Ngày thực thu chi", "Nguồn", "Chi nhánh", "Phòng", "Nội dung", "Ghi chú", "Thu", "Chi hoặc hoàn", "Đã hủy", "Lý do hủy", "Người ghi nhận"],
                    result.Entries.Select(x => new object?[] { TimeZoneInfo.ConvertTimeFromUtc(x.OccurredAtUtc, zones[x.PropertyId]), x.Source, x.PropertyName, x.RoomName ?? "Khoản chung", x.Description, x.Note, x.Inflow, x.Outflow, x.IsVoided, x.VoidReason, x.RecordedByName }).ToList()),
                Groups("Theo chi nhánh", result.ByProperty), Groups("Theo phòng", result.ByRoom), Groups("Theo ngày", result.ByDay), Groups("Theo nhóm chi", result.ByExpense));
            return Results.File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Tien-mat-{month}.xlsx");
        });
        var mutations = app.MapGroup("/api/admin/properties/{propertyId:guid}/cash").RequireAuthorization("ManageFinance")
            .AddEndpointFilter<PropertyAccessFilter>().AddEndpointFilter<ApiAntiforgeryFilter>().WithTags("Cash");
        mutations.MapPost("/receipts", async (Guid propertyId, CashReceiptRequest request, ClaimsPrincipal user, CashService service, CancellationToken ct) => {
            var (id, error) = await service.AddAsync(propertyId, request, Actor(user), ct);
            return error is null ? Results.Ok(new { id }) : Results.Problem(detail: error, statusCode: 400);
        });
        mutations.MapPost("/receipts/{id:guid}/void", async (Guid propertyId, Guid id, CashVoidRequest request, ClaimsPrincipal user, CashService service, CancellationToken ct) => {
            var error = await service.VoidAsync(propertyId, id, request.Reason, Actor(user), ct);
            return error is null ? Results.Ok(new { id }) : Results.Problem(detail: error, statusCode: 400);
        });
        return app;
    }
    private static Guid? Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
