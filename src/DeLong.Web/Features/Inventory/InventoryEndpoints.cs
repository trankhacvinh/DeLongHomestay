using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Finance;

namespace DeLong.Web.Features.Inventory;

public sealed record InitializeInventoryRequest(Guid PropertyId);

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/inventory").RequireAuthorization("ViewInventory").WithTags("Inventory");
        group.MapGet("/import/template", (string mode) => mode is "opening" or "catalog"
            ? Results.File(InventoryImportService.Template(mode), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", mode == "opening" ? "Mau-ton-dau.xlsx" : "Mau-mat-hang.xlsx")
            : Results.BadRequest(new { message = "Loại import không hợp lệ." })).RequireAuthorization("ManageInventory");
        group.MapPost("/import/preview", async (HttpRequest request, ClaimsPrincipal user, InventoryImportService service, CancellationToken ct) => {
            var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("file");
            if (file is null) return Results.Problem(detail: "Chưa chọn file Excel.", statusCode: 400);
            DateTimeOffset? at = DateTimeOffset.TryParse(form["openingAt"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ? date : null;
            var (result, error) = await service.PreviewAsync(user, file, new(form["mode"].ToString(), form["duplicates"].ToString(), at, form["sheet"].ToString()), ct);
            return error is null ? Results.Ok(result) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter(async (context, next) => {
            var request = context.HttpContext.Request;
            if (!request.HasFormContentType || request.ContentLength > 5 * 1024 * 1024) return Results.Problem(detail: "Chọn file .xlsx tối đa 4 MB.", statusCode: 400);
            var limit = context.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 5 * 1024 * 1024;
            return await next(context);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/import/commit", async (InventoryImportCommit request, ClaimsPrincipal user, InventoryImportService service, CancellationToken ct) => {
            var (result, error) = await service.CommitAsync(user, request, ct); return error is null ? Results.Ok(result) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapGet("/", async (string month, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.GetAsync(user, month, ct); return error is null ? Results.Ok(result) : Problem(error);
        });
        group.MapGet("/items/{id:guid}/history", async (Guid id, int? skip, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.HistoryAsync(user, id, skip ?? 0, ct); return error is null ? Results.Ok(result) : Problem(error);
        });
        group.MapGet("/export", async (string month, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.GetAsync(user, month, ct);
            if (error is not null) return Problem(error);
            if (result!.Warehouse is null) return Results.BadRequest(new { message = "Chưa khởi tạo kho." });
            var zone = TimeZoneInfo.FindSystemTimeZoneById(result.Warehouse.TimeZoneId);
            var file = OperationalExcelExport.Create(
                new("Nhập xuất tồn", ["Mã", "Mặt hàng", "ĐVT", "Tồn đầu", "Nhập", "Xuất", "Điều chỉnh", "Tồn cuối"], result.Stock.Select(x => new object?[] { x.Code, x.Name, x.UnitName, x.Opening, x.Received, x.Issued, x.Adjustments, x.Closing }).ToList()),
                new("Phiếu", ["Mã phiếu", "Loại", "Ngày", "Chi nhánh nhận", "Mặt hàng", "ĐVT", "Số lượng", "Đơn giá", "Thành tiền", "Ghi chú", "Đã hủy", "Lý do hủy"],
                    result.Documents.SelectMany(x => x.Lines.Select(l => new object?[] { x.Code, TypeName(x.Type), TimeZoneInfo.ConvertTimeFromUtc(x.OccurredAtUtc, zone), x.RecipientName, l.ItemName, l.UnitName, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice, x.Note, x.IsVoided, x.VoidReason })).ToList()),
                new("Cấp chi nhánh", ["Chi nhánh nhận", "Mặt hàng", "ĐVT", "Số lượng xuất", "Giá trị theo phiếu"], result.Distribution.Select(x => new object?[] { x.RecipientName, x.ItemName, x.UnitName, x.Quantity, x.Value }).ToList()));
            return Results.File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Kho-{month}.xlsx");
        });
        group.MapPost("/initialize", async (InitializeInventoryRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.InitializeAsync(user, request.PropertyId, ct); return error is null ? Results.Ok(result) : Problem(error);
        }).RequireAuthorization(p => p.RequireRole("Admin")).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/units", async (InventoryUnitRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.AddUnitAsync(user, request, ct); return error is null ? Results.Ok(result) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/items", async (InventoryItemRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.SaveItemAsync(user, null, request, ct); return error is null ? Results.Ok(result) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPut("/items/{id:guid}", async (Guid id, InventoryItemRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.SaveItemAsync(user, id, request, ct); return error is null ? Results.Ok(result) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/documents", async (InventoryDocumentRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var (result, error) = await service.PostAsync(user, request, ct); return error is null ? Results.Ok(new { id = result }) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/documents/{id:guid}/void", async (Guid id, InventoryVoidRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) => {
            var error = await service.VoidAsync(user, id, request.Reason, ct); return error is null ? Results.Ok(new { id }) : Problem(error);
        }).RequireAuthorization("ManageInventory").AddEndpointFilter<ApiAntiforgeryFilter>();
        return app;
    }
    public static string TypeName(DeLong.Web.Domain.Entities.InventoryDocumentType type) => type switch {
        DeLong.Web.Domain.Entities.InventoryDocumentType.Opening => "Tồn đầu", DeLong.Web.Domain.Entities.InventoryDocumentType.Receipt => "Nhập kho",
        DeLong.Web.Domain.Entities.InventoryDocumentType.Issue => "Xuất kho", DeLong.Web.Domain.Entities.InventoryDocumentType.AdjustmentIn => "Điều chỉnh tăng", _ => "Điều chỉnh giảm"
    };
    private static IResult Problem(InventoryError error) => Results.Problem(title: "Không thể xử lý kho", detail: error.Message, statusCode: error.Status, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
