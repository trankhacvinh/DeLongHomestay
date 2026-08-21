using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Housekeeping;

public sealed record ChangeHousekeepingStatusRequest(HousekeepingStatus Status);

public sealed record UpdateHousekeepingSettingsRequest(
    int BeforeCheckInMinutes,
    int AfterCheckOutMinutes);

public sealed record HousekeepingSettingsDto(
    int BeforeCheckInMinutes,
    int AfterCheckOutMinutes);

public sealed record HousekeepingRoomDto(
    Guid RoomId,
    string RoomCode,
    string RoomName,
    HousekeepingStatus Status,
    DateTime? UpdatedAtUtc,
    Guid? UpdatedByUserId);

public sealed record HousekeepingScheduleDto(
    DateOnly From,
    int Days,
    string TimeZoneId,
    HousekeepingSettingsDto Settings,
    IReadOnlyList<HousekeepingScheduleDayDto> Calendar);

public sealed record HousekeepingScheduleDayDto(
    DateOnly Date,
    IReadOnlyList<HousekeepingScheduleTaskDto> Tasks);

public sealed record HousekeepingScheduleTaskDto(
    Guid BookingId,
    string BookingCode,
    Guid RoomId,
    string RoomCode,
    string RoomName,
    DateTime AtUtc,
    string Kind,
    string Action,
    string Text);

public sealed record RoomConditionTagDto(Guid Id, string Name, string Category);

public sealed record RoomConditionReportImageDto(
    Guid Id,
    string LargeUrl,
    string ThumbnailUrl,
    int Width,
    int Height);

public sealed record RoomConditionReportDto(
    Guid Id,
    Guid RoomId,
    string RoomCode,
    string RoomName,
    RoomInspectionType InspectionType,
    RoomConditionSeverity Severity,
    RoomConditionReportStatus Status,
    string Content,
    IReadOnlyList<string> Tags,
    string ReportedBy,
    DateTime CreatedAtUtc,
    IReadOnlyList<RoomConditionReportImageDto> Images);

public sealed record CreateRoomConditionTagRequest(string Name, string Category);

public sealed record ChangeRoomConditionReportStatusRequest(RoomConditionReportStatus Status);
