using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingLookupRequest
{
    public string Code { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

public sealed record PublicBookingLookupDto(
    string Code,
    string Status,
    string StatusLabel,
    string CustomerName,
    string RoomName,
    string BookingType,
    string CheckInLocal,
    string CheckOutLocal,
    int? NightCount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    string PropertyName,
    string PropertyPhone,
    string PropertyAddress,
    string? GuestGuideHtml);

public sealed record PublicBookingGuideDto(string Code, string RoomName, string? GuestGuideHtml);

public sealed class PublicBookingLookupService(AppDbContext db, PublicPropertyResolver? resolver = null)
{
    private readonly PublicPropertyResolver publicPropertyResolver = resolver ?? new PublicPropertyResolver(db);

    public Task<PublicBookingLookupDto?> LookupAsync(string rawCode, string rawPhone, CancellationToken ct = default) =>
        LookupAsync(null, rawCode, rawPhone, ct);

    public async Task<PublicBookingLookupDto?> LookupAsync(string? siteSlug, string rawCode, string rawPhone, CancellationToken ct = default)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return null;

        var code = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
        var phone = CustomerService.NormalizePhone(rawPhone ?? string.Empty);
        if (code.Length is < 8 or > 50 || phone.Length < 8) return null;

        var booking = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.Code == code && x.Customer.NormalizedPhone == phone &&
                        x.Status != BookingStatus.Completed && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => new
            {
                x.Code,
                x.Status,
                x.Type,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.NightCount,
                x.RoomAmount,
                x.ExtraAmount,
                x.DiscountAmount,
                CustomerName = x.Customer.Name,
                RoomName = x.Room.Name,
                x.Room.GuestGuideHtml,
                PropertyName = x.Property.Name,
                x.Property.TimeZoneId,
                Payments = x.Payments.Where(p => !p.IsVoided).Select(p => new { p.Type, p.Amount }).ToList()
            })
            .SingleOrDefaultAsync(ct);
        if (booking is null) return null;

        var site = await db.Set<PropertySiteSettings>().AsNoTracking()
            .Where(x => x.PropertyId == property.Id)
            .Select(x => new { x.Phone, x.Address, x.SiteName })
            .SingleOrDefaultAsync(ct);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(booking.TimeZoneId);
        var checkIn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), tz);
        var checkOut = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), tz);
        var total = booking.RoomAmount + booking.ExtraAmount - booking.DiscountAmount;
        var paid = booking.Payments.Sum(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount);

        return new PublicBookingLookupDto(
            booking.Code,
            booking.Status.ToString(),
            StatusLabel(booking.Status),
            booking.CustomerName,
            booking.RoomName,
            booking.Type == BookingType.MultiDay ? "Lưu trú nhiều ngày" : "Theo khung giờ",
            checkIn.ToString("yyyy-MM-dd'T'HH:mm:ss"),
            checkOut.ToString("yyyy-MM-dd'T'HH:mm:ss"),
            booking.NightCount,
            total,
            paid,
            total - paid,
            string.IsNullOrWhiteSpace(site?.SiteName) ? booking.PropertyName : site.SiteName,
            site?.Phone ?? string.Empty,
            site?.Address ?? string.Empty,
            booking.GuestGuideHtml);
    }

    public async Task<PublicBookingGuideDto?> GetSuccessGuideAsync(
        string? siteSlug,
        string rawCode,
        CancellationToken ct = default)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return null;
        var code = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 8 or > 50) return null;
        return await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.Code == code &&
                        x.Status != BookingStatus.Completed && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => new PublicBookingGuideDto(x.Code, x.Room.Name, x.Room.GuestGuideHtml))
            .SingleOrDefaultAsync(ct);
    }

    public static string StatusLabel(BookingStatus status) => status switch
    {
        BookingStatus.Requested => "Đã gửi yêu cầu",
        BookingStatus.Held => "Đang giữ phòng",
        BookingStatus.Confirmed => "Đã xác nhận",
        BookingStatus.CheckedIn => "Đang lưu trú",
        BookingStatus.Completed => "Hoàn tất",
        BookingStatus.Cancelled => "Đã hủy",
        BookingStatus.NoShow => "Không đến",
        _ => "Đang xử lý"
    };
}
