using System.ComponentModel.DataAnnotations;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Staff;

public sealed class StaffAccountService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AuditService auditService)
{
    private static readonly StaffRoleOptionDto[] RoleOptions =
    [
        new(StaffRoles.Admin, "Quản trị viên", "Toàn quyền hệ thống, cấu hình và quản lý tài khoản nhân viên.", "danger"),
        new(StaffRoles.Manager, "Quản lý", "Quản lý phòng, đặt phòng, vận hành, tài chính và báo cáo.", "warning"),
        new(StaffRoles.Staff, "Nhân viên", "Xử lý khách hàng, đặt phòng, thanh toán và hỗ trợ vận hành.", "info"),
        new(StaffRoles.Housekeeping, "Dọn phòng", "Tập trung vào trạng thái phòng và quy trình dọn phòng.", "success"),
        new(StaffRoles.Viewer, "Chỉ xem", "Xem dữ liệu được cấp quyền nhưng không thực hiện thao tác thay đổi.", "neutral")
    ];

    public async Task<StaffPageDataDto> GetPageDataAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var availableProperties = await GetActorPropertiesAsync(actorUserId, cancellationToken);
        var actorPropertyIds = availableProperties.Select(x => x.Id).ToHashSet();
        if (actorPropertyIds.Count == 0)
        {
            return new StaffPageDataDto(actorUserId, [], RoleOptions, []);
        }

