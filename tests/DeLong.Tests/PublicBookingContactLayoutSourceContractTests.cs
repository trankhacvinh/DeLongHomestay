using Xunit;

namespace DeLong.Tests;

public sealed class PublicBookingContactLayoutSourceContractTests
{
    [Fact]
    public void Phone_precedes_customer_name_and_account_panel_spans_the_form()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Booking/Index.cshtml");
        var css = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/booking-core-v2.css");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-booking-core-v2.js");
        var phoneIndex = page.IndexOf("ref=\"customerPhone\"", StringComparison.Ordinal);
        var nameIndex = page.IndexOf("ref=\"customerName\"", StringComparison.Ordinal);

        Assert.True(phoneIndex >= 0 && nameIndex > phoneIndex);
        Assert.Contains("<span>Họ và tên *</span>", page, StringComparison.Ordinal);
        Assert.Contains(".booking-account-panel{grid-column:1/-1", css, StringComparison.Ordinal);
        Assert.Contains(".booking-account-register{display:grid", css, StringComparison.Ordinal);
        Assert.Contains("data-account-terms-open", script, StringComparison.Ordinal);
        Assert.Contains("await DeLongApi.refreshAntiforgery();", script, StringComparison.Ordinal);
        Assert.True(CountOccurrences(script, "await DeLongApi.refreshAntiforgery();") >= 4);
        Assert.Contains("function openAccountTerms()", script, StringComparison.Ordinal);
        Assert.Contains("await createPendingCustomerAccount(originalPost);", script, StringComparison.Ordinal);
        Assert.Contains("Tài khoản sẽ được tạo cùng lúc khi bạn gửi yêu cầu đặt phòng.", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-quick-register>Đăng ký & điền nhanh", script, StringComparison.Ordinal);
        Assert.DoesNotContain("delong.booking.contact.v1", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.Ordinal);
        Assert.Contains("root.querySelector('.booking-id-section')", script, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(script, "restoreCustomerAccount()"));
        Assert.Contains("ref=\"customerPhone\"", page, StringComparison.Ordinal);
        Assert.Contains("data-booking-customer-phone", page, StringComparison.Ordinal);
        Assert.Contains("data-booking-customer-name", page, StringComparison.Ordinal);
        Assert.Contains("root.querySelector('[data-booking-customer-phone]')", script, StringComparison.Ordinal);
        Assert.Contains("root.querySelector('[data-booking-customer-name]')", script, StringComparison.Ordinal);
        Assert.Contains("data-customer-authenticated", page, StringComparison.Ordinal);
        Assert.Contains("state.authenticatedPhone && normalizePhone(phone) === state.authenticatedPhone", script, StringComparison.Ordinal);
        Assert.Contains("root.dataset.customerAuthenticated === 'true'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("input[autocomplete=\"tel\"]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("input[autocomplete=\"name\"]", script, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"30\" inputmode=\"tel\" autocomplete=\"off\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("target=\"_blank\" rel=\"noopener\">${escapeHtml(state.accountSettings.termsTitle", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Customer_account_page_loads_vue_before_its_page_script()
    {
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/customer-account.js");

        Assert.Contains("string.Equals(routePage, \"/Customer/Account\"", layout, StringComparison.Ordinal);
        Assert.Contains("if (!root || !window.Vue) return;", script, StringComparison.Ordinal);
        Assert.Contains("const { createApp } = window.Vue;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Authentication_changes_refresh_the_claim_bound_antiforgery_token()
    {
        var program = ReadRepositoryFile("src/DeLong.Web/Program.cs");
        var api = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/core/api.js");
        var account = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/customer-account.js");

        Assert.Contains("/api/antiforgery/token", program, StringComparison.Ordinal);
        Assert.Contains("antiforgery.GetAndStoreTokens(httpContext)", program, StringComparison.Ordinal);
        Assert.Contains("async function refreshAntiforgery()", api, StringComparison.Ordinal);
        Assert.Contains("refreshAntiforgery", api, StringComparison.Ordinal);
        Assert.True(CountOccurrences(account, "await DeLongApi.refreshAntiforgery();") >= 4);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }

    private static int CountOccurrences(string value, string search) =>
        (value.Length - value.Replace(search, string.Empty, StringComparison.Ordinal).Length) / search.Length;
}
