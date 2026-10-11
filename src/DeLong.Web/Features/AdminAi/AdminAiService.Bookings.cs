using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.Rooms;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed partial class AdminAiService
{
    private sealed record BookingProposalInput(string RoomReference, string CustomerName, string CustomerPhone,
        string CheckIn, string CheckOut, string? RateReference, decimal? RoomAmount, string? Source, string? Note);
    private sealed record PreparedBookingPayload(CreateBookingRequest PreparedBooking, string RoomName, string TimeZoneId, Guid? CreatedBookingId = null, string? CreatedBookingCode = null);

    private async Task<(AiOperationEnvelope? Value, string? Error)> PrepareBookingAsync(Guid propertyId,
        AiOperationEnvelope operation, CancellationToken ct)
    {
        var input = operation.Payload.Deserialize<BookingProposalInput>(Json);
        if (input is null || string.IsNullOrWhiteSpace(input.RoomReference) || string.IsNullOrWhiteSpace(input.CustomerName) ||
            string.IsNullOrWhiteSpace(input.CustomerPhone) || DeLong.Web.Features.Customers.CustomerService.NormalizePhone(input.CustomerPhone).Length < 8) return (null, "Cần phòng, tên khách và số điện thoại để tạo đơn.");
        if (!TryBookingTime(input.CheckIn, out var checkIn) || !TryBookingTime(input.CheckOut, out var checkOut) || checkOut <= checkIn)
            return (null, "Cần ngày giờ nhận/trả rõ ràng, có offset múi giờ; giờ trả phải sau giờ nhận.");
        if (input.CustomerName.Length > 160 || input.CustomerPhone.Length > 30 ||
            input.Note?.Length > 2000 || input.Source?.Length > 100 || input.RoomAmount is < 0 or > 1_000_000_000m)
            return (null, "Tên, số điện thoại, ghi chú hoặc số tiền không hợp lệ.");
        var rooms = await db.Rooms.AsNoTracking().Include(x => x.Rates)
            .Where(x => x.PropertyId == propertyId && x.IsActive).ToListAsync(ct);
        var candidates = rooms.Where(x => x.Id.ToString().Equals(input.RoomReference, StringComparison.OrdinalIgnoreCase) ||
            x.Code.Equals(input.RoomReference, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(input.RoomReference, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length != 1) return (null, "Không xác định duy nhất phòng; hãy dùng đúng mã phòng của chi nhánh.");
        var room = candidates[0];
        var availabilityError = await BookingProposalAvailabilityAsync(propertyId, room, checkIn.UtcDateTime, checkOut.UtcDateTime, ct);
        if (availabilityError is not null) return (null, availabilityError);
        var zoneId = await db.Properties.AsNoTracking().Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var localIn = TimeZoneInfo.ConvertTime(checkIn, zone);
        var localOut = TimeZoneInfo.ConvertTime(checkOut, zone);
        RoomRate? rate = null;
        decimal amount, surcharge = 0;
        if (!string.IsNullOrWhiteSpace(input.RateReference))
        {
            var rates = room.Rates.Where(x => x.IsActive && (x.Id.ToString().Equals(input.RateReference, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Equals(input.RateReference, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (rates.Length != 1) return (null, "Không xác định duy nhất khung giá; hãy dùng tên/id khung trong dữ liệu.");
            rate = rates[0];
            var nominalStart = localIn.Date.Add(rate.StartTime.ToTimeSpan());
            // A post-midnight remainder belongs to the preceding overnight service date.
            if (rate.EndTime <= rate.StartTime && localIn.TimeOfDay < rate.EndTime.ToTimeSpan()) nominalStart = nominalStart.AddDays(-1);
            var nominalEnd = nominalStart.Date.Add(rate.EndTime.ToTimeSpan());
            if (rate.EndTime <= rate.StartTime) nominalEnd = nominalEnd.AddDays(1);
            if (localIn.DateTime < nominalStart || localOut.DateTime > nominalEnd)
                return (null, "Giờ đặt vượt khung giá. Với nhiều khung/cả ngày, cần nhập rõ tổng giá thỏa thuận và bỏ rateReference.");
            var quote = await pricingService.CalculateAsync(propertyId,
                [new(DateOnly.FromDateTime(nominalStart), rate.SortOrder, rate.Id, rate.Name,
                    rate.Price, rate.UseWeekdayPriceOnWeekend, rate.WeekendPrice)], room.Rates.Count(x => x.IsActive),
                room.FullDayPricingEnabled, room.FullDayPrice, room.UseWeekdayFullDayPriceOnWeekend, room.WeekendFullDayPrice, ct);
            if (quote.Error is not null) return (null, quote.Error.Message);
            amount = input.RoomAmount ?? quote.Result!.RoomAmount;
            surcharge = quote.Result!.SpecialSurchargeAmount;
        }
        else if (input.RoomAmount is { } price) amount = price;
        else return (null, "Cần khung giá hoặc số tiền thỏa thuận; không tự đoán giá cho giờ tùy chỉnh.");
        var request = new CreateBookingRequest {
            RoomId = room.Id, CustomerName = input.CustomerName.Trim(), CustomerPhone = input.CustomerPhone.Trim(),
            CheckIn = checkIn, CheckOut = checkOut, Status = BookingStatus.Confirmed,
            RoomRateId = rate?.Id, RateName = rate?.Name, RoomAmount = amount, SpecialSurchargeAmount = surcharge,
            Type = BookingType.TimeSlot,
            Source = input.Source?.Trim() ?? "AI quản trị", Note = input.Note?.Trim()
        };
        var summary = $"Tạo đơn {room.Name} cho {request.CustomerName} ({request.CustomerPhone}): " +
            $"{localIn:dd/MM/yyyy HH:mm} → {localOut:dd/MM/yyyy HH:mm}; tiền phòng {amount:N0}đ, phụ thu {surcharge:N0}đ. " +
            "Trạng thái xác nhận, chưa ghi nhận thanh toán. Giá khung giữ nguyên khi chỉ còn một phần thời gian; nhân viên kiểm tra trước khi duyệt.";
        return (operation with { Summary = summary, Payload = JsonSerializer.SerializeToElement(new PreparedBookingPayload(request, room.Name, zoneId), Json) }, null);
    }

    private async Task<string?> BookingProposalAvailabilityAsync(Guid propertyId, Room room, DateTime start, DateTime end, CancellationToken ct)
    {
        if (room.IsBookingLocked || await RoomBookingGuard.HasScheduledLockAsync(db, propertyId, room.Id, start, end, ct))
            return "Phòng đang khóa trong khoảng đã chọn; cần chọn phòng hoặc giờ khác.";
        var occupied = await db.Bookings.AsNoTracking().AnyAsync(x => x.PropertyId == propertyId && x.RoomId == room.Id &&
            (x.Status == BookingStatus.Held || x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.CheckedIn || x.Status == BookingStatus.Completed) &&
            x.CheckInUtc < end && start < x.CheckOutUtc, ct);
        return occupied ? "Khoảng thời gian đã có khách. Hãy chọn giờ hoặc phòng khác." : null;
    }

    private async Task<string?> ExecuteBookingProposalAsync(AiChangeProposal proposal, Guid actor, CancellationToken ct)
    {
        if (bookingService is null) return "Chức năng tạo booking chưa được cấu hình.";
        var payload = JsonSerializer.Deserialize<PreparedBookingPayload>(proposal.PayloadJson, Json);
        if (payload is null) return "Bản xem trước booking không hợp lệ.";
        // The existing BookingService rechecks property, customer, room locks and overlaps under its row lock.
        var result = await bookingService.CreateAsync(proposal.PropertyId, payload.PreparedBooking, actor, ct);
        if (result.Error is not null) return result.Error.Message;
        proposal.PayloadJson = JsonSerializer.Serialize(payload with { CreatedBookingId = result.Booking!.Id, CreatedBookingCode = result.Booking.Code }, Json);
        return null;
    }

    private static bool TryBookingTime(string? value, out DateTimeOffset parsed)
    {
        parsed = default;
        return value is not null && Regex.IsMatch(value, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }
}
