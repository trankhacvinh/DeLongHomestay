namespace DeLong.Web.Features.PublicBooking;

public static class PublicSlotSelectionRules
{
    public static PublicBookingError? ValidateConsecutive(
        IReadOnlyList<(DateOnly ServiceDate, int RateIndex)> slots,
        int ratesPerDay,
        int maximumDays)
    {
        if (slots.Count == 0 || ratesPerDay <= 0)
            return new PublicBookingError("validation", "Vui lòng chọn ít nhất một khung giờ.");

        for (var index = 1; index < slots.Count; index++)
        {
            var previous = slots[index - 1];
            var current = slots[index];
            var expectedDate = previous.RateIndex == ratesPerDay - 1
                ? previous.ServiceDate.AddDays(1)
                : previous.ServiceDate;
            var expectedRateIndex = previous.RateIndex == ratesPerDay - 1 ? 0 : previous.RateIndex + 1;
            if (current.ServiceDate != expectedDate || current.RateIndex != expectedRateIndex)
                return new PublicBookingError("slots_not_consecutive", "Các khung giờ phải liền nhau và chỉ được mở rộng về phía sau.");
        }

        return slots[^1].ServiceDate.DayNumber - slots[0].ServiceDate.DayNumber + 1 > maximumDays
            ? new PublicBookingError("slot_range_too_long", $"Mỗi lượt chỉ được chọn tối đa {maximumDays} ngày liên tiếp.")
            : null;
    }
}
