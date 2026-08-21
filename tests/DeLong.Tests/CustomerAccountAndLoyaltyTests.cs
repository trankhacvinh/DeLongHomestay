using DeLong.Web.Domain.Entities;
using DeLong.Web.Pages.Customer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Xunit;

namespace DeLong.Tests;

public sealed class CustomerAccountAndLoyaltyTests
{
    [Fact]
    public void Loyalty_is_disabled_by_default_and_uses_ten_thousand_per_point()
    {
        var settings = new CustomerAccountSettings();

        Assert.False(settings.LoyaltyEnabled);
        Assert.Equal(10_000, settings.LoyaltySpendPerPoint);
    }

    [Fact]
    public void Customer_registration_and_authenticator_are_enabled_by_default()
    {
        var settings = new CustomerAccountSettings();

        Assert.True(settings.RegistrationEnabled);
        Assert.True(settings.AuthenticatorEnabled);
        Assert.Equal(1, settings.TermsVersion);
    }

    [Fact]
    public void Account_lookup_keeps_identity_phone_and_property_link_fallbacks()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountService.cs");

        Assert.Contains("x.PhoneNumber == normalizedPhone", service, StringComparison.Ordinal);
        Assert.Contains("x.PropertyId == propertyId && x.Customer.NormalizedPhone == normalizedPhone", service, StringComparison.Ordinal);
        Assert.Contains("x.Customer.NormalizedPhone == normalizedPhone && x.User.IsCustomerAccount", service, StringComparison.Ordinal);
        Assert.Contains("CustomerProfileExistsAsync", service, StringComparison.Ordinal);
        Assert.Contains("customer?.Name?.Trim()", service, StringComparison.Ordinal);
        Assert.Contains("customer?.Email?.Trim()", service, StringComparison.Ordinal);
        Assert.Contains("identityEmail = null", service, StringComparison.Ordinal);
        var endpoints = ReadRepositoryFile("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountEndpoints.cs");
        Assert.Contains("new { exists, hasAccount }", endpoints, StringComparison.Ordinal);
    }

    [Fact]
    public void Customer_identity_uses_account_storage_without_promoting_an_old_booking()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountService.cs");
        var endpoints = ReadRepositoryFile("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountEndpoints.cs");
        var bookingEndpoints = ReadRepositoryFile("src/DeLong.Web/Features/PublicBooking/PublicBookingEndpoints.cs");

        Assert.DoesNotContain("PromoteLatestIdentityDocumentsAsync", service, StringComparison.Ordinal);
        Assert.Contains("storage.ListAsync(propertyId, userId, ct)", endpoints, StringComparison.Ordinal);
        Assert.Contains("CopySavedIdentityDocumentsToBookingAsync", bookingEndpoints, StringComparison.Ordinal);
    }

    [Fact]
    public void Expired_customer_session_redirects_account_to_customer_login()
    {
        var page = CreateAccountPage("/customer/account", new ClaimsPrincipal(new ClaimsIdentity()));

        var result = Assert.IsType<LocalRedirectResult>(page.OnGet());

        Assert.Equal("/customer/login", result.Url);
    }

    [Fact]
    public void Customer_login_route_renders_for_anonymous_and_redirects_signed_in_customer()
    {
        var anonymous = CreateAccountPage("/customer/login", new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.IsType<PageResult>(anonymous.OnGet());

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Customer")
        ], "Test");
        var signedIn = CreateAccountPage("/customer/login", new ClaimsPrincipal(identity));
        var result = Assert.IsType<LocalRedirectResult>(signedIn.OnGet());
        Assert.Equal("/customer/account", result.Url);
    }

    [Fact]
    public void Api_authentication_failures_do_not_redirect_fetch_to_an_html_login_page()
    {
        var program = ReadRepositoryFile("src/DeLong.Web/Program.cs");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/customer-account.js");
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Customer/Account.cshtml");

        Assert.Contains("context.Request.Path.StartsWithSegments(\"/api\")", program, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status401Unauthorized", program, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status403Forbidden", program, StringComparison.Ordinal);
        Assert.Contains("typeof profile !== 'object'", script, StringComparison.Ordinal);
        Assert.Contains("Array.isArray(profile.bookings) ? profile.bookings : []", script, StringComparison.Ordinal);
        Assert.Contains("(profile.bookings || []).length", page, StringComparison.Ordinal);
        Assert.Contains("(profile.loyaltyHistory || []).length", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Customer_login_is_compact_and_links_to_the_admin_login()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Customer/Account.cshtml");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/customer-account.css");

        Assert.Contains("href=\"/Account/Login\"", page, StringComparison.Ordinal);
        Assert.Contains("Đăng nhập trang quản trị", page, StringComparison.Ordinal);
        Assert.Contains("'is-auth-view': !loading && !profile", page, StringComparison.Ordinal);
        Assert.Contains(".customer-account-shell.is-auth-view { grid-template-columns:", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 900px)", styles, StringComparison.Ordinal);
    }

    private static AccountModel CreateAccountPage(string path, ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.Path = path;
        return new AccountModel
        {
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new PageActionDescriptor()))
        };
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
