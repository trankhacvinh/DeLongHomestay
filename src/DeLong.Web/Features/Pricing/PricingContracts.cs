using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Pricing;

public sealed record PricingSelectionInput(
    DateOnly ServiceDate,
    int RateIndex,
    Guid RateId,
    string RateName,
    decimal WeekdayPrice,
    bool UseWeekdayPriceOnWeekend,
    decimal? WeekendPrice);

public sealed record PricingSelectionResult(
    DateOnly ServiceDate,
    Guid RateId,
    decimal ListPrice,
    decimal AppliedAmount,
    decimal SpecialSurchargeAmount,
    decimal ComboDiscountPercent,
    decimal ComboDiscountAmount,
    PricingDayProfile DayProfile,
    Guid? SpecialPricingDayId,
    string? SpecialDayName,
    decimal SpecialSurchargePercent,
    string PricingRule);

public sealed record BookingPricingResult(
    IReadOnlyList<PricingSelectionResult> Segments,
    decimal RoomAmount,
    decimal SpecialSurchargeAmount,
    decimal TotalBeforeVoucher);

public sealed record PricingDayPolicy(
    DateOnly Date,
    PricingDayProfile DayProfile,
    Guid? SpecialPricingDayId,
    string? SpecialDayName,
    decimal SurchargePercent,
    SpecialDayBookingMode BookingMode,
    bool AllowThreeSlotCombo);

public sealed record PricingOperationError(string Code, string Message);

public sealed record PricingCalculationSettings(
    int ActiveRatesPerDay,
    bool FullDayPricingEnabled,
    decimal? WeekdayFullDayPrice,
    bool UseWeekdayFullDayPriceOnWeekend,
    decimal? WeekendFullDayPrice,
    bool MultiSlotDiscountEnabled,
    int MultiSlotCount,
    decimal MultiSlotDiscountPercent);

public sealed record SavePricingSettingsRequest(
    bool ThreeSlotDiscountEnabled,
    int ThreeSlotCount,
    decimal ThreeSlotDiscountPercent,
    int WeekendDayMask);

public sealed record SaveRoomPricingRequest(IReadOnlyList<SaveRoomPricingRoomRequest> Rooms);
public sealed record SaveRoomPricingRoomRequest(
    Guid RoomId,
    bool FullDayPricingEnabled,
    decimal? FullDayPrice,
    bool UseWeekdayFullDayPriceOnWeekend,
    decimal? WeekendFullDayPrice,
    IReadOnlyList<SaveRoomRatePricingRequest> Rates);
public sealed record SaveRoomRatePricingRequest(
    Guid RateId,
    decimal WeekdayPrice,
    bool UseWeekdayPriceOnWeekend,
    decimal? WeekendPrice);

public sealed record SaveSpecialPricingDayRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string Name,
    SpecialDayCategory Category,
    PricingDayProfile BasePriceProfile,
    decimal SurchargePercent,
    SpecialDayBookingMode BookingMode,
    bool AllowThreeSlotCombo,
    string? Note,
    bool IsActive);
