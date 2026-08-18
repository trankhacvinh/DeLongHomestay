using System.Text.Json;
using System.Text.RegularExpressions;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public sealed class SavePublicHeaderDesignRequest
{
    public bool Sticky { get; init; } = true;
    public string? Background { get; init; }
    public string? TextColor { get; init; }
    public string? BorderColor { get; init; }
    public bool Shadow { get; init; }
    public bool Blur { get; init; } = true;
    public string? StickyBackground { get; init; }
    public string? StickyTextColor { get; init; }
    public string? StickyBorderColor { get; init; }
    public bool StickyShadow { get; init; }
    public bool StickyBlur { get; init; } = true;
}

public sealed class SaveFooterLinkRequest
{
    public string? Label { get; init; }
    public string? Url { get; init; }
    public bool OpenInNewTab { get; init; }
}

public sealed class SaveFooterElementRequest
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public string? Text { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public string? AltText { get; init; }
    public string? Align { get; init; }
    public string? Variant { get; init; }
    public IReadOnlyList<SaveFooterLinkRequest>? Links { get; init; }
}

public sealed class SaveFooterColumnRequest
{
    public string? Id { get; init; }
    public int Span { get; init; } = 12;
    public IReadOnlyList<SaveFooterElementRequest>? Elements { get; init; }
}

public sealed class SaveFooterRowRequest
{
    public string? Id { get; init; }
    public string? Background { get; init; }
    public string? TextColor { get; init; }
    public string? Padding { get; init; }
    public string? Gap { get; init; }
    public IReadOnlyList<SaveFooterColumnRequest>? Columns { get; init; }
}

public sealed class SavePublicShellDesignerRequest
{
    public SavePublicHeaderDesignRequest? Header { get; init; }
    public bool FooterBuilderEnabled { get; init; }
    public IReadOnlyList<SaveFooterRowRequest>? FooterRows { get; init; }
}

public sealed record PublicHeaderDesignDto(
    bool Sticky,
    string Background,
    string TextColor,
    string BorderColor,
    bool Shadow,
    bool Blur,
    string StickyBackground,
    string StickyTextColor,
    string StickyBorderColor,
    bool StickyShadow,
    bool StickyBlur);

public sealed record FooterLinkDto(string Label, string Url, bool OpenInNewTab);

public sealed record FooterElementDto(
    string Id,
    string Type,
    string Text,
    string Url,
    string ImageUrl,
    string AltText,
    string Align,
    string Variant,
    IReadOnlyList<FooterLinkDto> Links);

public sealed record FooterColumnDto(string Id, int Span, IReadOnlyList<FooterElementDto> Elements);

public sealed record FooterRowDto(
    string Id,
    string Background,
    string TextColor,
    string Padding,
    string Gap,
    IReadOnlyList<FooterColumnDto> Columns);

public sealed record PublicShellDesignerDto(
    PublicHeaderDesignDto Header,
    bool FooterBuilderEnabled,
    IReadOnlyList<FooterRowDto> FooterRows);

