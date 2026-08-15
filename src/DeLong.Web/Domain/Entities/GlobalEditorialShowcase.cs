namespace DeLong.Web.Domain.Entities;

public sealed class GlobalEditorialShowcase : EntityBase
{
    public bool GalleryEnabled { get; set; } = true;
    public string GalleryMode { get; set; } = "all";
    public string GalleryPropertyIdsJson { get; set; } = "[]";
    public string GalleryItemIdsJson { get; set; } = "[]";
    public int GalleryLimit { get; set; } = 8;
    public string GalleryTitle { get; set; } = "Một vài khoảnh khắc tại De Long";

    public bool BlogEnabled { get; set; } = true;
    public string BlogMode { get; set; } = "all";
    public string BlogPropertyIdsJson { get; set; } = "[]";
    public string BlogPostIdsJson { get; set; } = "[]";
    public int BlogLimit { get; set; } = 3;
    public string BlogTitle { get; set; } = "Gợi ý cho chuyến nghỉ của bạn";
}
