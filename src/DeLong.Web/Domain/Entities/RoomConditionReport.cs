using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

public sealed class RoomConditionReport : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public Guid ReportedByUserId { get; set; }
    public RoomInspectionType InspectionType { get; set; }
    public RoomConditionSeverity Severity { get; set; }
    public RoomConditionReportStatus Status { get; set; } = RoomConditionReportStatus.New;
    public string Content { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public ICollection<RoomConditionReportImage> Images { get; set; } = [];
}

public sealed class RoomConditionReportImage : EntityBase
{
    public Guid ReportId { get; set; }
    public RoomConditionReport Report { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalStoragePath { get; set; } = string.Empty;
    public string LargePath { get; set; } = string.Empty;
    public string CardPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long OriginalBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int SortOrder { get; set; }
}

public sealed class RoomConditionTag : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
