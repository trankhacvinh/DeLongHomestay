using System.Security.Claims;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Common.Security;

public sealed record CurrentPropertyDto(Guid Id, string Code, string Name, string TimeZoneId);

public sealed class CurrentPropertyService(AppDbContext db)
{
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
            query = query.Where(x => x.PropertyId == requestedPropertyId.Value);
        }

        return await query
            .OrderBy(x => x.Property.Name)
            .Select(x => new CurrentPropertyDto(
                x.PropertyId,
                x.Property.Code,
                x.Property.Name,
                x.Property.TimeZoneId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CurrentPropertyDto>> GetAccessibleAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return [];

        return await db.UserPropertyAccesses
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Property.IsActive)
            .OrderBy(x => x.Property.Name)
            .Select(x => new CurrentPropertyDto(
                x.PropertyId,
                x.Property.Code,
                x.Property.Name,
                x.Property.TimeZoneId))
            .ToListAsync(cancellationToken);
    }
}
