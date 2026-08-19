using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public static class BookingCoreV2Endpoints
{
    private const string IdentityStorageUnavailable = "Kho CCCD không thể đọc hoặc tạo khóa mã hóa trong DataRoot. Hãy kiểm tra quyền DataRoot/security hoặc khôi phục DataRoot đầy đủ từ bản sao lưu.";

    public static void MapBookingCoreV2Endpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/booking-policy", async (
            string? siteSlug,
            PublicPropertyResolver resolver,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, cancellationToken);
            if (property is null) return Results.NotFound();
            var policy = await new BookingPolicyStore(paths, configuration).GetAsync(property.Id, cancellationToken);
            return Results.Ok(policy);
        }).AllowAnonymous().WithTags("Public Booking");

        app.MapPost("/api/public/booking-requests/{bookingId:guid}/identity-documents/{side}", async (
            Guid bookingId,
            string side,
            string? siteSlug,
            HttpRequest httpRequest,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, cancellationToken);
            if (property is null) return Results.NotFound();
            var requestKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 100)
                return Problem("request_key_required", "Thiếu khóa xác nhận của lượt đặt.", StatusCodes.Status400BadRequest);

            var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(
                x => x.PropertyId == property.Id && x.Id == bookingId &&
                     x.Source == "Website" && x.PublicRequestKey == requestKey,
                cancellationToken);
            if (booking is null) return Results.NotFound();
            if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.NoShow)
                return Problem("booking_locked", "Lượt đặt đã kết thúc nên không thể cập nhật CCCD.", StatusCodes.Status409Conflict);
            if (!httpRequest.HasFormContentType)
                return Problem("invalid_upload", "Yêu cầu tải ảnh không hợp lệ.", StatusCodes.Status400BadRequest);

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");
            if (file is null) return Problem("invalid_upload", "Vui lòng chọn ảnh CCCD.", StatusCodes.Status400BadRequest);

            var storage = new IdentityDocumentStorage(paths, configuration);
            if (!storage.IsConfigured)
                return Problem("identity_storage_unavailable", IdentityStorageUnavailable, StatusCodes.Status503ServiceUnavailable);
            var (document, error) = await storage.SaveAsync(property.Id, bookingId, side, file, cancellationToken);
            return error is null
                ? Results.Ok(document)
                : Problem("invalid_identity_document", error, StatusCodes.Status400BadRequest);
        })
        .AllowAnonymous()
        .AddEndpointFilter<ApiAntiforgeryFilter>()
        .RequireRateLimiting("public-booking")
        .WithTags("Public Booking");

        var policyGroup = app.MapGroup("/api/admin/properties/{propertyId:guid}/booking-policy")
            .RequireAuthorization("ManageRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Booking Policy");

        policyGroup.MapGet("/", async (
            Guid propertyId,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
            Results.Ok(await new BookingPolicyStore(paths, configuration).GetAsync(propertyId, cancellationToken)));

        policyGroup.MapPut("/", async (
            Guid propertyId,
            UpdateBookingPolicyRequest request,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var (policy, error) = await new BookingPolicyStore(paths, configuration).SaveAsync(propertyId, request, cancellationToken);
            return error is null ? Results.Ok(policy) : Problem("booking_policy_invalid", error, StatusCodes.Status400BadRequest);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        var identityGroup = app.MapGroup("/api/admin/properties/{propertyId:guid}/bookings/{bookingId:guid}/identity-documents")
            .RequireAuthorization("ManageBookings")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Booking Identity Documents");

        identityGroup.MapGet("/", async (
            Guid propertyId,
            Guid bookingId,
            AppDbContext db,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Bookings.AsNoTracking().AnyAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken))
                return Results.NotFound();
            var storage = new IdentityDocumentStorage(paths, configuration);
            if (!storage.IsConfigured)
                return Problem("identity_storage_unavailable", IdentityStorageUnavailable, StatusCodes.Status503ServiceUnavailable);
            var documents = await storage.ListAsync(propertyId, bookingId, cancellationToken);
            return Results.Ok(new { configured = true, documents });
        });

        identityGroup.MapGet("/{side}", async (
            Guid propertyId,
            Guid bookingId,
            string side,
            HttpContext httpContext,
            AppDbContext db,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Bookings.AsNoTracking().AnyAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken))
                return Results.NotFound();
            var storage = new IdentityDocumentStorage(paths, configuration);
            if (!storage.IsConfigured)
                return Problem("identity_storage_unavailable", IdentityStorageUnavailable, StatusCodes.Status503ServiceUnavailable);
            IdentityDocumentReadResult? document;
            try
            {
                document = await storage.ReadAsync(propertyId, bookingId, side, cancellationToken);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return Problem("identity_document_corrupt", "Không thể giải mã ảnh CCCD. Vui lòng kiểm tra file lưu trữ và master key trong DataRoot/security.", StatusCodes.Status500InternalServerError);
            }
            if (document is null) return Results.NotFound();
            httpContext.Response.Headers.CacheControl = "private,no-store,max-age=0";
            httpContext.Response.Headers.Pragma = "no-cache";
            return Results.File(document.Bytes, document.ContentType, enableRangeProcessing: false);
        });
    }

    private static IResult Problem(string code, string detail, int status) => Results.Problem(
        type: $"https://delong.local/problems/{code}",
        title: status >= 500 ? "Không thể xử lý dữ liệu bảo mật" : "Không thể xử lý yêu cầu",
        detail: detail,
        statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
