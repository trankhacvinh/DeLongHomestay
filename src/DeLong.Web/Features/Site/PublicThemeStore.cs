using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SavePublicThemeRequest
{
    public string? Mode { get; init; }
}

/// <summary>
/// Public theme mode, stored as a hidden metadata HomeSection (no schema change, like the shell/branding stores).
/// <c>standard</c>: the built-in design is authoritative; per-element sizes/widths/spacing from the visual editor,
/// header/footer designer colours/rows, section layout variants and property custom.css are ignored on public pages.
/// Content (text, images, sections, navigation) stays fully editable. <c>custom</c>: the previous behaviour.
/// </summary>
public sealed record PublicThemeDto(string Mode)
{
    public bool IsStandard => !string.Equals(Mode, PublicThemeStore.CustomMode, StringComparison.Ordinal);
}

public static class PublicThemeStore
{
    public const string MetadataSectionType = "__PublicTheme";
    public const string StandardMode = "standard";
    public const string CustomMode = "custom";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class Payload
    {
        public string? Mode { get; init; }
    }

    public static string Normalize(string? mode) =>
        string.Equals(mode?.Trim(), CustomMode, StringComparison.OrdinalIgnoreCase) ? CustomMode : StandardMode;

    public static async Task<PublicThemeDto> ReadAsync(AppDbContext db, CancellationToken ct = default)
    {
        var json = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == null && x.Type == MetadataSectionType)
            .Select(x => x.ContentJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return new PublicThemeDto(StandardMode);
        try
        {
            return new PublicThemeDto(Normalize(JsonSerializer.Deserialize<Payload>(json, JsonOptions)?.Mode));
        }
        catch (JsonException)
        {
            return new PublicThemeDto(StandardMode);
        }
    }

    public static async Task<PublicThemeDto> SaveAsync(AppDbContext db, SavePublicThemeRequest request, CancellationToken ct = default)
    {
        var payload = new Payload { Mode = Normalize(request.Mode) };
        var row = await db.Set<HomeSection>()
            .FirstOrDefaultAsync(x => x.PropertyId == null && x.Type == MetadataSectionType, ct);
        if (row is null)
        {
            row = new HomeSection
            {
                PropertyId = null,
                Type = MetadataSectionType,
                Name = "Chế độ giao diện",
                Variant = "metadata",
                SortOrder = int.MinValue,
                IsVisible = false
            };
            db.Set<HomeSection>().Add(row);
        }
        row.ContentJson = JsonSerializer.Serialize(payload, JsonOptions);
        row.IsVisible = false;
        row.SortOrder = int.MinValue;
        await db.SaveChangesAsync(ct);
        return new PublicThemeDto(payload.Mode!);
    }
}
