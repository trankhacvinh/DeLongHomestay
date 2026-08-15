using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Domain.Entities;

[Index(nameof(SiteSlug), IsUnique = true)]
public sealed class Property : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string SiteSlug { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool IsActive { get; set; } = true;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
