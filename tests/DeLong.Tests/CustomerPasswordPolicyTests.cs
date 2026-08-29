using DeLong.Web.Features.CustomerAccounts;
using Xunit;

namespace DeLong.Tests;

public sealed class CustomerPasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1!")]
    [InlineData("abcdefgh")]
    [InlineData("abcdef1 ")]
    [InlineData("ABCDEFG1")]
    public void Weak_customer_passwords_are_rejected(string? password)
    {
        Assert.Equal(CustomerPasswordPolicy.RequirementMessage, CustomerPasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("Abcdef12")]
    [InlineData("abcdef1!")]
    [InlineData("ABCDEF1!")]
    [InlineData("MậtKhẩu9")]
    public void Passwords_with_eight_characters_and_three_groups_are_accepted(string password)
    {
        Assert.Null(CustomerPasswordPolicy.Validate(password));
    }

    [Fact]
    public void Customer_password_policy_is_enforced_by_registration_and_change_password()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountService.cs");
        var endpoints = ReadRepositoryFile("src/DeLong.Web/Features/CustomerAccounts/CustomerAccountEndpoints.cs");
        var account = ReadRepositoryFile("src/DeLong.Web/Pages/Customer/Account.cshtml");
        var booking = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-booking-core-v2.js");

        Assert.Contains("CustomerPasswordPolicy.Validate(request.Password)", service, StringComparison.Ordinal);
        Assert.Contains("CustomerPasswordPolicy.Validate(request.NewPassword)", endpoints, StringComparison.Ordinal);
        Assert.Contains("registerPasswordStrength", account, StringComparison.Ordinal);
        Assert.Contains("DeLongPassword.mount(panel.querySelector('[data-quick-new-password]'))", booking, StringComparison.Ordinal);
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
