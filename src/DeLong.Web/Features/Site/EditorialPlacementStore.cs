using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed record EditorialPlacementDto(string GalleryAfter, string BlogAfter);

public sealed class SaveEditorialPlacementRequest
{
    public string? GalleryAfter { get; init; }
    public string? BlogAfter { get; init; }
}

public static class EditorialPlacementStore
{
    public const string MetadataSectionType = "__EditorialPlacement";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class Payload
    {
        public string GalleryAfter { get; init; } = "end";
        public string BlogAfter { get; init; } = "end";
    }

    public static async Task<EditorialPlacementDto> GetAsync(
        AppDbContext db,
        Guid? propertyId,
        CancellationToken ct = default)
    {
        var json = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType)
            .Select(x => x.ContentJson)
            .SingleOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json)) return new("end", "end");

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions) ?? new Payload();
            return new(NormalizeForRead(payload.GalleryAfter, "gallery"), NormalizeForRead(payload.BlogAfter, "blog"));
        }
        catch (JsonException)
        {
            return new("end", "end");
        }
    }

    public static async Task<(EditorialPlacementDto? Placement, SiteContentError? Error)> SaveAsync(
        AppDbContext db,
        Guid? propertyId,
        SaveEditorialPlacementRequest request,
        CancellationToken ct = default)
    {
        var galleryAfter = NormalizeForRead(request.GalleryAfter, "gallery");
        var blogAfter = NormalizeForRead(request.BlogAfter, "blog");

        var galleryError = await ValidateAnchorAsync(db, propertyId, galleryAfter, "gallery", ct);
        if (galleryError is not null) return (null, galleryError);
        var blogError = await ValidateAnchorAsync(db, propertyId, blogAfter, "blog", ct);
        if (blogError is not null) return (null, blogError);

        if (galleryAfter == "blog" && blogAfter == "gallery")
            return (null, new SiteContentError("validation", "Vị trí Gallery và Blog tạo thành vòng lặp."));

        var row = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);

        var payload = new Payload { GalleryAfter = galleryAfter, BlogAfter = blogAfter };
        if (row is null)
        {
            row = new HomeSection
            {
                PropertyId = propertyId,
                Type = MetadataSectionType,
                Name = "Vị trí Gallery & Blog",
                Variant = "metadata",
                ContentJson = JsonSerializer.Serialize(payload, JsonOptions),
                SortOrder = int.MinValue + 1,
                IsVisible = false
            };
            db.Set<HomeSection>().Add(row);
        }
        else
        {
            row.ContentJson = JsonSerializer.Serialize(payload, JsonOptions);
            row.IsVisible = false;
            row.SortOrder = int.MinValue + 1;
        }

        await db.SaveChangesAsync(ct);
        return (new EditorialPlacementDto(galleryAfter, blogAfter), null);
    }

    private static string NormalizeForRead(string? value, string self)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "end" : value.Trim().ToLowerInvariant();
        if (normalized == self) return "end";
        if (normalized is "start" or "end" or "gallery" or "blog") return normalized;
        if (normalized.StartsWith("section:", StringComparison.Ordinal) &&
            Guid.TryParse(normalized["section:".Length..], out var id))
            return $"section:{id:D}";
        return "end";
    }

    private static async Task<SiteContentError?> ValidateAnchorAsync(
        AppDbContext db,
        Guid? propertyId,
        string anchor,
        string self,
        CancellationToken ct)
    {
        if (anchor == self)
            return new("validation", "Một khối không thể đặt sau chính nó.");
        if (anchor is "start" or "end" or "gallery" or "blog") return null;
        if (!anchor.StartsWith("section:", StringComparison.Ordinal) ||
            !Guid.TryParse(anchor["section:".Length..], out var sectionId))
            return new("validation", "Vị trí khối không hợp lệ.");

        var exists = await db.Set<HomeSection>().AsNoTracking().AnyAsync(x =>
            x.Id == sectionId &&
            x.PropertyId == propertyId &&
            x.Type != MetadataSectionType &&
            x.Type != GlobalSiteBrandingStore.MetadataSectionType,
            ct);

        return exists ? null : new("validation", "Khối neo cho vị trí Gallery/Blog không còn tồn tại.");
    }
}
