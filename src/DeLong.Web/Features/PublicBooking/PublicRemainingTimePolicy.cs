using DeLong.Web.Features.Operations;

namespace DeLong.Web.Features.PublicBooking;

public static class PublicRemainingTimePolicy
{
    public static bool IsTooShort(DateTime nominalStartUtc, DateTime nominalEndUtc, AvailabilityRangeDto free, int minimumMinutes) =>
        minimumMinutes > 0 && (free.StartUtc > nominalStartUtc || free.EndUtc < nominalEndUtc) &&
        free.EndUtc - free.StartUtc <= TimeSpan.FromMinutes(minimumMinutes);
}
