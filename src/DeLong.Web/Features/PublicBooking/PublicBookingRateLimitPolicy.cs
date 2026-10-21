using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingRateLimitPolicy(int permitLimit) : IRateLimiterPolicy<string>
{
    public const int BookingPermitLimit = 120;
    public const int IdentityPermitLimit = 240;
    public const int CompletionPermitLimit = 120;
    public const int LookupPermitLimit = 120;
    public const int AccountStatusPermitLimit = 300;
    public const int AccountLoginPermitLimit = 60;
    public const int VoucherPermitLimit = 120;

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
