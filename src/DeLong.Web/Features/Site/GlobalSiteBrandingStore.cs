using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SaveGlobalSiteBrandingRequest
{
    public string? SiteName { get; init; }
    public string? Tagline { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string? OgImageUrl { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
}

public sealed record GlobalSiteBrandingDto(
    string SiteName,
    string Tagline,
    string LogoUrl,
    string FaviconUrl,
    string OgImageUrl,
    string MetaTitle,
    string MetaDescription,
    string OverrideSiteName,
    string OverrideTagline,
    string OverrideLogoUrl,
    string OverrideFaviconUrl,
    string OverrideOgImageUrl,
    string OverrideMetaTitle,
    string OverrideMetaDescription,
    bool HasSinglePropertyFallback,
    string FallbackPropertyName);

public static class GlobalSiteBrandingStore
{
    public const string MetadataSectionType = "__GlobalBranding";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class Payload
    {
        public string? SiteName { get; init; }
        public string? Tagline { get; init; }
        public string? LogoUrl { get; init; }
        public string? FaviconUrl { get; init; }
        public string? OgImageUrl { get; init; }
        public string? MetaTitle { get; init; }
        public string? MetaDescription { get; init; }
    }

    public static async Task<GlobalSiteBrandingDto> ResolveAsync(
        AppDbContext db,
        SiteContentService siteContentService,
        IReadOnlyList<PublicPropertyContext> activeProperties,
        CancellationToken ct = default)
    {
        var overrides = await ReadOverridesAsync(db, ct);
        SiteSettingsDto? inherited = null;
        string fallbackPropertyName = string.Empty;

        if (activeProperties.Count == 1)
        {
            var property = activeProperties[0];
            inherited = (await siteContentService.GetPublicAsync(property.SiteSlug, ct))?.Settings;
            fallbackPropertyName = inherited?.SiteName ?? property.Name;
        }

        var siteName = First(overrides.SiteName, inherited?.SiteName, "De Long Homestay");
        var taglineDefault = activeProperties.Count > 1 ? string.Empty : "Long Thành · Đồng Nai";
        var tagline = First(overrides.Tagline, inherited?.Tagline, taglineDefault);
        var logoUrl = First(overrides.LogoUrl, inherited?.LogoUrl);
        var faviconUrl = First(overrides.FaviconUrl, inherited?.FaviconUrl);
        var ogImageUrl = First(overrides.OgImageUrl, inherited?.OgImageUrl);
        var metaTitle = First(overrides.MetaTitle, inherited?.MetaTitle, siteName);
        var metaDescription = First(overrides.MetaDescription, inherited?.MetaDescription);

        return new GlobalSiteBrandingDto(
            siteName,
            tagline,
            logoUrl,
            faviconUrl,
            ogImageUrl,
            metaTitle,
            metaDescription,
            Clean(overrides.SiteName),
            Clean(overrides.Tagline),
            Clean(overrides.LogoUrl),
            Clean(overrides.FaviconUrl),
            Clean(overrides.OgImageUrl),
            Clean(overrides.MetaTitle),
            Clean(overrides.MetaDescription),
            inherited is not null,
            fallbackPropertyName);
    }

    public static async Task<(bool Success, string? Error)> SaveAsync(
        AppDbContext db,
        SaveGlobalSiteBrandingRequest request,
        CancellationToken ct = default)
    {
        if (Length(request.SiteName) > 200 || Length(request.Tagline) > 300 ||
            Length(request.MetaTitle) > 200 || Length(request.MetaDescription) > 500)
            return (false, "Một hoặc nhiều trường thương hiệu vượt quá độ dài cho phép.");

        if (Length(request.LogoUrl) > 1000 || Length(request.FaviconUrl) > 1000 || Length(request.OgImageUrl) > 1000)
            return (false, "Đường dẫn ảnh thương hiệu vượt quá độ dài cho phép.");

        var payload = new Payload
        {
            SiteName = NullIfBlank(request.SiteName),
            Tagline = NullIfBlank(request.Tagline),
            LogoUrl = NullIfBlank(request.LogoUrl),
            FaviconUrl = NullIfBlank(request.FaviconUrl),
            OgImageUrl = NullIfBlank(request.OgImageUrl),
            MetaTitle = NullIfBlank(request.MetaTitle),
            MetaDescription = NullIfBlank(request.MetaDescription)
        };

        var row = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.PropertyId == null && x.Type == MetadataSectionType, ct);
        if (row is null)
        {
            row = new HomeSection
            {
                PropertyId = null,
                Type = MetadataSectionType,
                Name = "Thương hiệu chung",
                Variant = "metadata",
                ContentJson = JsonSerializer.Serialize(payload, JsonOptions),
                SortOrder = int.MinValue,
                IsVisible = false
            };
            db.Set<HomeSection>().Add(row);
        }
        else
        {
            row.ContentJson = JsonSerializer.Serialize(payload, JsonOptions);
            row.IsVisible = false;
            row.SortOrder = int.MinValue;
        }

        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    private static async Task<Payload> ReadOverridesAsync(AppDbContext db, CancellationToken ct)
    {
        var json = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == null && x.Type == MetadataSectionType)
            .Select(x => x.ContentJson)
            .SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return new Payload();

        try
        {
            return JsonSerializer.Deserialize<Payload>(json, JsonOptions) ?? new Payload();
        }
        catch (JsonException)
        {
            return new Payload();
        }
    }

    private static string First(params string?[] values) =>
        values.Select(Clean).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int Length(string? value) => value?.Trim().Length ?? 0;
}
