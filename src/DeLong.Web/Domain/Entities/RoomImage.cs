namespace DeLong.Web.Domain.Entities;

public sealed class RoomImage : EntityBase
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalStoragePath { get; set; } = string.Empty;
    public string LargePath { get; set; } = string.Empty;
    public string CardPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long OriginalBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? AltText { get; set; }
    public bool IsCover { get; set; }
    public int SortOrder { get; set; }
}
