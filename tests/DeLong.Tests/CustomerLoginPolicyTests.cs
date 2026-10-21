using DeLong.Web.Features.CustomerAccounts;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Xunit;

namespace DeLong.Tests;

public sealed class CustomerLoginPolicyTests
{
    [Fact]
    public void Customer_options_increase_retry_budget_without_changing_admin_or_other_identity_settings()
    {
        var shared = new IdentityOptions();
        shared.Lockout.MaxFailedAccessAttempts = 5;
        shared.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        shared.Password.RequiredLength = 12;
        var customer = CustomerLoginPolicy.CreateOptions(shared);

        Assert.Equal(15, customer.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(1), customer.Lockout.DefaultLockoutTimeSpan);
        Assert.Equal(5, shared.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), shared.Lockout.DefaultLockoutTimeSpan);
        Assert.NotSame(shared.Lockout, customer.Lockout);
        Assert.Same(shared.Password, customer.Password);
        Assert.Same(shared.User, customer.User);
        Assert.Same(shared.SignIn, customer.SignIn);
        Assert.Same(shared.Tokens, customer.Tokens);
        Assert.Same(shared.Stores, customer.Stores);
        Assert.Same(shared.ClaimsIdentity, customer.ClaimsIdentity);
    }

    [Fact]
    public void Repeated_profile_checks_do_not_consume_login_attempt_budget()
    {
        using var status = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.AccountStatusPermitLimit).GetPartition);
        using var login = PartitionedRateLimiter.Create<HttpContext, string>(
            new PublicBookingRateLimitPolicy(PublicBookingRateLimitPolicy.AccountLoginPermitLimit).GetPartition);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        for (var i = 0; i < PublicBookingRateLimitPolicy.AccountStatusPermitLimit; i++)
        {
            using var lease = status.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }
        using var rejected = status.AttemptAcquire(context);
        using var loginAllowed = login.AttemptAcquire(context);
        Assert.False(rejected.IsAcquired);
        Assert.True(loginAllowed.IsAcquired);
    }
}
