using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Imports;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/imports")
            .RequireAuthorization("ManageBookings")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Imports");

        group.MapPost("/bookings/preview", async (
            Guid propertyId,
            HttpRequest request,
            ExcelBookingImportService service,
            CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            if (file is null) return Problem("file_empty", "Vui lòng chọn file Excel.", 400);
            var (preview, error) = await service.PreviewAsync(propertyId, file, cancellationToken);
            return error is null ? Results.Ok(preview) : Problem(error.Code, error.Message, 400);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/bookings/commit", async (
            Guid propertyId,
            HttpRequest request,
            ClaimsPrincipal user,
            ExcelBookingImportService service,
            CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            if (file is null) return Problem("file_empty", "Vui lòng chọn file Excel.", 400);
            var (result, error) = await service.ImportAsync(propertyId, file, GetUserId(user), cancellationToken);
            if (error is null) return Results.Ok(result);
            var status = error.Code is "booking_conflict" ? 409 : 400;
            return Problem(error.Code, error.Message, status);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/bookings/convert-calendar", async (
            Guid propertyId,
            HttpRequest request,
            LegacyCalendarConversionService service,
            CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            if (file is null) return Problem("file_empty", "Vui lòng chọn file lịch Excel.", 400);
            var (result, error) = await service.ConvertAsync(file, cancellationToken);
            if (error is not null) return Problem(error.Code, error.Message, 400);
            return Results.File(
                result!.FileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.FileName,
                enableRangeProcessing: false);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapGet("/bookings/template", (
            Guid propertyId,
            ExcelBookingImportService service) =>
        {
            var bytes = service.CreateTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DeLong-booking-import-{DateTime.Today:yyyyMMdd}.xlsx");
        });

        return app;
    }

    private static async Task<IFormFile?> ReadFileAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType) return null;
        var form = await request.ReadFormAsync(cancellationToken);
        return form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult Problem(string code, string message, int status) => Results.Problem(
        type: $"https://delong.local/problems/{code}",
        title: "Không thể import dữ liệu",
        detail: message,
        statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
