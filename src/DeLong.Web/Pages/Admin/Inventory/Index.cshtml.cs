using System.Globalization;
using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Admin.Inventory;

public sealed class IndexModel(InventoryService inventory, CurrentPropertyService properties, AppDbContext db) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";
    public async Task<IActionResult> OnGetAsync(string? month, CancellationToken cancellationToken)
    {
        month ??= TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var (snapshot, error) = await inventory.GetAsync(User, month, cancellationToken);
        if (error is not null) return StatusCode(error.Status, error.Message);
        var branches = User.IsInRole("Admin")
            ? await db.Properties.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new CurrentPropertyDto(x.Id, x.Code, x.Name, x.TimeZoneId)).ToListAsync(cancellationToken)
            : (await properties.GetAccessibleAsync(User, cancellationToken)).ToList();
        PageDataJson = JsonSerializer.Serialize(new { month, snapshot, properties = branches,
            canInitialize = User.IsInRole("Admin"), canManage = User.IsInRole("Admin") || User.IsInRole("Manager") }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Page();
    }
}
