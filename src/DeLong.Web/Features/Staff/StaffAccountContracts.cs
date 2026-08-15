namespace DeLong.Web.Features.Staff;

public static class StaffRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";
    public const string Housekeeping = "Housekeeping";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Manager, Staff, Housekeeping, Viewer];

    public static bool IsAllowed(string? role) =>
        !string.IsNullOrWhiteSpace(role) && All.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string role) =>
        All.Single(x => string.Equals(x, role.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record StaffRoleOptionDto(
    string Value,
    string Label,
    string Description,
    string Tone);

public sealed record StaffPropertyDto(Guid Id, string Name, string Code);

public sealed record StaffAccountDto(
    Guid Id,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    bool IsSelf,
    bool CanManage,
    IReadOnlyList<StaffPropertyDto> Properties);

public sealed record StaffPageDataDto(
    Guid CurrentUserId,
    IReadOnlyList<StaffAccountDto> Accounts,
    IReadOnlyList<StaffRoleOptionDto> Roles,
    IReadOnlyList<StaffPropertyDto> AvailableProperties);

public sealed record CreateStaffAccountRequest(
    string DisplayName,
    string Email,
    string Role,
    IReadOnlyList<Guid> PropertyIds,
    string TemporaryPassword);

public sealed record UpdateStaffAccountRequest(
    string DisplayName,
    string Email,
    string Role,
    IReadOnlyList<Guid> PropertyIds,
    bool IsActive);

public sealed record ResetStaffPasswordRequest(string TemporaryPassword);
