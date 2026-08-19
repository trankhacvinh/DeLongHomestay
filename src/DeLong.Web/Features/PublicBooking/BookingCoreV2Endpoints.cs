using System.Net.Mail;
using System.Text.RegularExpressions;
using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
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

        var bookingDetailsGroup = app.MapGroup("/api/admin/properties/{propertyId:guid}/bookings/{bookingId:guid}/guest-details")
            .RequireAuthorization("ManageBookings")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Booking Guest Details");

        bookingDetailsGroup.MapGet("/", async (
            Guid propertyId,
            Guid bookingId,
            AppDbContext db,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var booking = await db.Bookings
                .Include(x => x.Customer)
                .Include(x => x.Room)
                .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
            if (booking is null) return Results.NotFound();

            var (details, cleanNote) = await LoadOrMigrateGuestDetailsAsync(db, paths, booking, cancellationToken);
            var storage = new IdentityDocumentStorage(paths, configuration);
            IReadOnlyList<IdentityDocumentInfo> documents = [];
            if (storage.IsConfigured)
            {
                try
                {
                    documents = await storage.ListAsync(propertyId, bookingId, cancellationToken);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    return Problem("identity_document_corrupt", "Không thể giải mã ảnh CCCD. Vui lòng kiểm tra file lưu trữ và master key trong DataRoot/security.", StatusCodes.Status500InternalServerError);
                }
            }

            return Results.Ok(new
            {
                customerEmail = booking.Customer.Email ?? string.Empty,
                guestCount = details.GuestCount,
                maxGuests = booking.Room.Capacity,
                policyAccepted = details.PolicyAccepted,
                policyVersion = details.PolicyVersion,
                policyAcceptedAtUtc = details.PolicyAcceptedAtUtc,
                note = cleanNote,
                identityConfigured = storage.IsConfigured,
                documents
            });
        });

        bookingDetailsGroup.MapPut("/", async (
            Guid propertyId,
            Guid bookingId,
            UpdateAdminBookingGuestDetailsRequest request,
            AppDbContext db,
            StoragePaths paths,
            CancellationToken cancellationToken) =>
        {
            var booking = await db.Bookings
                .Include(x => x.Customer)
                .Include(x => x.Room)
                .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
            if (booking is null) return Results.NotFound();
            if (request.GuestCount < 1 || request.GuestCount > booking.Room.Capacity)
                return Problem("guest_limit", $"Phòng này tối đa {booking.Room.Capacity} khách.", StatusCodes.Status400BadRequest);

            var email = CleanEmail(request.CustomerEmail);
            if (email is not null && !IsValidEmail(email))
                return Problem("email_invalid", "Email khách không hợp lệ.", StatusCodes.Status400BadRequest);

            var (current, cleanNote) = await LoadOrMigrateGuestDetailsAsync(db, paths, booking, cancellationToken);
            booking.Customer.Email = email;
            booking.Note = cleanNote;
            await db.SaveChangesAsync(cancellationToken);

            var updated = current with { GuestCount = request.GuestCount };
            await new BookingGuestDetailsStore(paths).SaveAsync(propertyId, bookingId, updated, cancellationToken);
            return Results.Ok(new
            {
                customerEmail = booking.Customer.Email ?? string.Empty,
                guestCount = updated.GuestCount,
                maxGuests = booking.Room.Capacity,
                policyAccepted = updated.PolicyAccepted,
                policyVersion = updated.PolicyVersion,
                policyAcceptedAtUtc = updated.PolicyAcceptedAtUtc,
                note = booking.Note
            });
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
            try
            {
                var documents = await storage.ListAsync(propertyId, bookingId, cancellationToken);
                return Results.Ok(new { configured = true, documents });
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return Problem("identity_document_corrupt", "Không thể giải mã ảnh CCCD. Vui lòng kiểm tra file lưu trữ và master key trong DataRoot/security.", StatusCodes.Status500InternalServerError);
            }
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

        identityGroup.MapPost("/{side}", async (
            Guid propertyId,
            Guid bookingId,
            string side,
            HttpRequest httpRequest,
            AppDbContext db,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Bookings.AsNoTracking().AnyAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken))
                return Results.NotFound();
            if (!httpRequest.HasFormContentType)
                return Problem("invalid_upload", "Yêu cầu tải ảnh không hợp lệ.", StatusCodes.Status400BadRequest);
            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");
            if (file is null) return Problem("invalid_upload", "Vui lòng chọn ảnh CCCD.", StatusCodes.Status400BadRequest);

            var storage = new IdentityDocumentStorage(paths, configuration);
            if (!storage.IsConfigured)
                return Problem("identity_storage_unavailable", IdentityStorageUnavailable, StatusCodes.Status503ServiceUnavailable);
            var (document, error) = await storage.SaveAsync(propertyId, bookingId, side, file, cancellationToken);
            return error is null
                ? Results.Ok(document)
                : Problem("invalid_identity_document", error, StatusCodes.Status400BadRequest);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        identityGroup.MapDelete("/{side}", async (
            Guid propertyId,
            Guid bookingId,
            string side,
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
            try
            {
                var path = storage.GetEncryptedPathForDiagnostics(propertyId, bookingId, side);
                if (File.Exists(path)) File.Delete(path);
                return Results.NoContent();
            }
            catch (ArgumentException)
            {
                return Problem("invalid_identity_side", "Mặt giấy tờ không hợp lệ.", StatusCodes.Status400BadRequest);
            }
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
    }

    private static async Task<(BookingGuestDetailsDto Details, string? CleanNote)> LoadOrMigrateGuestDetailsAsync(
        AppDbContext db,
        StoragePaths paths,
        Booking booking,
        CancellationToken cancellationToken)
    {
        var store = new BookingGuestDetailsStore(paths);
        var stored = await store.GetAsync(booking.PropertyId, booking.Id, cancellationToken);
        if (stored is not null) return (stored, booking.Note);

        var legacy = string.Equals(booking.Source, "Website", StringComparison.OrdinalIgnoreCase)
            ? ParseLegacyWebNote(booking.Note)
            : null;
        if (legacy is not null)
        {
            var migrated = new BookingGuestDetailsDto(
                legacy.GuestCount,
                legacy.PolicyAccepted,
                legacy.PolicyVersion,
                legacy.PolicyAccepted ? booking.CreatedAtUtc : null);
            booking.Note = legacy.CleanNote;
            await db.SaveChangesAsync(cancellationToken);
            await store.SaveAsync(booking.PropertyId, booking.Id, migrated, cancellationToken);
            return (migrated, booking.Note);
        }

        return (new BookingGuestDetailsDto(1, false, null, null), booking.Note);
    }

    private static LegacyWebNote? ParseLegacyWebNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var first = lines[0].Trim();
        if (!first.StartsWith("[Đặt web]", StringComparison.OrdinalIgnoreCase)) return null;

        var guestMatch = Regex.Match(first, @"\[Đặt web\]\s*(?<count>\d+)\s*khách", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var versionMatch = Regex.Match(first, @"\bv(?<version>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var guestCount = guestMatch.Success && int.TryParse(guestMatch.Groups["count"].Value, out var parsedCount) ? Math.Max(1, parsedCount) : 1;
        int? policyVersion = versionMatch.Success && int.TryParse(versionMatch.Groups["version"].Value, out var parsedVersion) ? Math.Max(1, parsedVersion) : null;
        var policyAccepted = first.Contains("đồng ý", StringComparison.OrdinalIgnoreCase);
        var cleanNote = string.Join(Environment.NewLine, lines.Skip(1)).Trim();
        return new LegacyWebNote(guestCount, policyAccepted, policyVersion, string.IsNullOrWhiteSpace(cleanNote) ? null : cleanNote);
    }

    private static string? CleanEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidEmail(string value)
    {
        if (value.Length > 254) return false;
        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IResult Problem(string code, string detail, int status) => Results.Problem(
        type: $"https://delong.local/problems/{code}",
        title: status >= 500 ? "Không thể xử lý dữ liệu bảo mật" : "Không thể xử lý yêu cầu",
        detail: detail,
        statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record LegacyWebNote(int GuestCount, bool PolicyAccepted, int? PolicyVersion, string? CleanNote);
}
