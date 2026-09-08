using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Pricing;

public sealed class PricingService(AppDbContext db, AuditService auditService)
{
    private const int DefaultWeekendMask = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

    public async Task<(BookingPricingResult? Result, PricingOperationError? Error)> CalculateAsync(
        Guid propertyId,
        IReadOnlyList<PricingSelectionInput> selected,
        int activeRatesPerDay,
        bool fullDayPricingEnabled,
        decimal? weekdayFullDayPrice,
        bool useWeekdayFullDayPriceOnWeekend,
        decimal? weekendFullDayPrice,
        CancellationToken cancellationToken = default)
    {
        if (selected.Count == 0) return (null, new("pricing_selection_empty", "Vui lòng chọn ít nhất một khung giờ."));
        var settings = await GetSettingsEntityAsync(propertyId, cancellationToken);
        var dates = selected.Select(x => x.ServiceDate).Distinct().ToArray();
        var from = dates.Min();
        var to = dates.Max();
        var rules = await db.SpecialPricingDays.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.IsActive && !x.IsArchived && x.StartDate <= to && from <= x.EndDate)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var policies = dates.ToDictionary(x => x, x => PricingPolicyRules.Resolve(x, settings.WeekendDayMask, rules));
        return PricingCalculator.Calculate(selected, new PricingCalculationSettings(
            activeRatesPerDay, fullDayPricingEnabled, weekdayFullDayPrice, useWeekdayFullDayPriceOnWeekend,
            weekendFullDayPrice, settings.ThreeSlotDiscountEnabled, settings.ThreeSlotCount,
            settings.ThreeSlotDiscountPercent), policies);
    }

    public async Task<IReadOnlyDictionary<DateOnly, PricingDayPolicy>> ResolvePoliciesAsync(
        Guid propertyId,
        IEnumerable<DateOnly> dates,
        CancellationToken cancellationToken = default)
    {
        var dateList = dates.Distinct().ToArray();
        if (dateList.Length == 0) return new Dictionary<DateOnly, PricingDayPolicy>();
        var settings = await GetSettingsEntityAsync(propertyId, cancellationToken);
        var from = dateList.Min();
        var to = dateList.Max();
        var rules = await db.SpecialPricingDays.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.IsActive && !x.IsArchived && x.StartDate <= to && from <= x.EndDate)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return dateList.ToDictionary(x => x, x => PricingPolicyRules.Resolve(x, settings.WeekendDayMask, rules));
    }

    public async Task<PropertyPricingSettings> GetSettingsAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        await GetSettingsEntityAsync(propertyId, cancellationToken);

    public async Task<IReadOnlyList<SpecialPricingDay>> GetSpecialDaysAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        await db.SpecialPricingDays.AsNoTracking().Where(x => x.PropertyId == propertyId && !x.IsArchived)
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<(PropertyPricingSettings? Settings, PricingOperationError? Error)> SaveSettingsAsync(
        Guid propertyId, SavePricingSettingsRequest request, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        if (request.ThreeSlotCount is < 2 or > 20 || request.ThreeSlotDiscountPercent is < 0 or > 100 || request.WeekendDayMask is < 1 or > 127)
            return (null, new("validation", "Cấu hình combo hoặc ngày cuối tuần không hợp lệ."));
        var settings = await db.PropertyPricingSettings.SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null)
        {
            settings = new PropertyPricingSettings { PropertyId = propertyId };
            db.PropertyPricingSettings.Add(settings);
        }
        var before = new { settings.ThreeSlotDiscountEnabled, settings.ThreeSlotCount, settings.ThreeSlotDiscountPercent, settings.WeekendDayMask };
        settings.ThreeSlotDiscountEnabled = request.ThreeSlotDiscountEnabled;
        settings.ThreeSlotCount = request.ThreeSlotCount;
        settings.ThreeSlotDiscountPercent = request.ThreeSlotDiscountPercent;
        settings.WeekendDayMask = request.WeekendDayMask;
        settings.UpdatedByUserId = actorUserId;
        auditService.Add(propertyId, "PropertyPricingSettings", settings.Id, "Updated", actorUserId, before, request);
        await db.SaveChangesAsync(cancellationToken);
        return (settings, null);
    }

    public async Task<PricingOperationError?> SaveRoomPricingAsync(
        Guid propertyId, SaveRoomPricingRequest request, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        if (request.Rooms.Count == 0) return new("validation", "Danh sách phòng không được để trống.");
        var roomIds = request.Rooms.Select(x => x.RoomId).Distinct().ToArray();
        if (roomIds.Length != request.Rooms.Count) return new("validation", "Danh sách phòng bị trùng.");
        var rooms = await db.Rooms.Include(x => x.Rates)
            .Where(x => x.PropertyId == propertyId && roomIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (rooms.Count != roomIds.Length) return new("room_not_found", "Một phòng không còn thuộc cơ sở này.");
        foreach (var input in request.Rooms)
        {
            if (input.FullDayPricingEnabled && input.FullDayPrice is not > 0)
                return new("validation", $"Phòng {rooms.Single(x => x.Id == input.RoomId).Name} chưa có giá cả ngày ngày thường.");
            if (input.FullDayPricingEnabled && !input.UseWeekdayFullDayPriceOnWeekend && input.WeekendFullDayPrice is not > 0)
                return new("validation", $"Phòng {rooms.Single(x => x.Id == input.RoomId).Name} chưa có giá cả ngày cuối tuần.");
            if (input.Rates.Any(x => x.WeekdayPrice < 0 || (!x.UseWeekdayPriceOnWeekend && x.WeekendPrice is not > 0)))
                return new("validation", "Giá ngày thường/cuối tuần không hợp lệ.");
            var room = rooms.Single(x => x.Id == input.RoomId);
            var rateIds = input.Rates.Select(x => x.RateId).Distinct().ToArray();
            if (rateIds.Length != input.Rates.Count || rateIds.Any(id => room.Rates.All(x => x.Id != id)))
                return new("rate_not_found", $"Một khung giá của phòng {room.Name} không còn tồn tại.");
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var input in request.Rooms)
        {
            var room = rooms.Single(x => x.Id == input.RoomId);
            var before = new { room.FullDayPricingEnabled, room.FullDayPrice, room.UseWeekdayFullDayPriceOnWeekend, room.WeekendFullDayPrice };
            room.FullDayPricingEnabled = input.FullDayPricingEnabled;
            room.FullDayPrice = input.FullDayPricingEnabled ? input.FullDayPrice : null;
            room.UseWeekdayFullDayPriceOnWeekend = input.UseWeekdayFullDayPriceOnWeekend;
            room.WeekendFullDayPrice = input.FullDayPricingEnabled && !input.UseWeekdayFullDayPriceOnWeekend ? input.WeekendFullDayPrice : null;
            foreach (var rateInput in input.Rates)
            {
                var rate = room.Rates.Single(x => x.Id == rateInput.RateId);
                rate.Price = rateInput.WeekdayPrice;
                rate.UseWeekdayPriceOnWeekend = rateInput.UseWeekdayPriceOnWeekend;
                rate.WeekendPrice = rateInput.UseWeekdayPriceOnWeekend ? null : rateInput.WeekendPrice;
            }
            auditService.Add(propertyId, "RoomPricing", room.Id, "Updated", actorUserId, before,
                new { room.FullDayPricingEnabled, room.FullDayPrice, room.UseWeekdayFullDayPriceOnWeekend, room.WeekendFullDayPrice, Rates = input.Rates });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public async Task<(SpecialPricingDay? Day, PricingOperationError? Error)> SaveSpecialDayAsync(
        Guid propertyId, Guid? id, SaveSpecialPricingDayRequest request, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        if (request.EndDate < request.StartDate || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200 || request.SurchargePercent is < 0 or > 100)
            return (null, new("validation", "Thông tin ngày đặc biệt không hợp lệ."));
        var overlaps = await db.SpecialPricingDays.AnyAsync(x => x.PropertyId == propertyId && !x.IsArchived && x.IsActive &&
            (!id.HasValue || x.Id != id.Value) && x.StartDate <= request.EndDate && request.StartDate <= x.EndDate, cancellationToken);
        if (overlaps) return (null, new("special_day_overlap", "Khoảng ngày này đang giao với một cấu hình ngày đặc biệt khác."));
        var entity = id.HasValue
            ? await db.SpecialPricingDays.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == id.Value, cancellationToken)
            : null;
        if (id.HasValue && entity is null) return (null, new("not_found", "Không tìm thấy ngày đặc biệt."));
        entity ??= new SpecialPricingDay { PropertyId = propertyId, CreatedByUserId = actorUserId };
        if (!id.HasValue) db.SpecialPricingDays.Add(entity);
        var before = id.HasValue ? Snapshot(entity) : null;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Name = request.Name.Trim();
        entity.Category = request.Category;
        entity.BasePriceProfile = request.BasePriceProfile;
        entity.SurchargePercent = request.SurchargePercent;
        entity.BookingMode = request.BookingMode;
        entity.AllowThreeSlotCombo = request.AllowThreeSlotCombo && request.BookingMode != SpecialDayBookingMode.FullDayOnly;
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedByUserId = actorUserId;
        auditService.Add(propertyId, "SpecialPricingDay", entity.Id, id.HasValue ? "Updated" : "Created", actorUserId, before, Snapshot(entity));
        await db.SaveChangesAsync(cancellationToken);
        return (entity, null);
    }

    public async Task<bool> ArchiveSpecialDayAsync(Guid propertyId, Guid id, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await db.SpecialPricingDays.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == id, cancellationToken);
        if (entity is null) return false;
        var before = Snapshot(entity);
        entity.IsArchived = true;
        entity.IsActive = false;
        entity.UpdatedByUserId = actorUserId;
        auditService.Add(propertyId, "SpecialPricingDay", entity.Id, "Archived", actorUserId, before, Snapshot(entity));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PropertyPricingSettings> GetSettingsEntityAsync(Guid propertyId, CancellationToken cancellationToken) =>
        await db.PropertyPricingSettings.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken)
        ?? new PropertyPricingSettings { PropertyId = propertyId, WeekendDayMask = DefaultWeekendMask };

    private static object Snapshot(SpecialPricingDay x) => new
    {
        x.Id, x.StartDate, x.EndDate, x.Name, x.Category, x.BasePriceProfile,
        x.SurchargePercent, x.BookingMode, x.AllowThreeSlotCombo, x.Note, x.IsActive, x.IsArchived
    };
}
