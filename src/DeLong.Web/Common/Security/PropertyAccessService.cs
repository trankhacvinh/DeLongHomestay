using System.Security.Claims;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Common.Security;

public sealed class PropertyAccessService(AppDbContext db)
{
    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return false;

        return await db.UserPropertyAccesses
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.PropertyId == propertyId,
                cancellationToken);
    }
}
