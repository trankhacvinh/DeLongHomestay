using Xunit;

namespace DeLong.Tests;

public sealed class InitialSetupSourceContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Setup_is_first_admin_only_and_serialized_in_postgres()
    {
        var service = Read("src/DeLong.Web/Features/Setup/InitialSetupService.cs");
        var page = Read("src/DeLong.Web/Pages/Setup.cshtml.cs");
        var login = Read("src/DeLong.Web/Pages/Account/Login.cshtml.cs");

        Assert.Contains("role.NormalizedName == \"ADMIN\"", service, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("pg_advisory_xact_lock", service, StringComparison.Ordinal);
        Assert.Contains("if (!await IsRequiredAsync", service, StringComparison.Ordinal);
        Assert.Contains("[AllowAnonymous]", page, StringComparison.Ordinal);
        Assert.Contains("RedirectToPage(\"/Setup\")", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Optional_seed_contains_complete_safe_DeLong_starter_configuration()
    {
        var seeder = Read("src/DeLong.Web/Data/Seed/DbSeeder.cs");

        Assert.Contains("RoomSeed(\"COCO-01\"", seeder, StringComparison.Ordinal);
        Assert.Contains("RoomSeed(\"ROMAN-06\"", seeder, StringComparison.Ordinal);
        Assert.Contains("780_000m, 910_000m", seeder, StringComparison.Ordinal);
        Assert.Contains("880_000m, 990_000m", seeder, StringComparison.Ordinal);
        Assert.Contains("new PropertyPricingSettings", seeder, StringComparison.Ordinal);
        Assert.Contains("new PropertyPay2SSettings", seeder, StringComparison.Ordinal);
        Assert.Contains("new PropertyNotificationSettings", seeder, StringComparison.Ordinal);
        Assert.Contains("new CustomerAccountSettings", seeder, StringComparison.Ordinal);
        Assert.Contains("new PropertyAiProfile", seeder, StringComparison.Ordinal);
        Assert.Contains("ProtectedApiKey = string.Empty", seeder, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = false", seeder, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey = \"", seeder, StringComparison.Ordinal);
    }

    [Fact]
    public void Setup_form_explains_seed_choice_and_requires_password_confirmation()
    {
        var page = Read("src/DeLong.Web/Pages/Setup.cshtml");
        var model = Read("src/DeLong.Web/Pages/Setup.cshtml.cs");

        Assert.Contains("Input.SeedDeLongData", page, StringComparison.Ordinal);
        Assert.Contains("không có khóa bí mật/API key", page, StringComparison.Ordinal);
        Assert.Contains("[Compare(nameof(Password)", model, StringComparison.Ordinal);
        Assert.Contains("MinimumLength = 8", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_configuration_does_not_bypass_setup_with_a_known_seeded_password()
    {
        var settings = Read("src/DeLong.Web/appsettings.json");

        Assert.Contains("\"SeedOnStartup\": false", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminPassword", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("admin@localhost.com", settings, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
