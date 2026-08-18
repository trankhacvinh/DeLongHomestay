namespace DeLong.Web.Domain.Entities;

public sealed class MediaAsset : EntityBase
{
    public Guid? PropertyId { get; set; }
    public Property? Property { get; set; }

    public string Kind { get; set; } = "section";
    public string Url { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/webp";
    public string Sha256 { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string AltText { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
