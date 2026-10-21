using System.Security.Claims;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Common.Security;

public sealed record CurrentPropertyDto(Guid Id, string Code, string Name, string TimeZoneId);
public sealed record ReportingPropertyScope(CurrentPropertyDto WorkingProperty, CurrentPropertyDto SelectedProperty,
    IReadOnlyList<CurrentPropertyDto> Properties, bool IsAll);

public sealed class CurrentPropertyService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public const string WorkingPropertyCookieName = "delong.working-property";

    public async Task<ReportingPropertyScope?> ResolveReportingScopeAsync(ClaimsPrincipal user, Guid? requestedPropertyId,
        string? scope, CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out _)) return null;
        if (!user.IsInRole("Admin"))
        {
            var working = await ResolveAsync(user, requestedPropertyId, cancellationToken);
            if (working is null) return null;
            if (!string.IsNullOrWhiteSpace(scope) && (!Guid.TryParse(scope, out var scopedId) || scopedId != working.Id)) return null;
            return new(working, working, [working], false);
        }

        // Read-only reporting scope is independent of permission to mutate a working property.
        var properties = await db.Properties.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new CurrentPropertyDto(x.Id, x.Code, x.Name, x.TimeZoneId)).ToListAsync(cancellationToken);
        var workingProperty = requestedPropertyId.HasValue ? properties.SingleOrDefault(x => x.Id == requestedPropertyId) : properties.FirstOrDefault();
        if (workingProperty is null) return null;
        if (string.IsNullOrWhiteSpace(scope) || string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
            return new(workingProperty, workingProperty, properties, true);
        if (!Guid.TryParse(scope, out var selectedId)) return null;
        var selected = properties.SingleOrDefault(x => x.Id == selectedId);
        return selected is null ? null : new(workingProperty, selected, properties, false);
    }

    public async Task<CurrentPropertyDto?> ResolveAsync(
        ClaimsPrincipal user,
        Guid? requestedPropertyId = null,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return null;

        var query = db.UserPropertyAccesses
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Property.IsActive);

        if (requestedPropertyId.HasValue)
        {
            var requested = await ToDtoQuery(query.Where(x => x.PropertyId == requestedPropertyId.Value))
                .SingleOrDefaultAsync(cancellationToken);
            if (requested is not null) Remember(requested.Id);
            return requested;
        }

        var http = httpContextAccessor.HttpContext;
        if (http is not null &&
            http.Request.Cookies.TryGetValue(WorkingPropertyCookieName, out var rememberedValue) &&
            Guid.TryParse(rememberedValue, out var rememberedId))
        {
            var remembered = await ToDtoQuery(query.Where(x => x.PropertyId == rememberedId))
                .SingleOrDefaultAsync(cancellationToken);
            if (remembered is not null) return remembered;

            ForgetRemembered();
        }

        // A single accessible property is unambiguous and can be selected automatically.
        // With two or more properties we intentionally return null so the UI can ask the
        // user to choose a working property instead of silently falling back to the first.
        var candidates = await ToDtoQuery(query
                .OrderBy(x => x.Property.Name)
                .ThenBy(x => x.Property.Code))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (candidates.Count != 1) return null;

        Remember(candidates[0].Id);
        return candidates[0];
    }

    public async Task<IReadOnlyList<CurrentPropertyDto>> GetAccessibleAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return [];

        var query = db.UserPropertyAccesses
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Property.IsActive)
            .OrderBy(x => x.Property.Name)
            .ThenBy(x => x.Property.Code);

        return await ToDtoQuery(query).ToListAsync(cancellationToken);
    }

    private static IQueryable<CurrentPropertyDto> ToDtoQuery(IQueryable<UserPropertyAccess> query) =>
        query.Select(x => new CurrentPropertyDto(
            x.PropertyId,
            x.Property.Code,
            x.Property.Name,
            x.Property.TimeZoneId));

    private void Remember(Guid propertyId)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null || http.Response.HasStarted) return;

        http.Response.Cookies.Append(
            WorkingPropertyCookieName,
            propertyId.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
                Path = "/Admin",
                MaxAge = TimeSpan.FromDays(180)
            });
    }

    private void ForgetRemembered()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null || http.Response.HasStarted) return;
        http.Response.Cookies.Delete(WorkingPropertyCookieName, new CookieOptions { Path = "/Admin" });
    }
}
