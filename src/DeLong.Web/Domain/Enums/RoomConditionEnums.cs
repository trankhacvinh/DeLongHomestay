namespace DeLong.Web.Domain.Enums;

public enum RoomInspectionType
{
    PreCheckIn,
    PostCheckOut,
    Routine,
    Incident
}

public enum RoomConditionSeverity
{
    Normal,
    Attention,
    Urgent
}

public enum RoomConditionReportStatus
{
    New,
    InProgress,
    Resolved
}
