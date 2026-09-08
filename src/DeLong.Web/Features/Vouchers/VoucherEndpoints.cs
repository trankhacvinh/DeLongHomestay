using System.Security.Claims;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;

namespace DeLong.Web.Features.Vouchers;

public static class VoucherEndpoints
{
    public static IEndpointRouteBuilder MapVoucherEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/properties/{propertyId:guid}/vouchers")
            .RequireAuthorization("ViewVouchers")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Vouchers");

        admin.MapGet("/", async (
            Guid propertyId,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            VoucherService service,
            CancellationToken ct) =>
        {
            Domain.Enums.VoucherStatus? parsedStatus = Enum.TryParse<Domain.Enums.VoucherStatus>(status, true, out var value)
                ? value
                : null;
            return Results.Ok(await service.GetAllAsync(propertyId, parsedStatus, search, fromUtc, toUtc, ct));
        });

        admin.MapGet("/{voucherId:guid}", async (
            Guid propertyId, Guid voucherId, VoucherService service, CancellationToken ct) =>
        {
            var voucher = await service.GetAsync(propertyId, voucherId, ct);
            return voucher is null ? Results.NotFound() : Results.Ok(voucher);
        });

        admin.MapGet("/{voucherId:guid}/history", async (
            Guid propertyId, Guid voucherId, VoucherService service, CancellationToken ct) =>
            Results.Ok(new
            {
                redemptions = await service.GetHistoryAsync(propertyId, voucherId, ct),
                emailDeliveries = await service.GetEmailHistoryAsync(propertyId, voucherId, ct)
            }));

        admin.MapPost("/", async (
            Guid propertyId,
            SaveVoucherRequest request,
            ClaimsPrincipal user,
            VoucherService service,
            CancellationToken ct) =>
        {
            var (voucher, error) = await service.CreateAsync(propertyId, request, UserId(user), ct);
            return error is null
                ? Results.Created($"/api/admin/properties/{propertyId}/vouchers/{voucher!.Id}", voucher)
                : Problem(error);
        }).RequireAuthorization("ManageVouchers").AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPut("/{voucherId:guid}", async (
            Guid propertyId,
            Guid voucherId,
            SaveVoucherRequest request,
            ClaimsPrincipal user,
            VoucherService service,
            CancellationToken ct) =>
        {
            var (voucher, error) = await service.UpdateAsync(propertyId, voucherId, request, UserId(user), ct);
            return error is null ? Results.Ok(voucher) : Problem(error);
        }).RequireAuthorization("ManageVouchers").AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapDelete("/{voucherId:guid}", async (
            Guid propertyId,
            Guid voucherId,
            ClaimsPrincipal user,
            VoucherService service,
            CancellationToken ct) =>
        {
            var error = await service.ArchiveAsync(propertyId, voucherId, UserId(user), ct);
            return error is null ? Results.NoContent() : Problem(error);
        }).RequireAuthorization("ManageVouchers").AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/redemptions/{redemptionId:guid}/restore", async (
            Guid propertyId,
            Guid redemptionId,
            RestoreVoucherRedemptionRequest request,
            ClaimsPrincipal user,
            VoucherService service,
            CancellationToken ct) =>
        {
            var error = await service.RestoreAsync(propertyId, redemptionId, request.Reason, UserId(user), ct);
            return error is null ? Results.NoContent() : Problem(error);
        }).RequireAuthorization("ManageVouchers").AddEndpointFilter<ApiAntiforgeryFilter>();

        admin.MapPost("/{voucherId:guid}/send-email", async (
            Guid propertyId,
            Guid voucherId,
            SendVoucherEmailRequest request,
            ClaimsPrincipal user,
            VoucherService service,
            CancellationToken ct) =>
        {
            var (delivery, error) = await service.QueueEmailAsync(propertyId, voucherId, request, UserId(user), ct);
            return error is null ? Results.Accepted(null, delivery) : Problem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        app.MapPost("/api/public/vouchers/preview", async (
            [FromQuery] string? siteSlug,
            PublicVoucherPreviewRequest request,
            PublicPropertyResolver resolver,
            VoucherService service,
            CancellationToken ct) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return Results.NotFound();
            var (result, error) = await service.PreviewAsync(property.Id, request, ct);
            return error is null ? Results.Ok(result) : Problem(error);
        }).AllowAnonymous().RequireRateLimiting("public-booking").AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;

    private static IResult Problem(VoucherOperationError error)
    {
        var status = error.Code switch
        {
            "voucher_not_found" or "redemption_not_found" => StatusCodes.Status404NotFound,
            "voucher_usage_exhausted" or "voucher_customer_limit_reached" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể áp dụng voucher",
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