public static class PublicShellDesignerStore
{
    public const string MetadataSectionType = "__PublicShellDesigner";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex IdRegex = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);
    private static readonly Regex ColorRegex = new("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);
    private static readonly HashSet<string> ElementTypes = new(
        ["brand", "heading", "text", "image", "button", "links", "menu", "branches", "contact", "divider", "spacer"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Alignments = new(["left", "center", "right"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Paddings = new(["sm", "md", "lg", "xl"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Gaps = new(["sm", "md", "lg"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Variants = new(["default", "sm", "md", "lg", "h2", "h3", "h4", "pill", "outline"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> UrlTokens = new(["@home", "@rooms", "@branches", "@booking", "@lookup"], StringComparer.OrdinalIgnoreCase);

    private sealed class Payload
    {
        public int Version { get; init; } = 1;
        public SavePublicHeaderDesignRequest? Header { get; init; }
        public bool FooterBuilderEnabled { get; init; }
        public IReadOnlyList<SaveFooterRowRequest>? FooterRows { get; init; }
    }

    public static async Task<PublicShellDesignerDto> ReadAsync(AppDbContext db, Guid? propertyId, CancellationToken ct = default)
    {
        var json = await db.Set<HomeSection>().AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Type == MetadataSectionType)
            .Select(x => x.ContentJson)
            .SingleOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json)) return Defaults();
        try
        {
            return Normalize(JsonSerializer.Deserialize<Payload>(json, JsonOptions));
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    public static async Task<(PublicShellDesignerDto? Settings, string? Error)> SaveAsync(
        AppDbContext db,
        Guid? propertyId,
        SavePublicShellDesignerRequest request,
        CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error is not null) return (null, error);

        var payload = new Payload
        {
            Version = 1,
            Header = NormalizeHeaderRequest(request.Header),
            FooterBuilderEnabled = request.FooterBuilderEnabled,
            FooterRows = NormalizeRowsRequest(request.FooterRows)
        };

        var row = await db.Set<HomeSection>()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Type == MetadataSectionType, ct);
        if (row is null)
        {
            row = new HomeSection
            {
                PropertyId = propertyId,
                Type = MetadataSectionType,
                Name = "Thiết kế Header & Footer",
                Variant = "metadata",
                ContentJson = JsonSerializer.Serialize(payload, JsonOptions),
                SortOrder = int.MinValue + 12,
                IsVisible = false
            };
            db.Set<HomeSection>().Add(row);
        }
        else
        {
            row.ContentJson = JsonSerializer.Serialize(payload, JsonOptions);
            row.SortOrder = int.MinValue + 12;
            row.IsVisible = false;
        }

        await db.SaveChangesAsync(ct);
        return (Normalize(payload), null);
    }

    private static PublicShellDesignerDto Normalize(Payload? payload)
    {
        payload ??= new Payload();
        var header = NormalizeHeader(payload.Header);
        var rows = NormalizeRows(payload.FooterRows);
        return new PublicShellDesignerDto(header, payload.FooterBuilderEnabled, rows);
    }

    private static PublicShellDesignerDto Defaults() => Normalize(null);

    private static PublicHeaderDesignDto NormalizeHeader(SavePublicHeaderDesignRequest? value)
    {
        value ??= new SavePublicHeaderDesignRequest();
        return new PublicHeaderDesignDto(
            value.Sticky,
            ColorOr(value.Background, "#fbfaf6"),
            ColorOr(value.TextColor, "#172422"),
            ColorOr(value.BorderColor, "#d5e0dc"),
            value.Shadow,
            value.Blur,
            ColorOr(value.StickyBackground, "#fbfaf6"),
            ColorOr(value.StickyTextColor, "#172422"),
            ColorOr(value.StickyBorderColor, "#d5e0dc"),
            value.StickyShadow,
            value.StickyBlur);
    }

    private static SavePublicHeaderDesignRequest NormalizeHeaderRequest(SavePublicHeaderDesignRequest? value)
    {
        var header = NormalizeHeader(value);
        return new SavePublicHeaderDesignRequest
        {
            Sticky = header.Sticky,
            Background = header.Background,
            TextColor = header.TextColor,
            BorderColor = header.BorderColor,
            Shadow = header.Shadow,
            Blur = header.Blur,
            StickyBackground = header.StickyBackground,
            StickyTextColor = header.StickyTextColor,
            StickyBorderColor = header.StickyBorderColor,
            StickyShadow = header.StickyShadow,
            StickyBlur = header.StickyBlur
        };
    }

    private static IReadOnlyList<SaveFooterRowRequest> NormalizeRowsRequest(IReadOnlyList<SaveFooterRowRequest>? rows) =>
        NormalizeRows(rows).Select(row => new SaveFooterRowRequest
        {
            Id = row.Id,
            Background = row.Background,
            TextColor = row.TextColor,
            Padding = row.Padding,
            Gap = row.Gap,
            Columns = row.Columns.Select(column => new SaveFooterColumnRequest
            {
                Id = column.Id,
                Span = column.Span,
                Elements = column.Elements.Select(element => new SaveFooterElementRequest
                {
                    Id = element.Id,
                    Type = element.Type,
                    Text = element.Text,
                    Url = element.Url,
                    ImageUrl = element.ImageUrl,
                    AltText = element.AltText,
                    Align = element.Align,
                    Variant = element.Variant,
                    Links = element.Links.Select(link => new SaveFooterLinkRequest
                    {
                        Label = link.Label,
                        Url = link.Url,
                        OpenInNewTab = link.OpenInNewTab
                    }).ToList()
                }).ToList()
            }).ToList()
        }).ToList();

    private static IReadOnlyList<FooterRowDto> NormalizeRows(IReadOnlyList<SaveFooterRowRequest>? rows)
    {
        var result = new List<FooterRowDto>();
        foreach (var row in rows ?? [])
        {
            var rowId = IdOrNew(row.Id, "row");
            var columns = new List<FooterColumnDto>();
            foreach (var column in row.Columns ?? [])
            {
                var columnId = IdOrNew(column.Id, "col");
                var elements = new List<FooterElementDto>();
                foreach (var element in column.Elements ?? [])
                {
                    var type = Clean(element.Type).ToLowerInvariant();
                    if (!ElementTypes.Contains(type)) continue;
                    var links = (element.Links ?? [])
                        .Select(link => new FooterLinkDto(
                            Clean(link.Label),
                            Clean(link.Url),
                            link.OpenInNewTab))
                        .Where(link => !string.IsNullOrWhiteSpace(link.Label) && !string.IsNullOrWhiteSpace(link.Url))
                        .ToList();
                    elements.Add(new FooterElementDto(
                        IdOrNew(element.Id, "el"),
                        type,
                        Clean(element.Text),
                        Clean(element.Url),
                        Clean(element.ImageUrl),
                        Clean(element.AltText),
                        ValueOr(element.Align, Alignments, "left"),
                        ValueOr(element.Variant, Variants, "default"),
                        links));
                }
                columns.Add(new FooterColumnDto(columnId, Math.Clamp(column.Span, 1, 12), elements));
            }
            result.Add(new FooterRowDto(
                rowId,
                ColorOr(row.Background, "transparent"),
                ColorOr(row.TextColor, "#d7e3df"),
                ValueOr(row.Padding, Paddings, "md"),
                ValueOr(row.Gap, Gaps, "md"),
                columns));
        }
        return result;
    }

    private static string? Validate(SavePublicShellDesignerRequest request)
    {
        var header = request.Header;
        if (header is not null)
        {
            foreach (var color in new[] { header.Background, header.TextColor, header.BorderColor, header.StickyBackground, header.StickyTextColor, header.StickyBorderColor })
            {
                if (!IsColor(color)) return "Màu Header phải là mã HEX hợp lệ hoặc transparent.";
            }
        }

        var rows = request.FooterRows ?? [];
        if (rows.Count > 6) return "Footer tối đa 6 hàng.";
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!ValidId(row.Id) || !ids.Add(row.Id!.Trim())) return "ID hàng Footer không hợp lệ hoặc bị trùng.";
            if (!IsColor(row.Background) || !IsColor(row.TextColor)) return "Màu Footer phải là mã HEX hợp lệ hoặc transparent.";
            if (!Paddings.Contains(Clean(row.Padding)) || !Gaps.Contains(Clean(row.Gap))) return "Khoảng cách Footer không hợp lệ.";
            if ((row.Columns?.Count ?? 0) is < 1 or > 4) return "Mỗi hàng Footer cần từ 1 đến 4 cột.";
            foreach (var column in row.Columns ?? [])
            {
                if (!ValidId(column.Id) || !ids.Add(column.Id!.Trim())) return "ID cột Footer không hợp lệ hoặc bị trùng.";
                if (column.Span is < 1 or > 12) return "Độ rộng cột Footer phải từ 1 đến 12.";
                if ((column.Elements?.Count ?? 0) > 12) return "Mỗi cột Footer tối đa 12 phần tử.";
                foreach (var element in column.Elements ?? [])
                {
                    if (!ValidId(element.Id) || !ids.Add(element.Id!.Trim())) return "ID phần tử Footer không hợp lệ hoặc bị trùng.";
                    var type = Clean(element.Type);
                    if (!ElementTypes.Contains(type)) return "Footer chứa loại phần tử không được hỗ trợ.";
                    if (!Alignments.Contains(Clean(element.Align)) || !Variants.Contains(Clean(element.Variant))) return "Kiểu hiển thị phần tử Footer không hợp lệ.";
                    if (Clean(element.Text).Length > 1200 || Clean(element.AltText).Length > 300) return "Nội dung phần tử Footer quá dài.";
                    if (!IsSafeUrl(element.Url) || !IsSafeUrl(element.ImageUrl)) return "Footer chứa đường dẫn không hợp lệ.";
                    if ((element.Links?.Count ?? 0) > 12) return "Một danh sách Footer tối đa 12 liên kết.";
                    foreach (var link in element.Links ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(link.Label) || Clean(link.Label).Length > 100 || !IsSafeUrl(link.Url))
                            return "Liên kết tùy chỉnh trong Footer không hợp lệ.";
                    }
                }
            }
        }
        return null;
    }

    private static bool IsSafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var url = value.Trim();
        if (UrlTokens.Contains(url)) return true;
        if (url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)) return true;
        if (url.StartsWith("#", StringComparison.Ordinal) || url.StartsWith("?", StringComparison.Ordinal)) return true;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.Scheme is "http" or "https" or "mailto" or "tel";
        return Uri.TryCreate(url, UriKind.Relative, out _) && !url.Contains(':');
    }

    private static bool IsColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var color = value.Trim();
        return string.Equals(color, "transparent", StringComparison.OrdinalIgnoreCase) || ColorRegex.IsMatch(color);
    }

    private static string ColorOr(string? value, string fallback) => IsColor(value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
    private static string ValueOr(string? value, HashSet<string> allowed, string fallback) => allowed.Contains(Clean(value)) ? Clean(value).ToLowerInvariant() : fallback;
    private static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) && IdRegex.IsMatch(value.Trim());
    private static string IdOrNew(string? value, string prefix) => ValidId(value) ? value!.Trim() : $"{prefix}-{Guid.NewGuid():N}";
    private static string Clean(string? value) => (value ?? string.Empty).Trim();
}
