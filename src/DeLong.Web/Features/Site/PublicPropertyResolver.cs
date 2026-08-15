using System.Text.RegularExpressions;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed record PublicPropertyContext(
    Guid Id,
    string Code,
    string Name,
    string TimeZoneId,
    string SiteSlug);

public sealed class PublicPropertyResolver(AppDbContext db)
{
    public const string LegacyPropertyCode = "DELONG";
    private static readonly Regex InvalidSlugCharacters = new("[^a-z0-9]+", RegexOptions.Compiled);

    public async Task<PublicPropertyContext?> ResolveAsync(
        string? siteSlug,
        CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name, x.TimeZoneId, x.SiteSlug })
            .ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            var legacy = properties.SingleOrDefault(x => x.Code == LegacyPropertyCode);
            return legacy is null
                ? null
                : ToContext(legacy.Id, legacy.Code, legacy.Name, legacy.TimeZoneId, legacy.SiteSlug);
        }

        var normalized = NormalizeSiteSlug(siteSlug);
        var matches = properties
            .Where(x => string.Equals(EffectiveSiteSlug(x.SiteSlug, x.Code), normalized, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count == 1
            ? ToContext(matches[0].Id, matches[0].Code, matches[0].Name, matches[0].TimeZoneId, matches[0].SiteSlug)
            : null;
    }

    public async Task<PublicPropertyContext?> ResolveByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var property = await db.Properties.AsNoTracking()
            .Where(x => x.Id == propertyId && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name, x.TimeZoneId, x.SiteSlug })
            .SingleOrDefaultAsync(cancellationToken);

        return property is null
            ? null
            : ToContext(property.Id, property.Code, property.Name, property.TimeZoneId, property.SiteSlug);
    }

    public static string ToSiteSlug(string code)
    {
        var normalized = NormalizeSiteSlug(code);
        return string.Equals(normalized, "delong", StringComparison.OrdinalIgnoreCase)
            ? "de-long"
            : normalized;
    }

    public static string EffectiveSiteSlug(string? storedSiteSlug, string code) =>
        string.IsNullOrWhiteSpace(storedSiteSlug)
            ? ToSiteSlug(code)
            : NormalizeSiteSlug(storedSiteSlug);

    public static string NormalizeSiteSlug(string value)
    {
        var normalized = InvalidSlugCharacters
            .Replace((value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-'), "-")
            .Trim('-');
        return normalized;
    }

    public static string ScopePrefix(string? siteSlug) =>
        string.IsNullOrWhiteSpace(siteSlug)
            ? string.Empty
            : $"/h/{Uri.EscapeDataString(NormalizeSiteSlug(siteSlug))}";

    private static PublicPropertyContext ToContext(
        Guid id,
        string code,
        string name,
        string timeZoneId,
        string? storedSiteSlug) =>
        new(id, code, name, timeZoneId, EffectiveSiteSlug(storedSiteSlug, code));
}
