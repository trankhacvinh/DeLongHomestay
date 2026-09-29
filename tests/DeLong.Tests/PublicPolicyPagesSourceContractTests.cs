using DeLong.Web.Features.Site;
using DeLong.Web.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicPolicyPagesSourceContractTests
{
    [Fact]
    public void Policy_navigation_contains_the_five_public_pages()
    {
        var expected = new[]
        {
            "chinh-sach-dat-phong",
            "chinh-sach-huy-phong",
            "chinh-sach-bao-mat-thong-tin",
            "chinh-sach-thanh-toan",
            "dieu-khoan-va-dieu-kien-giao-dich-chung"
        };

        Assert.Equal(expected, PublicPolicyLinks.All.Select(x => x.Slug));
        Assert.All(PublicPolicyLinks.All, page => Assert.False(string.IsNullOrWhiteSpace(page.Title)));
    }

    [Fact]
    public void Public_shell_exposes_policy_navigation_and_footer_links()
    {
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var runtime = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-shell-runtime.js");
        var sitemap = ReadRepositoryFile("src/DeLong.Web/Features/Site/PublicSeoEndpoints.cs");

        Assert.Contains("public-footer-policies", layout, StringComparison.Ordinal);
        Assert.Contains("PublicPolicyLinks.All", layout, StringComparison.Ordinal);
        Assert.Contains("@policies", runtime, StringComparison.Ordinal);
        Assert.Contains("customPageStore.ListAsync(null, true", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_pages_are_seeded_as_editable_custom_pages_without_overwriting_existing_slugs()
    {
        var migration = ReadRepositoryFile("src/DeLong.Web/Data/Migrations/20260929104949_SeedPublicPolicyPages.cs");
        var program = ReadRepositoryFile("src/DeLong.Web/Program.cs");

        Assert.Contains("'__CustomPage'", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", migration, StringComparison.Ordinal);
        Assert.Contains("content_json ->> 'slug'", migration, StringComparison.Ordinal);
        Assert.Contains("isPublished = true", migration, StringComparison.Ordinal);
        Assert.Contains("Section(page.Id, \"RichText\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("/Policies/Details", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_data_migration_generates_five_guarded_postgresql_inserts()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestablePolicyMigration().Apply(builder);
        var sql = builder.Operations.OfType<SqlOperation>().Select(x => x.Sql).ToList();

        Assert.Equal(5, sql.Count);
        Assert.All(sql, command =>
        {
            Assert.Contains("INSERT INTO home_section", command, StringComparison.Ordinal);
            Assert.Contains("WHERE NOT EXISTS", command, StringComparison.Ordinal);
            Assert.Contains("'__CustomPage'", command, StringComparison.Ordinal);
            Assert.Contains("::jsonb", command, StringComparison.Ordinal);
        });
    }

    private sealed class TestablePolicyMigration : SeedPublicPolicyPages
    {
        public void Apply(MigrationBuilder builder) => Up(builder);
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
