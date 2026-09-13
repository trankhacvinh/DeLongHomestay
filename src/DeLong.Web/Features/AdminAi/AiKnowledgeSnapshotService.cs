using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiKnowledgeBuildResult(long Version, string ContentHash, DateTime BuiltAtUtc);

public sealed class AiKnowledgeSnapshotService(AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AiKnowledgeBuildResult> RebuildAsync(Guid propertyId, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().Where(x => x.Id == propertyId)
            .Select(x => new { x.Name, x.Code, x.TimeZoneId }).SingleAsync(ct);
        var site = await db.Set<PropertySiteSettings>().AsNoTracking().Where(x => x.PropertyId == propertyId)
            .Select(x => new { x.SiteName, x.Tagline, x.Address, x.Phone, x.Email, x.FacebookUrl, x.ZaloUrl, x.GoogleMapsUrl })
            .SingleOrDefaultAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(x => x.PropertyId == propertyId && x.IsActive && x.IsPublished)
            .OrderBy(x => x.SortOrder).Select(x => new
            {
                x.Code, x.Name, x.Slug, x.ShortDescription, x.Capacity,
                Amenities = x.Amenities.Where(a => a.Amenity.IsActive).OrderBy(a => a.Amenity.Name).Select(a => a.Amenity.Name),
                Rates = x.Rates.Where(r => r.IsActive).OrderBy(r => r.SortOrder).Select(r => new
                {
                    r.Name, r.Type, r.StartTime, r.EndTime, r.Price, r.UseWeekdayPriceOnWeekend, r.WeekendPrice
                }),
                x.FullDayPricingEnabled, x.FullDayPrice, x.UseWeekdayFullDayPriceOnWeekend, x.WeekendFullDayPrice
            }).ToListAsync(ct);
        var pricing = await db.PropertyPricingSettings.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .Select(x => new { x.ThreeSlotDiscountEnabled, x.ThreeSlotCount, x.ThreeSlotDiscountPercent, x.WeekendDayMask })
            .SingleOrDefaultAsync(ct);
        var specialDays = await db.SpecialPricingDays.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.IsActive && !x.IsArchived && x.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderBy(x => x.StartDate).Take(100)
            .Select(x => new { x.Name, x.StartDate, x.EndDate, x.Category, x.BasePriceProfile, x.SurchargePercent, x.BookingMode, x.AllowThreeSlotCombo, x.Note })
            .ToListAsync(ct);

        var content = JsonSerializer.Serialize(new { property, site, rooms, pricing, specialDays }, Json);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        var now = DateTime.UtcNow;
        var row = await db.PropertyAiKnowledgeSnapshots.SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (row is null)
        {
            row = new PropertyAiKnowledgeSnapshot { PropertyId = propertyId };
            db.PropertyAiKnowledgeSnapshots.Add(row);
        }
        row.ContentJson = content;
        row.ContentHash = hash;
        row.BuiltAtUtc = now;
        row.IsDirty = false;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return new(row.Version, row.ContentHash, row.BuiltAtUtc);
    }

    public async Task<AiKnowledgeSnapshotDto?> GetAsync(Guid propertyId, CancellationToken ct)
    {
        var row = await db.PropertyAiKnowledgeSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (row is null) return null;
        return new(row.Version, row.ContentHash, row.BuiltAtUtc, row.IsDirty,
            JsonSerializer.Deserialize<object>(row.ContentJson, Json) ?? new { });
    }
}
