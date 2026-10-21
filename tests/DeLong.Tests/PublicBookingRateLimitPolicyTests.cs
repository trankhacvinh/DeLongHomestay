using System.Net;
using System.Threading.RateLimiting;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicBookingRateLimitPolicyTests
{
    [Theory]
    [InlineData(PublicBookingRateLimitPolicy.BookingPermitLimit)]
    [InlineData(PublicBookingRateLimitPolicy.IdentityPermitLimit)]
    [InlineData(PublicBookingRateLimitPolicy.AccountStatusPermitLimit)]
    [InlineData(PublicBookingRateLimitPolicy.AccountLoginPermitLimit)]
    public void Each_ip_has_its_own_budget_and_excess_requests_get_retry_metadata(int permits)
    {
        var policy = new PublicBookingRateLimitPolicy(permits);
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(policy.GetPartition);
        var first = Context("192.0.2.1");
        for (var i = 0; i < permits; i++)
        {
            using var lease = limiter.AttemptAcquire(first);
            Assert.True(lease.IsAcquired);
        }
        using var rejected = limiter.AttemptAcquire(first);
        Assert.False(rejected.IsAcquired);
        Assert.True(rejected.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter.TotalMinutes, 0.01, 10);
        using var otherIp = limiter.AttemptAcquire(Context("192.0.2.2"));
        Assert.True(otherIp.IsAcquired);
    }

    [Fact]
    public void Full_booking_budget_can_upload_two_images_each_and_complete_independently()
    {
        using var create = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.BookingPermitLimit).GetPartition);
        using var identity = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.IdentityPermitLimit).GetPartition);
        using var complete = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.CompletionPermitLimit).GetPartition);
        var context = Context("192.0.2.1");
        for (var i = 0; i < PublicBookingRateLimitPolicy.BookingPermitLimit; i++)
        {
            using var booking = create.AttemptAcquire(context);
            using var front = identity.AttemptAcquire(context);
            using var back = identity.AttemptAcquire(context);
            using var completion = complete.AttemptAcquire(context);
            Assert.True(booking.IsAcquired && front.IsAcquired && back.IsAcquired && completion.IsAcquired);
        }
        using var excessCreate = create.AttemptAcquire(context);
        using var excessIdentity = identity.AttemptAcquire(context);
        using var excessComplete = complete.AttemptAcquire(context);
        Assert.False(excessCreate.IsAcquired);
        Assert.False(excessIdentity.IsAcquired);
        Assert.False(excessComplete.IsAcquired);
    }

    [Fact]
    public void Exhausting_upload_budget_does_not_block_creation_or_completion()
    {
        using var identity = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.IdentityPermitLimit).GetPartition);
        using var create = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.BookingPermitLimit).GetPartition);
        using var complete = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.CompletionPermitLimit).GetPartition);
        var context = Context("192.0.2.1");
        for (var i = 0; i < PublicBookingRateLimitPolicy.IdentityPermitLimit; i++)
        {
            using var upload = identity.AttemptAcquire(context);
            Assert.True(upload.IsAcquired);
        }
        using var rejected = identity.AttemptAcquire(context);
        using var booking = create.AttemptAcquire(context);
        using var completion = complete.AttemptAcquire(context);
        Assert.False(rejected.IsAcquired);
        Assert.True(booking.IsAcquired);
        Assert.True(completion.IsAcquired);
    }

    private static DefaultHttpContext Context(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
