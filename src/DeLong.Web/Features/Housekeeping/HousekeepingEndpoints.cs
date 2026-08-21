using System.Security.Claims;
using System.Globalization;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Operations;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Housekeeping;

public static class HousekeepingEndpoints
{
    public static IEndpointRouteBuilder MapHousekeepingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/housekeeping")
            .RequireAuthorization("ViewHousekeeping")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Housekeeping");

        group.MapGet("/", async (
            Guid propertyId,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(propertyId, cancellationToken)));

        group.MapGet("/schedule", async (
            Guid propertyId,
            string? date,
            int? days,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
                return Results.BadRequest(new { message = "Ngày xem lịch không hợp lệ." });

            var schedule = await service.GetScheduleAsync(propertyId, from, Math.Clamp(days ?? 1, 1, 7), cancellationToken);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        });

        group.MapGet("/settings", async (
            Guid propertyId,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(propertyId, cancellationToken);
            return settings is null ? Results.NotFound() : Results.Ok(settings);
        }).RequireAuthorization("ManageRooms");

        group.MapPut("/settings", async (
            Guid propertyId,
            UpdateHousekeepingSettingsRequest request,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var (settings, error) = await service.SaveSettingsAsync(propertyId, request, cancellationToken);
            if (settings is not null)
            {
                OperationsRealtimeBroker.Shared.Publish(OperationsRealtimeEvent.Create(
                    propertyId,
                    OperationsEventTypes.HousekeepingChanged));
                return Results.Ok(settings);
            }

            return Results.Problem(
                type: "https://delong.local/problems/housekeeping_settings_invalid",
                title: "Không thể lưu cấu hình dọn phòng",
                detail: error,
                statusCode: error == "Không tìm thấy cơ sở." ? 404 : 400,
                extensions: new Dictionary<string, object?> { ["code"] = "housekeeping_settings_invalid" });
        })
            .RequireAuthorization("ManageRooms")
            .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/rooms/{roomId:guid}/status", async (
            Guid propertyId,
            Guid roomId,
            ChangeHousekeepingStatusRequest request,
            ClaimsPrincipal user,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var actorUserId = GetUserId(user);
            var room = await service.ChangeStatusAsync(propertyId, roomId, request.Status, actorUserId, cancellationToken);
            if (room is null) return Results.NotFound();
            OperationsRealtimeBroker.Shared.Publish(OperationsRealtimeEvent.Create(
                propertyId,
                OperationsEventTypes.HousekeepingChanged,
                null,
                roomId));
            return Results.Ok(room);
        })
        .RequireAuthorization("ManageHousekeeping")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapGet("/reports", async (
            Guid propertyId,
            Guid? roomId,
            int? take,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetConditionReportsAsync(propertyId, roomId, take ?? 50, cancellationToken)));

        group.MapGet("/report-tags", async (
            Guid propertyId,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetConditionTagsAsync(propertyId, cancellationToken)));

        group.MapPost("/reports", async (
            Guid propertyId,
            HttpRequest request,
            ClaimsPrincipal user,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Dữ liệu báo cáo không hợp lệ." });

            var form = await request.ReadFormAsync(cancellationToken);
            if (!Guid.TryParse(form["roomId"].FirstOrDefault(), out var roomId))
                return Results.BadRequest(new { message = "Vui lòng chọn phòng." });
            if (!Enum.TryParse<RoomInspectionType>(form["inspectionType"].FirstOrDefault(), true, out var inspectionType))
                return Results.BadRequest(new { message = "Loại kiểm tra không hợp lệ." });
            if (!Enum.TryParse<RoomConditionSeverity>(form["severity"].FirstOrDefault(), true, out var severity))
                return Results.BadRequest(new { message = "Mức độ tình trạng không hợp lệ." });
            var actorUserId = GetUserId(user);
            if (!actorUserId.HasValue) return Results.Unauthorized();

            var (report, error) = await service.CreateConditionReportAsync(
                propertyId,
                roomId,
                actorUserId.Value,
                inspectionType,
                severity,
                form["content"].FirstOrDefault(),
                form["tags"].Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray(),
                form.Files.ToArray(),
                cancellationToken);
            return report is not null
                ? Results.Created($"/api/admin/properties/{propertyId}/housekeeping/reports/{report.Id}", report)
                : Results.Problem(
                    title: "Không thể lưu báo cáo tình trạng phòng",
                    detail: error,
                    statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["code"] = "room_condition_report_invalid" });
        })
        .RequireAuthorization("ManageHousekeeping")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/reports/{reportId:guid}/status", async (
            Guid propertyId,
            Guid reportId,
            ChangeRoomConditionReportStatusRequest request,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var report = await service.ChangeConditionReportStatusAsync(propertyId, reportId, request.Status, cancellationToken);
            return report is null ? Results.NotFound() : Results.Ok(report);
        })
        .RequireAuthorization("ManageHousekeeping")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/report-tags", async (
            Guid propertyId,
            CreateRoomConditionTagRequest request,
            HousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var (tag, error) = await service.CreateConditionTagAsync(propertyId, request, cancellationToken);
            return tag is not null
                ? Results.Ok(tag)
                : Results.Problem(title: "Không thể tạo tag", detail: error, statusCode: 400);
        })
        .RequireAuthorization("ManageRooms")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
