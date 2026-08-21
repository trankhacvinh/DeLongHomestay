using Xunit;

namespace DeLong.Tests;

public sealed class AdminTwoFactorLoginSourceContractTests
{
    [Fact]
    public void Password_page_does_not_render_authenticator_and_two_factor_has_its_own_step()
    {
        var loginPage = ReadRepositoryFile("src/DeLong.Web/Pages/Account/Login.cshtml");
        var loginModel = ReadRepositoryFile("src/DeLong.Web/Pages/Account/Login.cshtml.cs");
        var twoFactorPage = ReadRepositoryFile("src/DeLong.Web/Pages/Account/LoginWith2fa.cshtml");

        Assert.DoesNotContain("Input.AuthenticatorCode", loginPage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-toggle-authenticator", loginPage, StringComparison.Ordinal);
        Assert.Contains("result.RequiresTwoFactor", loginModel, StringComparison.Ordinal);
        Assert.Contains("RedirectToPage(\"/Account/LoginWith2fa\"", loginModel, StringComparison.Ordinal);
        Assert.Contains("Input.Code", twoFactorPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Emergency_bypass_is_disabled_by_default_and_restricted_to_admin_role()
    {
        var settings = ReadRepositoryFile("src/DeLong.Web/appsettings.json");
        var loginModel = ReadRepositoryFile("src/DeLong.Web/Pages/Account/Login.cshtml.cs");

        Assert.Contains("\"AdminEmergencyBypassTwoFactor\": false", settings, StringComparison.Ordinal);
        Assert.Contains("userManager.IsInRoleAsync(user, \"Admin\")", loginModel, StringComparison.Ordinal);
        Assert.Contains("userManager.CheckPasswordAsync(user, Input.Password)", loginModel, StringComparison.Ordinal);
        Assert.Contains("userManager.SetLockoutEndDateAsync(user, null)", loginModel, StringComparison.Ordinal);
        Assert.Contains("logger.LogCritical", loginModel, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate repository root from the test output directory.");

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
