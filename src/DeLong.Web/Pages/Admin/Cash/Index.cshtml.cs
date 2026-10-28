using System.Globalization;
using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Cash;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Admin.Cash;

public sealed class IndexModel(CashService cash, CurrentPropertyService properties, AppDbContext db) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";
    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? scope, string? month, Guid? roomId, CancellationToken cancellationToken)
    {
        var context = await properties.ResolveReportingScopeAsync(User, propertyId, scope, cancellationToken);
        if (context is null) return Forbid();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(context.WorkingProperty.TimeZoneId);
        month ??= TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var (snapshot, error) = await cash.GetAsync(context, month, roomId, cancellationToken);
        if (error is not null) return BadRequest(error);
        var branches = User.IsInRole("Admin")
            ? await db.Properties.AsNoTracking().OrderBy(x => x.Name).Select(x => new CurrentPropertyDto(x.Id, x.Code, x.Name, x.TimeZoneId)).ToListAsync(cancellationToken)
            : context.Properties.ToList();
        var ids = branches.Select(x => x.Id).ToArray();
        var rooms = await db.Rooms.AsNoTracking().Where(x => ids.Contains(x.PropertyId)).OrderBy(x => x.Name).Select(x => new { x.Id, x.PropertyId, x.Name, x.IsActive }).ToListAsync(cancellationToken);
        var editable = await properties.GetAccessibleAsync(User, cancellationToken);
        PageDataJson = JsonSerializer.Serialize(new { propertyId = context.WorkingProperty.Id, scope = context.IsAll ? "all" : context.SelectedProperty.Id.ToString(), month, roomId, snapshot,
            properties = branches, editableProperties = editable.Where(x => ids.Contains(x.Id)).ToList(), rooms, canManage = User.IsInRole("Admin") || User.IsInRole("Manager") }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
