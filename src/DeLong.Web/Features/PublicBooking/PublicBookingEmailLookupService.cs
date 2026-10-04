using System.Net.Mail;
using System.Text;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingEmailLookupRequest
{
    public string Email { get; init; } = string.Empty;
}

public sealed class PublicBookingEmailLookupService(AppDbContext db, PublicPropertyResolver resolver)
{
    public const string ResponseMessage = "Nếu email này có đơn đặt phòng, thông tin tra cứu sẽ được gửi về hộp thư. Vui lòng kiểm tra cả thư rác.";

    public static bool IsValidEmail(string? value) => value is { Length: <= 320 } &&
        MailAddress.TryCreate(value.Trim(), out var address) &&
        string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);

    public async Task QueueAsync(string? siteSlug, string email, CancellationToken ct = default)
    {
        var property = await resolver.ResolveAsync(siteSlug, ct);
        if (property is null) return;
        var recipient = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-5);
        if (await db.BookingGuestGuideEmails.AsNoTracking().AnyAsync(x =>
                x.PropertyId == property.Id && x.TemplateKey == "BookingLookup" &&
                x.RecipientEmail == recipient && x.CreatedAtUtc >= cutoff, ct)) return;

        var bookings = await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.Customer.Email != null &&
                        x.Customer.Email.Trim().ToLower() == recipient)
            .OrderByDescending(x => x.CreatedAtUtc).Take(20)
            .Select(x => new
            {
                x.Id, x.Code, x.Status, x.CheckInUtc, x.CheckOutUtc, x.Note,
                RoomName = x.Room.Name,
                Total = x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount,
                Paid = x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount)
            }).ToListAsync(ct);
        if (bookings.Count == 0) return;

        var zone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var body = new StringBuilder($"Thông tin đặt phòng tại {property.Name}\nTối đa 20 đơn gần nhất; thời gian theo giờ địa phương của cơ sở.\n");
        foreach (var booking in bookings)
        {
            var checkIn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), zone);
            var checkOut = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), zone);
            body.AppendLine($"\nMã đặt phòng: {booking.Code}\nPhòng: {booking.RoomName}\nTrạng thái: {PublicBookingLookupService.StatusLabel(booking.Status)}\nNhận phòng: {checkIn:HH:mm dd/MM/yyyy}\nTrả phòng: {checkOut:HH:mm dd/MM/yyyy}\nTổng tiền: {booking.Total:N0} VND\nĐã thanh toán: {booking.Paid:N0} VND\nCòn lại: {booking.Total - booking.Paid:N0} VND");
            var adjustment = (booking.Note ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries)
                .FirstOrDefault(x => x.StartsWith("Điều chỉnh thời gian", StringComparison.Ordinal));
            if (adjustment is not null) body.AppendLine(adjustment);
        }
        db.BookingGuestGuideEmails.Add(new BookingGuestGuideEmail
        {
            PropertyId = property.Id, BookingId = bookings[0].Id, RecipientEmail = recipient,
            Trigger = "PublicEmailLookup", TemplateKey = "BookingLookup",
            Subject = "Thông tin tra cứu đặt phòng", BodyText = body.ToString(), NextAttemptAtUtc = now
        });
        await db.SaveChangesAsync(ct);
    }
}
