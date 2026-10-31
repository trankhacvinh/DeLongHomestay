using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeLong.Tests;

public sealed class SecurityAndPolicyCacheTests
{
    [Fact]
    public async Task Cached_policy_is_reused_and_refreshed_after_save()
    {
        var root = Path.Combine(Path.GetTempPath(), "delong-policy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new StoragePaths(root, Path.Combine(root, "public"), new PathString("/uploads/rooms"), true, true, false);
            var config = new ConfigurationBuilder().Build();
            var propertyId = Guid.NewGuid();

            var first = await new BookingPolicyStore(paths, config).GetAsync(propertyId);
            var second = await new BookingPolicyStore(paths, config).GetAsync(propertyId);
            Assert.Same(first, second);
            Assert.True(first.IdentityEncryptionConfigured);

            var (saved, error) = await new BookingPolicyStore(paths, config).SaveAsync(propertyId, new UpdateBookingPolicyRequest
            {
                PublicMaxNights = 5,
                PolicyTitle = "Nội quy mới",
                PolicyText = "<p>Giữ yên tĩnh</p>"
            });
            Assert.Null(error);
            Assert.NotNull(saved);

            var afterSave = await new BookingPolicyStore(paths, config).GetAsync(propertyId);
            Assert.Equal(5, afterSave.PublicMaxNights);
            Assert.Equal("Nội quy mới", afterSave.PolicyTitle);
            Assert.Equal(saved.PolicyVersion, afterSave.PolicyVersion);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("/Admin/Calendar", true)]
    [InlineData("/admin/bookings", true)]
    [InlineData("/Account/Login", true)]
    [InlineData("/api/admin/properties/x/bookings", true)]
    [InlineData("/", false)]
    [InlineData("/booking", false)]
    [InlineData("/api/public/booking-requests", false)]
    [InlineData("/Administrator", false)]
    public void Staff_pages_get_the_stricter_content_security_policy(string path, bool staff)
    {
        var policy = SecurityHeaders.ContentSecurityPolicyFor(new PathString(path));

        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'self'", policy, StringComparison.Ordinal);
        Assert.Equal(staff, policy.Contains("connect-src 'self'", StringComparison.Ordinal));
        Assert.Equal(staff, policy.Contains("form-action 'self'", StringComparison.Ordinal));
    }
}