        var userIds = await db.UserPropertyAccesses
            .AsNoTracking()
            .Where(x => actorPropertyIds.Contains(x.PropertyId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.DisplayName)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var accessRows = await (
            from access in db.UserPropertyAccesses.AsNoTracking()
            join property in db.Properties.AsNoTracking() on access.PropertyId equals property.Id
            where userIds.Contains(access.UserId)
            select new
            {
                access.UserId,
                Property = new StaffPropertyDto(property.Id, property.Name, property.Code)
            }).ToListAsync(cancellationToken);

        var roleRows = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role.Name! })
            .ToListAsync(cancellationToken);

        var propertiesByUser = accessRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<StaffPropertyDto>)group.Select(x => x.Property).OrderBy(x => x.Name).ToList());

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => StaffRoles.All.FirstOrDefault(allowed => group.Any(x => string.Equals(x.Role, allowed, StringComparison.OrdinalIgnoreCase)))
                    ?? group.Select(x => x.Role).FirstOrDefault()
                    ?? string.Empty);

        var now = DateTimeOffset.UtcNow;
        var accounts = users.Select(user =>
        {
            var properties = propertiesByUser.GetValueOrDefault(user.Id) ?? [];
            var canManage = properties.Count > 0 && properties.All(property => actorPropertyIds.Contains(property.Id));
            return new StaffAccountDto(
                user.Id,
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? user.UserName ?? "Nhân viên" : user.DisplayName,
                user.Email ?? user.UserName ?? string.Empty,
                rolesByUser.GetValueOrDefault(user.Id) ?? string.Empty,
                user.IsActive,
                user.MustChangePassword,
                user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
                user.LockoutEnd,
                user.Id == actorUserId,
                canManage,
                properties);
        }).ToList();

        return new StaffPageDataDto(actorUserId, accounts, RoleOptions, availableProperties);
    }

    public async Task<(StaffAccountDto? Account, string? Error)> CreateAsync(
        Guid actorUserId,
        CreateStaffAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCommon(request.DisplayName, request.Email, request.Role, request.PropertyIds);
        if (validation is not null) return (null, validation);
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8)
            return (null, "Mật khẩu tạm phải có ít nhất 8 ký tự.");

        var actorProperties = await GetActorPropertiesAsync(actorUserId, cancellationToken);
        var actorPropertyIds = actorProperties.Select(x => x.Id).ToHashSet();
        var requestedPropertyIds = request.PropertyIds.Distinct().ToHashSet();
        if (!requestedPropertyIds.IsSubsetOf(actorPropertyIds))
            return (null, "Bạn không thể cấp quyền cho cơ sở mà tài khoản của bạn không quản lý.");

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return (null, "Email này đã có tài khoản.");

        var roleName = StaffRoles.Normalize(request.Role);
        var roleError = await EnsureRoleAsync(roleName);
        if (roleError is not null) return (null, roleError);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, request.TemporaryPassword);
        if (!createResult.Succeeded) return (null, IdentityErrors(createResult));

        var roleResult = await userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded) return (null, IdentityErrors(roleResult));

        foreach (var propertyId in requestedPropertyIds)
        {
            db.UserPropertyAccesses.Add(new UserPropertyAccess { UserId = user.Id, PropertyId = propertyId });
            auditService.Add(
                propertyId,
                "StaffAccount",
                user.Id,
                "Created",
                actorUserId,
                after: new { user.DisplayName, user.Email, Role = roleName, PropertyIds = requestedPropertyIds, user.IsActive });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAccountAsync(actorUserId, user.Id, cancellationToken), null);
    }

    public async Task<(StaffAccountDto? Account, string? Error)> UpdateAsync(
        Guid actorUserId,
        Guid userId,
        UpdateStaffAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCommon(request.DisplayName, request.Email, request.Role, request.PropertyIds);
        if (validation is not null) return (null, validation);

        var actorProperties = await GetActorPropertiesAsync(actorUserId, cancellationToken);
        var actorPropertyIds = actorProperties.Select(x => x.Id).ToHashSet();
        var newPropertyIds = request.PropertyIds.Distinct().ToHashSet();
        if (!newPropertyIds.IsSubsetOf(actorPropertyIds))
            return (null, "Bạn không thể cấp quyền cho cơ sở mà tài khoản của bạn không quản lý.");

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return (null, "Không tìm thấy tài khoản.");

        var currentAccesses = await db.UserPropertyAccesses
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var currentPropertyIds = currentAccesses.Select(x => x.PropertyId).ToHashSet();
        if (currentPropertyIds.Count == 0 || !currentPropertyIds.IsSubsetOf(actorPropertyIds))
            return (null, "Tài khoản này có quyền ở cơ sở nằm ngoài phạm vi quản lý của bạn.");

        var currentRoles = await userManager.GetRolesAsync(user);
        var currentRole = StaffRoles.All.FirstOrDefault(allowed => currentRoles.Contains(allowed, StringComparer.OrdinalIgnoreCase)) ?? string.Empty;
        var newRole = StaffRoles.Normalize(request.Role);

        if (userId == actorUserId)
        {
            if (!request.IsActive) return (null, "Bạn không thể tự ngừng tài khoản đang đăng nhập.");
            if (!string.Equals(currentRole, newRole, StringComparison.OrdinalIgnoreCase))
                return (null, "Bạn không thể tự thay đổi vai trò của chính mình.");
            if (!currentPropertyIds.SetEquals(newPropertyIds))
                return (null, "Bạn không thể tự thay đổi quyền cơ sở của chính mình.");
        }

        var coverageError = await ValidateAdminCoverageAsync(
            user,
            currentRole,
            currentPropertyIds,
            newRole,
            newPropertyIds,
            request.IsActive,
            cancellationToken);
        if (coverageError is not null) return (null, coverageError);

        var email = request.Email.Trim();
        var duplicate = await userManager.FindByEmailAsync(email);
        if (duplicate is not null && duplicate.Id != userId)
            return (null, "Email này đã có tài khoản.");

        var roleError = await EnsureRoleAsync(newRole);
        if (roleError is not null) return (null, roleError);

        var before = new
        {
            user.DisplayName,
            user.Email,
            Role = currentRole,
            PropertyIds = currentPropertyIds.Order().ToArray(),
            user.IsActive
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded) return (null, IdentityErrors(emailResult));
            var usernameResult = await userManager.SetUserNameAsync(user, email);
            if (!usernameResult.Succeeded) return (null, IdentityErrors(usernameResult));
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return (null, IdentityErrors(updateResult));

        var allowedCurrentRoles = currentRoles.Where(role => StaffRoles.IsAllowed(role)).ToArray();
        if (allowedCurrentRoles.Length > 0 && !allowedCurrentRoles.Contains(newRole, StringComparer.OrdinalIgnoreCase))
        {
            var removeRoleResult = await userManager.RemoveFromRolesAsync(user, allowedCurrentRoles);
            if (!removeRoleResult.Succeeded) return (null, IdentityErrors(removeRoleResult));
        }
        if (!await userManager.IsInRoleAsync(user, newRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, newRole);
            if (!addRoleResult.Succeeded) return (null, IdentityErrors(addRoleResult));
        }

        var removeAccesses = currentAccesses.Where(x => !newPropertyIds.Contains(x.PropertyId)).ToArray();
        db.UserPropertyAccesses.RemoveRange(removeAccesses);
        foreach (var propertyId in newPropertyIds.Where(id => !currentPropertyIds.Contains(id)))
        {
            db.UserPropertyAccesses.Add(new UserPropertyAccess { UserId = user.Id, PropertyId = propertyId });
        }

        if (request.IsActive && !before.IsActive)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
        }

        await userManager.UpdateSecurityStampAsync(user);

        var auditProperties = currentPropertyIds.Union(newPropertyIds).Where(actorPropertyIds.Contains).Distinct().ToArray();
        foreach (var propertyId in auditProperties)
        {
            auditService.Add(
                propertyId,
                "StaffAccount",
                user.Id,
                "Updated",
                actorUserId,
                before,
                new
                {
                    user.DisplayName,
                    user.Email,
                    Role = newRole,
                    PropertyIds = newPropertyIds.Order().ToArray(),
                    user.IsActive
                });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAccountAsync(actorUserId, user.Id, cancellationToken), null);
    }

    public async Task<string?> ResetPasswordAsync(
        Guid actorUserId,
        Guid userId,
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == actorUserId) return "Hãy dùng trang Đổi mật khẩu cho tài khoản của chính bạn.";
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8)
            return "Mật khẩu tạm phải có ít nhất 8 ký tự.";

        var managed = await GetManagedUserAsync(actorUserId, userId, cancellationToken);
        if (managed.Error is not null) return managed.Error;
        var user = managed.User!;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, request.TemporaryPassword);
        if (!reset.Succeeded) return IdentityErrors(reset);

        user.MustChangePassword = true;
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityErrors(update);
        await userManager.UpdateSecurityStampAsync(user);

        foreach (var propertyId in managed.PropertyIds)
        {
            auditService.Add(propertyId, "StaffAccount", user.Id, "PasswordReset", actorUserId, after: new { user.MustChangePassword });
        }
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> UnlockAsync(
        Guid actorUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var managed = await GetManagedUserAsync(actorUserId, userId, cancellationToken);
        if (managed.Error is not null) return managed.Error;
        var user = managed.User!;

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        foreach (var propertyId in managed.PropertyIds)
        {
            auditService.Add(propertyId, "StaffAccount", user.Id, "Unlocked", actorUserId);
        }
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<StaffAccountDto?> GetAccountAsync(Guid actorUserId, Guid userId, CancellationToken cancellationToken)
    {
        var data = await GetPageDataAsync(actorUserId, cancellationToken);
        return data.Accounts.SingleOrDefault(x => x.Id == userId);
    }

    private async Task<IReadOnlyList<StaffPropertyDto>> GetActorPropertiesAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        return await (
            from access in db.UserPropertyAccesses.AsNoTracking()
            join property in db.Properties.AsNoTracking() on access.PropertyId equals property.Id
            where access.UserId == actorUserId && property.IsActive
            orderby property.Name
            select new StaffPropertyDto(property.Id, property.Name, property.Code))
            .ToListAsync(cancellationToken);
    }

    private async Task<(ApplicationUser? User, HashSet<Guid> PropertyIds, string? Error)> GetManagedUserAsync(
        Guid actorUserId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var actorPropertyIds = (await GetActorPropertiesAsync(actorUserId, cancellationToken)).Select(x => x.Id).ToHashSet();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return (null, [], "Không tìm thấy tài khoản.");

        var targetPropertyIds = (await db.UserPropertyAccesses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PropertyId)
            .ToListAsync(cancellationToken)).ToHashSet();

        if (targetPropertyIds.Count == 0 || !targetPropertyIds.IsSubsetOf(actorPropertyIds))
            return (null, targetPropertyIds, "Tài khoản này có quyền ở cơ sở nằm ngoài phạm vi quản lý của bạn.");

        return (user, targetPropertyIds, null);
    }

    private async Task<string?> ValidateAdminCoverageAsync(
        ApplicationUser user,
        string currentRole,
        HashSet<Guid> currentPropertyIds,
        string newRole,
        HashSet<Guid> newPropertyIds,
        bool newIsActive,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive || !string.Equals(currentRole, StaffRoles.Admin, StringComparison.OrdinalIgnoreCase)) return null;

        var propertiesLosingAdmin = currentPropertyIds
            .Where(propertyId => !newIsActive || !string.Equals(newRole, StaffRoles.Admin, StringComparison.OrdinalIgnoreCase) || !newPropertyIds.Contains(propertyId))
            .ToArray();
        if (propertiesLosingAdmin.Length == 0) return null;

        var adminRoleId = await db.Roles
            .AsNoTracking()
            .Where(x => x.Name == StaffRoles.Admin)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!adminRoleId.HasValue) return "Hệ thống chưa có vai trò Admin.";

        var coveredProperties = await (
            from access in db.UserPropertyAccesses.AsNoTracking()
            join otherUser in db.Users.AsNoTracking() on access.UserId equals otherUser.Id
            join userRole in db.UserRoles.AsNoTracking() on otherUser.Id equals userRole.UserId
            where otherUser.Id != user.Id &&
                  otherUser.IsActive &&
                  userRole.RoleId == adminRoleId.Value &&
                  propertiesLosingAdmin.Contains(access.PropertyId)
            select access.PropertyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missing = propertiesLosingAdmin.Except(coveredProperties).ToArray();
        if (missing.Length == 0) return null;

        var names = await db.Properties.AsNoTracking()
            .Where(x => missing.Contains(x.Id))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
        return $"Không thể thay đổi: {string.Join(", ", names)} cần còn ít nhất một tài khoản Admin đang hoạt động.";
    }

    private async Task<string?> EnsureRoleAsync(string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName)) return null;
        var result = await roleManager.CreateAsync(new IdentityRole<Guid> { Id = Guid.CreateVersion7(), Name = roleName });
        return result.Succeeded ? null : IdentityErrors(result);
    }

    private static string? ValidateCommon(string displayName, string email, string role, IReadOnlyList<Guid> propertyIds)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "Tên hiển thị là bắt buộc.";
        if (displayName.Trim().Length > 200) return "Tên hiển thị tối đa 200 ký tự.";
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim())) return "Email không hợp lệ.";
        if (email.Trim().Length > 254) return "Email tối đa 254 ký tự.";
        if (!StaffRoles.IsAllowed(role)) return "Vai trò không hợp lệ.";
        if (propertyIds is null || propertyIds.Distinct().Count() == 0) return "Chọn ít nhất một cơ sở được truy cập.";
        return null;
    }

    private static string IdentityErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => "Email này đã có tài khoản.",
            "InvalidEmail" or "InvalidUserName" => "Email không hợp lệ.",
            "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
            _ => error.Description
        }));
}
