using System.Text.RegularExpressions;
using DeLong.Web.Common.Caching;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace DeLong.Web.Features.Site;

public sealed record PublicPropertyContext(
    Guid Id,
    string Code,
    string Name,
    string TimeZoneId,
    string SiteSlug);

public sealed class PublicPropertyResolver(AppDbContext db, IFusionCache? fusionCache = null)
{
    private readonly IFusionCache? cache = fusionCache;
    public const string LegacyPropertyCode = "DELONG";
    private static readonly Regex InvalidSlugCharacters = new("[^a-z0-9]+", RegexOptions.Compiled);

    public async Task<IReadOnlyList<PublicPropertyContext>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache is null) return await LoadActiveAsync(cancellationToken);
        return await cache.GetOrSetAsync<IReadOnlyList<PublicPropertyContext>>(
            PublicCacheKeys.ActiveProperties,
            async (_, ct) => await LoadActiveAsync(ct),
            tags: [PublicCacheKeys.Tag],
            token: cancellationToken);
    }

    private async Task<IReadOnlyList<PublicPropertyContext>> LoadActiveAsync(CancellationToken cancellationToken)
    {
        var properties = await db.Properties.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.TimeZoneId, x.SiteSlug })
            .ToListAsync(cancellationToken);
        return properties.Select(x => ToContext(x.Id, x.Code, x.Name, x.TimeZoneId, x.SiteSlug)).ToList();
    }

    public async Task<PublicPropertyContext?> ResolveAsync(
        string? siteSlug,
        CancellationToken cancellationToken = default)
    {
        var properties = await GetActiveAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(siteSlug))
            return properties.SingleOrDefault(x => x.Code == LegacyPropertyCode);

        var normalized = NormalizeSiteSlug(siteSlug);
        var matches = properties
            .Where(x => string.Equals(x.SiteSlug, normalized, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<PublicPropertyContext?> ResolveByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var properties = await GetActiveAsync(cancellationToken);
        return properties.SingleOrDefault(x => x.Id == propertyId);
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
