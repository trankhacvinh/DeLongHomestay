using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Domain.Entities;

[Index(nameof(SiteSlug), IsUnique = true)]
public sealed class Property : EntityBase
{
    public int PublicMinimumRemainingMinutes { get; set; } = 120;
    public bool PublicCalendarMobileAllRooms { get; set; } = true;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SiteSlug { get; set; }

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public int HousekeepingBeforeCheckInMinutes { get; set; }
    public int HousekeepingAfterCheckOutMinutes { get; set; }
    public bool HousekeepingStaggerEnabled { get; set; }
    public int HousekeepingCleaningMinutes { get; set; } = 30;
    public int HousekeepingWorkerCount { get; set; } = 1;
    public int HousekeepingMaxAdvanceMinutes { get; set; } = 30;
    public TimeOnly? HousekeepingWorkStart { get; set; }
    public TimeOnly? HousekeepingWorkEnd { get; set; }
    public bool AutomaticCheckInEnabled { get; set; } = true;
    public bool AutomaticCheckOutEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
