using System.Text.Json;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SavePublicThemeRequest
{
    public string? Mode { get; init; }
    /// <summary>Optional card colour per room id (hex, e.g. "#5f8a8b"). Null keeps the stored colours.</summary>
    public Dictionary<string, string>? RoomColors { get; init; }
}

/// <summary>
/// Public theme, stored as a hidden metadata HomeSection (no schema change, like the shell/branding stores).
/// <c>standard</c>: the built-in design is authoritative; per-element sizes/widths/spacing from the visual editor,
/// header/footer designer colours/rows, section layout variants and property custom.css are ignored on public pages.
/// Content (text, images, sections, navigation) stays fully editable. <c>custom</c>: the previous behaviour.
/// <c>RoomColors</c>: colour of each room card in the standard theme; rooms without one use the default palette.
/// </summary>
public sealed record PublicThemeDto(string Mode, IReadOnlyDictionary<string, string>? RoomColors = null)
{
    public bool IsStandard => !string.Equals(Mode, PublicThemeStore.CustomMode, StringComparison.Ordinal);

    public string? RoomColor(Guid roomId) =>
        RoomColors is not null && RoomColors.TryGetValue(roomId.ToString(), out var color) ? color : null;
}

/// <summary>
/// Website-wide custom CSS/JS (Admin → Website chung). Loaded on every public page, global and property pages alike,
/// in both theme modes, after all built-in stylesheets. Empty by default, so a deployment that never fills it in
/// keeps its look. <see cref="Version"/> is a short content hash used to cache-bust the public URLs.
/// </summary>
public sealed record PublicCustomCodeDto(string Css, string Js, string Version)
{
    public static readonly PublicCustomCodeDto Empty = new(string.Empty, string.Empty, string.Empty);
    public bool HasCss => Css.Length > 0;
    public bool HasJs => Js.Length > 0;
}

public sealed class SavePublicCustomCodeRequest
{
    public string? Css { get; init; }
    public string? Js { get; init; }
}

public static class PublicThemeStore
{
    public const int MaxCustomCssLength = 100_000;
    public const int MaxCustomJsLength = 100_000;
    public const string MetadataSectionType = "__PublicTheme";
    public const string StandardMode = "standard";
    public const string CustomMode = "custom";
    private const int MaxRoomColors = 500;
    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Default card palette (warm, muted tones that sit on the beige ground); cycles by card position.</summary>
    public static readonly IReadOnlyList<string> DefaultRoomPalette = ["#5f8a8b", "#7d8f69", "#b5654f", "#6f6a8a", "#b88746", "#8c6a5a"];

    private sealed class Payload
    {
        public string? Mode { get; init; }
        public Dictionary<string, string>? RoomColors { get; init; }
        public string? CustomCss { get; init; }
        public string? CustomJs { get; init; }
    }

    public static string Normalize(string? mode) =>
        string.Equals(mode?.Trim(), CustomMode, StringComparison.OrdinalIgnoreCase) ? CustomMode : StandardMode;

    public static async Task<PublicThemeDto> ReadAsync(AppDbContext db, CancellationToken ct = default)
    {
        var payload = await ReadPayloadAsync(db, ct);
        return new PublicThemeDto(Normalize(payload.Mode), CleanColors(payload.RoomColors));
    }

    public static async Task<PublicThemeDto> SaveAsync(AppDbContext db, SavePublicThemeRequest request, CancellationToken ct = default)
    {
        var current = await ReadPayloadAsync(db, ct);
        var payload = new Payload
        {
            Mode = Normalize(request.Mode ?? current.Mode),
            RoomColors = request.RoomColors is null ? CleanColors(current.RoomColors) : CleanColors(request.RoomColors),
            CustomCss = current.CustomCss,
            CustomJs = current.CustomJs
        };
        await WriteAsync(db, payload, ct);
        return new PublicThemeDto(payload.Mode!, payload.RoomColors);
    }

    public static async Task<PublicCustomCodeDto> ReadCustomCodeAsync(AppDbContext db, CancellationToken ct = default) =>
        ToCustomCode(await ReadPayloadAsync(db, ct));

    /// <summary>Saves the website-wide CSS/JS; the theme mode and room colours stored in the same row are kept.</summary>
    public static async Task<PublicCustomCodeDto> SaveCustomCodeAsync(AppDbContext db, SavePublicCustomCodeRequest request, CancellationToken ct = default)
    {
        var current = await ReadPayloadAsync(db, ct);
        var payload = new Payload
        {
            Mode = Normalize(current.Mode),
            RoomColors = CleanColors(current.RoomColors),
            CustomCss = request.Css?.Trim() ?? string.Empty,
            CustomJs = request.Js?.Trim() ?? string.Empty
        };
        await WriteAsync(db, payload, ct);
        return ToCustomCode(payload);
    }

    private static PublicCustomCodeDto ToCustomCode(Payload payload)
    {
        var css = payload.CustomCss?.Trim() ?? string.Empty;
        var js = payload.CustomJs?.Trim() ?? string.Empty;
        if (css.Length == 0 && js.Length == 0) return PublicCustomCodeDto.Empty;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(css + "\n/*js*/\n" + js));
        return new PublicCustomCodeDto(css, js, Convert.ToHexString(hash, 0, 6).ToLowerInvariant());
    }

    private static async Task WriteAsync(AppDbContext db, Payload payload, CancellationToken ct)
    {
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
    }

    private static async Task<Payload> ReadPayloadAsync(AppDbContext db, CancellationToken ct)
    {
        var json = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == null && x.Type == MetadataSectionType)
            .Select(x => x.ContentJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return new Payload();
        try
        {
            return JsonSerializer.Deserialize<Payload>(json, JsonOptions) ?? new Payload();
        }
        catch (JsonException)
        {
            return new Payload();
        }
    }

    // Only "#rrggbb" values keyed by a room GUID are kept, so stored colours are always safe to emit in a style attribute.
    private static Dictionary<string, string> CleanColors(Dictionary<string, string>? colors)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (colors is null) return result;
        foreach (var (key, value) in colors)
        {
            if (result.Count >= MaxRoomColors) break;
            if (!Guid.TryParse(key, out var roomId)) continue;
            var color = value?.Trim() ?? string.Empty;
            if (!HexColor.IsMatch(color)) continue;
            result[roomId.ToString()] = color.ToLowerInvariant();
        }
        return result;
    }
}
