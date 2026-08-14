namespace DeLong.Web.Domain.Entities;

public sealed class AmenityPreset : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<AmenityPresetItem> Items { get; set; } = new List<AmenityPresetItem>();
}

public sealed class AmenityPresetItem
{
    public Guid AmenityPresetId { get; set; }
    public AmenityPreset AmenityPreset { get; set; } = null!;
    public Guid AmenityId { get; set; }
    public Amenity Amenity { get; set; } = null!;
}
