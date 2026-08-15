
namespace DeLong.Web.Domain.Entities;

public sealed class HomeSection : EntityBase
{
    public Guid? PropertyId { get; set; }
    public Property? Property { get; set; }

    public string Type { get; set; } = "RichText";
    public string Name { get; set; } = string.Empty;
    public string Variant { get; set; } = "default";
    public string ContentJson { get; set; } = "{}";
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}
