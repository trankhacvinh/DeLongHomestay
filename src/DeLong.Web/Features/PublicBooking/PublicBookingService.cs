using System.Globalization;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.Pricing;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingService(AppDbContext db, BookingService bookingService, PublicPropertyResolver? resolver = null, BookingNotificationService? notificationService = null, PricingService? pricingService = null)
{
    private readonly PublicPropertyResolver publicPropertyResolver = resolver ?? new PublicPropertyResolver(db);
    private static readonly BookingStatus[] LockingStatuses = [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];
    private sealed record BookingConflictWindow(Guid RoomId, DateTime CheckInUtc, DateTime CheckOutUtc);
    private sealed class ResolvedPublicSlot(
        DateOnly serviceDate,
        int rateIndex,
        Guid rateId,
        string rateName,
        decimal listPrice,
        DateTime checkInUtc,
        DateTime checkOutUtc)
    {
        public DateOnly ServiceDate { get; } = serviceDate;
        public int RateIndex { get; } = rateIndex;
        public Guid RateId { get; } = rateId;
        public string RateName { get; } = rateName;
        public decimal ListPrice { get; set; } = listPrice;
        public DateTime CheckInUtc { get; } = checkInUtc;
        public DateTime CheckOutUtc { get; } = checkOutUtc;
        public decimal AppliedAmount { get; set; } = listPrice;
        public decimal SpecialSurchargeAmount { get; set; }
        public decimal ComboDiscountPercent { get; set; }
        public decimal ComboDiscountAmount { get; set; }
        public PricingDayProfile DayProfile { get; set; } = PricingDayProfile.Weekday;
        public Guid? SpecialPricingDayId { get; set; }
        public string? SpecialDayName { get; set; }
        public decimal SpecialSurchargePercent { get; set; }
        public string PricingRule { get; set; } = "standard";
    }

    public Task<PublicCatalogDto?> GetCatalogAsync(DateOnly? availabilityDate = null, CancellationToken cancellationToken = default) =>
        GetCatalogAsync(null, availabilityDate, cancellationToken);

    public async Task<PublicCatalogDto?> GetCatalogAsync(string? siteSlug, DateOnly? availabilityDate = null, CancellationToken cancellationToken = default)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return null;
        HashSet<(Guid RoomId, Guid RateId)> unavailable = availabilityDate.HasValue ? await GetUnavailableRateKeysAsync(property.Id, property.TimeZoneId, availabilityDate.Value, cancellationToken) : [];
        var rooms = await db.Rooms.AsNoTracking().Where(x => x.PropertyId == property.Id && x.IsActive && x.IsPublished).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Capacity, x.FullDayPricingEnabled, x.FullDayPrice, Rates = x.Rates.Where(r => r.IsActive).OrderBy(r => r.SortOrder).Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.IsOvernight, r.Price }).ToList() }).ToListAsync(cancellationToken);
        var roomDtos = rooms.Select(room =>
        {
            var rates = room.Rates.Select(rate => new PublicRateDto(rate.Id, rate.Name, rate.StartTime.ToString("HH:mm"), rate.EndTime.ToString("HH:mm"), rate.Type, rate.IsOvernight, rate.Price, !availabilityDate.HasValue || !unavailable.Contains((room.Id, rate.Id)))).ToList();
            var publicPrices = rates.Where(r => r.Price > 0).Select(r => r.Price).ToList();
            return new PublicRoomDto(room.Id, room.Code, room.Name, room.Capacity, HasBathtub(room.Code), publicPrices.Count == 0 ? 0 : publicPrices.Min(), room.FullDayPricingEnabled, room.FullDayPrice, rates);
        }).ToList();
        return new PublicCatalogDto(property.Id, property.Name, property.TimeZoneId, roomDtos);
    }

    public async Task<PublicRoomDto?> GetRoomAsync(string code, CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(null, null, cancellationToken))?.Rooms.SingleOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    public async Task<PublicRoomDto?> GetRoomAsync(string? siteSlug, string code, CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(siteSlug, null, cancellationToken))?.Rooms.SingleOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    public Task<PublicAvailabilityDto?> GetAvailabilityAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        GetAvailabilityAsync(null, date, cancellationToken);

    public async Task<PublicAvailabilityDto?> GetAvailabilityAsync(string? siteSlug, DateOnly date, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(siteSlug, date, cancellationToken);
        return catalog is null ? null : new(date.ToString("yyyy-MM-dd"), catalog.Rooms);
    }

    public Task<(PublicStayAvailabilityDto? Availability, PublicBookingError? Error)> GetStayAvailabilityAsync(DateOnly checkInDate, DateOnly checkOutDate, CancellationToken cancellationToken = default) =>
        GetStayAvailabilityAsync(null, checkInDate, checkOutDate, cancellationToken);

    public async Task<(PublicStayAvailabilityDto? Availability, PublicBookingError? Error)> GetStayAvailabilityAsync(string? siteSlug, DateOnly checkInDate, DateOnly checkOutDate, CancellationToken cancellationToken = default)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return (null, new("property_not_found", "Cơ sở hiện không khả dụng."));
        var validation = ValidateStayDates(checkInDate, checkOutDate, property.TimeZoneId); if (validation is not null) return (null, validation);
        var nights = checkOutDate.DayNumber - checkInDate.DayNumber; var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var rates = await db.RoomRates.AsNoTracking().Where(r => r.Room.PropertyId == property.Id && r.Room.IsActive && r.Room.IsPublished && r.IsActive && r.Type == RoomRateType.Nightly && r.Price > 0)
            .OrderBy(r => r.Room.SortOrder).ThenBy(r => r.SortOrder).Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Price, RoomId = r.Room.Id, RoomCode = r.Room.Code, RoomName = r.Room.Name, r.Room.Capacity }).ToListAsync(cancellationToken);
        var roomRates = rates.GroupBy(r => r.RoomId).Select(g => g.First()).ToList();
        var candidates = roomRates.Select(rate =>
        {
            var (checkInUtc, checkOutUtc) = ToUtcStayRange(checkInDate, checkOutDate, rate.StartTime, rate.EndTime, timeZone);
            return new { Rate = rate, CheckInUtc = checkInUtc, CheckOutUtc = checkOutUtc };
        }).ToList();

        List<BookingConflictWindow> lockedBookings = [];
        if (candidates.Count > 0)
        {
            var roomIds = candidates.Select(x => x.Rate.RoomId).Distinct().ToArray();
            var windowStartUtc = candidates.Min(x => x.CheckInUtc);
            var windowEndUtc = candidates.Max(x => x.CheckOutUtc);
            lockedBookings = await db.Bookings.AsNoTracking()
                .Where(x => x.PropertyId == property.Id && roomIds.Contains(x.RoomId) && LockingStatuses.Contains(x.Status) &&
                            x.CheckInUtc < windowEndUtc && windowStartUtc < x.CheckOutUtc)
                .Select(x => new BookingConflictWindow(x.RoomId, x.CheckInUtc, x.CheckOutUtc))
                .ToListAsync(cancellationToken);
        }

        var results = new List<PublicStayRoomDto>();
        foreach (var candidate in candidates)
        {
            var rate = candidate.Rate;
            var conflict = lockedBookings.Any(x =>
                x.RoomId == rate.RoomId && x.CheckInUtc < candidate.CheckOutUtc && candidate.CheckInUtc < x.CheckOutUtc);
            var dto = new PublicRateDto(rate.Id, rate.Name, rate.StartTime.ToString("HH:mm"), rate.EndTime.ToString("HH:mm"), RoomRateType.Nightly, false, rate.Price, !conflict);
            results.Add(new PublicStayRoomDto(rate.RoomId, rate.RoomCode, rate.RoomName, rate.Capacity, HasBathtub(rate.RoomCode), dto, nights, rate.Price * nights, !conflict));
        }
        return (new PublicStayAvailabilityDto(checkInDate.ToString("yyyy-MM-dd"), checkOutDate.ToString("yyyy-MM-dd"), nights, results), null);
    }

    public Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(PublicBookingRequest request, CancellationToken cancellationToken = default) =>
        CreateRequestAsync(null, request, null, cancellationToken);

    public Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(PublicBookingRequest request, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        CreateRequestAsync(null, request, idempotencyKey, cancellationToken);

    public Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(string? siteSlug, PublicBookingRequest request, CancellationToken cancellationToken = default) =>
        CreateRequestAsync(siteSlug, request, null, cancellationToken);

    public async Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(string? siteSlug, PublicBookingRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
        => await CreateRequestAsync(siteSlug, request, idempotencyKey, null, cancellationToken);

    public async Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(
        string? siteSlug,
        PublicBookingRequest request,
        string? idempotencyKey,
        BookingPolicyDto? bookingPolicy,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Website)) return (null, new("spam", "Không thể gửi yêu cầu."));
        var key = NormalizeIdempotencyKey(idempotencyKey);
        if (idempotencyKey is not null && key is null) return (null, new("validation", "Idempotency-Key không hợp lệ."));
        return request.Type == BookingType.MultiDay
            ? await CreateMultiDayRequestAsync(siteSlug, request, key, cancellationToken)
            : await CreateTimeSlotRequestAsync(siteSlug, request, key, bookingPolicy, cancellationToken);
    }

    private async Task<(PublicBookingResult?, PublicBookingError?)> CreateTimeSlotRequestAsync(string? siteSlug, PublicBookingRequest request, string? idempotencyKey, BookingPolicyDto? policy, CancellationToken ct)
    {
        var context = await GetRequestContextAsync(siteSlug, request.CustomerName, request.CustomerPhone, ct); if (context.Error is not null) return (null, context.Error);
        if (idempotencyKey is not null && await FindIdempotentResultAsync(context.PropertyId, idempotencyKey, ct) is { } replay) return (replay, null);
        var selected = request.Slots.Count > 0
            ? request.Slots
            : [new PublicBookingSlotRequest(request.StayDate, request.RateId)];
        if (selected.Count > 60) return (null, new("validation", "Số khung giờ được chọn quá lớn."));

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.Id == request.RoomId && x.PropertyId == context.PropertyId && x.IsActive && x.IsPublished)
            .Select(x => new
            {
                x.Id, x.Name, x.FullDayPricingEnabled, x.FullDayPrice, x.UseWeekdayFullDayPriceOnWeekend, x.WeekendFullDayPrice,
                Rates = x.Rates.Where(r => r.IsActive && r.Type != RoomRateType.Nightly)
                    .OrderBy(r => r.SortOrder).ThenBy(r => r.StartTime).ThenBy(r => r.Name)
                    .Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.IsOvernight, r.Price, r.UseWeekdayPriceOnWeekend, r.WeekendPrice }).ToList()
            }).SingleOrDefaultAsync(ct);
        if (room is null || room.Rates.Count == 0) return (null, new("rate_not_found", "Khung giờ hoặc phòng không còn khả dụng."));

        var rateIndexes = room.Rates.Select((rate, index) => (rate.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var resolved = new List<ResolvedPublicSlot>(selected.Count);
        foreach (var slot in selected)
        {
            if (!DateOnly.TryParseExact(slot.StayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return (null, new("validation", "Ngày đặt phòng không hợp lệ."));
            if (ValidateDateWindow(date, context.TodayLocal) is { } dateError) return (null, dateError);
            if (!rateIndexes.TryGetValue(slot.RateId, out var rateIndex))
                return (null, new("rate_not_found", "Một khung giờ đã ngừng áp dụng."));
            var rate = room.Rates[rateIndex];
            var (startUtc, endUtc) = ToUtcTimeSlotRange(date, rate.StartTime, rate.EndTime, rate.Type == RoomRateType.Overnight || rate.IsOvernight, context.TimeZone);
            resolved.Add(new ResolvedPublicSlot(date, rateIndex, rate.Id, rate.Name, rate.Price, startUtc, endUtc));
        }

        if (resolved.Select(x => (x.ServiceDate, x.RateId)).Distinct().Count() != resolved.Count)
            return (null, new("validation", "Danh sách khung giờ bị trùng."));
        var submittedOrder = resolved.Select(x => (x.ServiceDate, x.RateId)).ToList();
        resolved = resolved.OrderBy(x => x.ServiceDate).ThenBy(x => x.RateIndex).ToList();
        if (!submittedOrder.SequenceEqual(resolved.Select(x => (x.ServiceDate, x.RateId))))
            return (null, new("slots_not_consecutive", "Các khung giờ chỉ được chọn theo thứ tự về phía sau."));
        var maxDays = policy?.PublicMaxConsecutiveSlotDays ?? 3;
        var selectionError = PublicSlotSelectionRules.ValidateConsecutive(
            resolved.Select(x => (x.ServiceDate, x.RateIndex)).ToList(), room.Rates.Count, maxDays);
        if (selectionError is not null) return (null, selectionError);

        if (pricingService is not null)
        {
            var pricingInputs = resolved.Select(x =>
            {
                var rate = room.Rates[x.RateIndex];
                return new PricingSelectionInput(x.ServiceDate, x.RateIndex, x.RateId, x.RateName,
                    rate.Price, rate.UseWeekdayPriceOnWeekend, rate.WeekendPrice);
            }).ToList();
            var (pricing, pricingError) = await pricingService.CalculateAsync(
                context.PropertyId, pricingInputs, room.Rates.Count, room.FullDayPricingEnabled, room.FullDayPrice,
                room.UseWeekdayFullDayPriceOnWeekend, room.WeekendFullDayPrice, ct);
            if (pricingError is not null) return (null, new(pricingError.Code, pricingError.Message));
            for (var index = 0; index < resolved.Count; index++)
            {
                var segment = pricing!.Segments[index];
                resolved[index].ListPrice = segment.ListPrice;
                resolved[index].AppliedAmount = segment.AppliedAmount;
                resolved[index].SpecialSurchargeAmount = segment.SpecialSurchargeAmount;
                resolved[index].ComboDiscountPercent = segment.ComboDiscountPercent;
                resolved[index].ComboDiscountAmount = segment.ComboDiscountAmount;
                resolved[index].DayProfile = segment.DayProfile;
                resolved[index].SpecialPricingDayId = segment.SpecialPricingDayId;
                resolved[index].SpecialDayName = segment.SpecialDayName;
                resolved[index].SpecialSurchargePercent = segment.SpecialSurchargePercent;
                resolved[index].PricingRule = segment.PricingRule;
            }
        }
        else
        {
            ApplyPricing(resolved, room.Rates.Count, room.FullDayPricingEnabled, room.FullDayPrice, policy?.MultiSlotDiscountTiers ?? []);
        }
        var checkInUtc = resolved[0].CheckInUtc;
        var checkOutUtc = resolved[^1].CheckOutUtc;
        if (await bookingService.HasConflictAsync(context.PropertyId, room.Id, checkInUtc, checkOutUtc, null, ct)) return (null, new("booking_conflict", "Phòng vừa được giữ hoặc xác nhận trong khoảng thời gian này. Vui lòng chọn lại."));
        var amount = resolved.Sum(x => x.AppliedAmount);
        var specialSurchargeAmount = resolved.Sum(x => x.SpecialSurchargeAmount);
        var rateLabel = resolved.Count == 1 ? resolved[0].RateName : $"{resolved.Count} khung liên tiếp";
        var segments = resolved.Select((x, index) => new CreateBookingRateSegmentRequest(
            x.RateId, x.ServiceDate, x.CheckInUtc, x.CheckOutUtc, index, x.RateName, x.ListPrice, x.AppliedAmount, x.PricingRule,
            x.SpecialSurchargeAmount, x.ComboDiscountPercent, x.ComboDiscountAmount, x.DayProfile, x.SpecialPricingDayId,
            x.SpecialDayName, x.SpecialSurchargePercent)).ToList();
        var (booking, error) = await bookingService.CreateAsync(context.PropertyId, new CreateBookingRequest { RoomId = room.Id, CustomerName = context.Name, CustomerPhone = context.Phone, Type = BookingType.TimeSlot, RoomRateId = resolved.Count == 1 ? resolved[0].RateId : null, RateName = rateLabel, UnitPrice = resolved.Count == 1 ? resolved[0].ListPrice : null, CheckIn = new DateTimeOffset(checkInUtc, TimeSpan.Zero), CheckOut = new DateTimeOffset(checkOutUtc, TimeSpan.Zero), Status = BookingStatus.Held, RoomAmount = amount, SpecialSurchargeAmount = specialSurchargeAmount, Source = "Website", PublicRequestKey = idempotencyKey, Note = Clean(request.Note), RateSegments = segments }, null, ct);
        if (booking is null && error?.Code == "public_request_retry" && idempotencyKey is not null && db.Database.CurrentTransaction is null)
        {
            db.ChangeTracker.Clear();
            if (await FindIdempotentResultAsync(context.PropertyId, idempotencyKey, ct) is { } idempotentReplay1) return (idempotentReplay1, null);
        }
        if (booking is null) return (null, new(error?.Code ?? "booking_failed", error?.Message ?? "Không thể tạo yêu cầu đặt phòng."));
        if (notificationService is not null) await notificationService.NotifyBookingCreatedAsync(context.PropertyId, booking.Id, ct);
        return (new PublicBookingResult(booking.Id, booking.Code, booking.Type, room.Name, rateLabel, null, booking.CheckInUtc, booking.CheckOutUtc, booking.TotalAmount), null);
    }

    private async Task<(PublicBookingResult?, PublicBookingError?)> CreateMultiDayRequestAsync(string? siteSlug, PublicBookingRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(request.CheckInDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkInDate) || !DateOnly.TryParseExact(request.CheckOutDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkOutDate)) return (null, new("validation", "Ngày nhận hoặc ngày trả không hợp lệ."));
        var context = await GetRequestContextAsync(siteSlug, request.CustomerName, request.CustomerPhone, ct); if (context.Error is not null) return (null, context.Error);
        if (idempotencyKey is not null && await FindIdempotentResultAsync(context.PropertyId, idempotencyKey, ct) is { } replay) return (replay, null);
        var dateError = ValidateStayDates(checkInDate, checkOutDate, context.TimeZone.Id); if (dateError is not null) return (null, dateError);
        var rate = await db.RoomRates.AsNoTracking().Where(x => x.Id == request.RateId && x.RoomId == request.RoomId && x.IsActive && x.Type == RoomRateType.Nightly && x.Price > 0 && x.Room.IsActive && x.Room.IsPublished && x.Room.PropertyId == context.PropertyId)
            .Select(x => new { x.Id, x.Name, x.StartTime, x.EndTime, x.Price, RoomId = x.Room.Id, RoomName = x.Room.Name }).SingleOrDefaultAsync(ct);
        if (rate is null) return (null, new("rate_not_found", "Phòng chưa có giá lưu trú theo đêm hoặc giá đã ngừng áp dụng."));
        var nights = checkOutDate.DayNumber - checkInDate.DayNumber;
        var (checkInUtc, checkOutUtc) = ToUtcStayRange(checkInDate, checkOutDate, rate.StartTime, rate.EndTime, context.TimeZone);
        if (await bookingService.HasConflictAsync(context.PropertyId, rate.RoomId, checkInUtc, checkOutUtc, null, ct)) return (null, new("booking_conflict", "Phòng đã có lượt đặt giao với khoảng lưu trú này. Vui lòng chọn ngày hoặc phòng khác."));
        var amount = rate.Price * nights;
        var (booking, error) = await bookingService.CreateAsync(context.PropertyId, new CreateBookingRequest { RoomId = rate.RoomId, CustomerName = context.Name, CustomerPhone = context.Phone, Type = BookingType.MultiDay, RoomRateId = rate.Id, RateName = rate.Name, UnitPrice = rate.Price, NightCount = nights, CheckIn = new DateTimeOffset(checkInUtc, TimeSpan.Zero), CheckOut = new DateTimeOffset(checkOutUtc, TimeSpan.Zero), Status = BookingStatus.Held, RoomAmount = amount, Source = "Website", PublicRequestKey = idempotencyKey, Note = Clean(request.Note) }, null, ct);
        if (booking is null && error?.Code == "public_request_retry" && idempotencyKey is not null && db.Database.CurrentTransaction is null)
        {
            db.ChangeTracker.Clear();
            if (await FindIdempotentResultAsync(context.PropertyId, idempotencyKey, ct) is { } idempotentReplay2) return (idempotentReplay2, null);
        }
        if (booking is null) return (null, new(error?.Code ?? "booking_failed", error?.Message ?? "Không thể tạo yêu cầu lưu trú."));
        if (notificationService is not null) await notificationService.NotifyBookingCreatedAsync(context.PropertyId, booking.Id, ct);
        return (new PublicBookingResult(booking.Id, booking.Code, booking.Type, rate.RoomName, rate.Name, nights, booking.CheckInUtc, booking.CheckOutUtc, booking.TotalAmount), null);
    }

    private async Task<PublicBookingResult?> FindIdempotentResultAsync(Guid propertyId, string key, CancellationToken ct)
    {
        return await db.Bookings.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.PublicRequestKey == key && x.Source == "Website")
            .Select(x => new PublicBookingResult(
                x.Id, x.Code, x.Type, x.Room.Name, x.RateName ?? string.Empty, x.NightCount,
                x.CheckInUtc, x.CheckOutUtc, x.RoomAmount + x.SpecialSurchargeAmount + x.ExtraAmount - x.DiscountAmount))
            .SingleOrDefaultAsync(ct);
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = value.Trim();
        if (key.Length > 100 || key.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':'))) return null;
        return key;
    }

    private static void ApplyPricing(
        IReadOnlyList<ResolvedPublicSlot> slots,
        int ratesPerDay,
        bool fullDayPricingEnabled,
        decimal? fullDayPrice,
        IReadOnlyList<MultiSlotDiscountTierDto> tiers)
    {
        var calculated = PublicSlotPricingCalculator.Calculate(
            slots.Select(x => new PublicSlotPricingItem(x.ServiceDate, x.RateIndex, x.ListPrice)).ToList(),
            ratesPerDay, fullDayPricingEnabled, fullDayPrice, tiers);
        for (var index = 0; index < slots.Count; index++)
        {
            slots[index].AppliedAmount = calculated[index].AppliedAmount;
            slots[index].PricingRule = calculated[index].PricingRule;
        }
    }

    private async Task<(Guid PropertyId, TimeZoneInfo TimeZone, DateOnly TodayLocal, string Name, string Phone, PublicBookingError? Error)> GetRequestContextAsync(string? siteSlug, string rawName, string rawPhone, CancellationToken ct)
    {
        var property = await publicPropertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return (Guid.Empty, TimeZoneInfo.Utc, default, "", "", new("property_not_found", "Cơ sở hiện không khả dụng."));
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var name = rawName.Trim(); var phone = rawPhone.Trim();
        if (name.Length is < 2 or > 200) return (property.Id, timeZone, today, name, phone, new("validation", "Vui lòng nhập tên khách hàng hợp lệ."));
        if (CustomerService.NormalizePhone(phone).Length < 8) return (property.Id, timeZone, today, name, phone, new("validation", "Vui lòng nhập số điện thoại hợp lệ."));
        return (property.Id, timeZone, today, name, phone, null);
    }

    private static PublicBookingError? ValidateDateWindow(DateOnly date, DateOnly today) => date < today || date > today.AddYears(1) ? new("validation", "Chỉ có thể gửi yêu cầu cho ngày hôm nay đến 12 tháng tới.") : null;
    private static PublicBookingError? ValidateStayDates(DateOnly arrival, DateOnly departure, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        if (arrival < today || arrival > today.AddYears(1)) return new("validation", "Ngày nhận phòng phải từ hôm nay đến 12 tháng tới.");
        var nights = departure.DayNumber - arrival.DayNumber;
        if (nights is < 1 or > 30) return new("validation", "Thời gian lưu trú phải từ 1 đến 30 đêm.");
        return null;
    }

    private async Task<HashSet<(Guid RoomId, Guid RateId)>> GetUnavailableRateKeysAsync(Guid propertyId, string timeZoneId, DateOnly date, CancellationToken ct)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); var windowStartLocal = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified); var windowEndLocal = windowStartLocal.AddDays(2);
        var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, timeZone); var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, timeZone);
        var locked = await db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && LockingStatuses.Contains(x.Status) && x.CheckInUtc < windowEndUtc && windowStartUtc < x.CheckOutUtc).Select(x => new { x.RoomId, x.CheckInUtc, x.CheckOutUtc }).ToListAsync(ct);
        var rates = await db.RoomRates.AsNoTracking().Where(x => x.Room.PropertyId == propertyId && x.Room.IsActive && x.Room.IsPublished && x.IsActive && x.Type != RoomRateType.Nightly).Select(x => new { x.Id, x.RoomId, x.StartTime, x.EndTime, x.Type }).ToListAsync(ct);
        HashSet<(Guid RoomId, Guid RateId)> unavailable = [];
        foreach (var rate in rates) { var range = ToUtcTimeSlotRange(date, rate.StartTime, rate.EndTime, rate.Type == RoomRateType.Overnight, timeZone); if (locked.Any(x => x.RoomId == rate.RoomId && x.CheckInUtc < range.CheckOutUtc && range.CheckInUtc < x.CheckOutUtc)) unavailable.Add((rate.RoomId, rate.Id)); }
        return unavailable;
    }

    private static (DateTime CheckInUtc, DateTime CheckOutUtc) ToUtcTimeSlotRange(DateOnly date, TimeOnly start, TimeOnly end, bool overnight, TimeZoneInfo tz)
    {
        var inLocal = DateTime.SpecifyKind(date.ToDateTime(start), DateTimeKind.Unspecified); var outDate = overnight || end <= start ? date.AddDays(1) : date; var outLocal = DateTime.SpecifyKind(outDate.ToDateTime(end), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(inLocal, tz), TimeZoneInfo.ConvertTimeToUtc(outLocal, tz));
    }
    private static (DateTime CheckInUtc, DateTime CheckOutUtc) ToUtcStayRange(DateOnly arrival, DateOnly departure, TimeOnly checkIn, TimeOnly checkOut, TimeZoneInfo tz)
    {
        var inLocal = DateTime.SpecifyKind(arrival.ToDateTime(checkIn), DateTimeKind.Unspecified); var outLocal = DateTime.SpecifyKind(departure.ToDateTime(checkOut), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(inLocal, tz), TimeZoneInfo.ConvertTimeToUtc(outLocal, tz));
    }
    private static bool HasBathtub(string code) => code is "COCO-01" or "MOON-04" or "AMBER-05" or "ROMAN-06";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
