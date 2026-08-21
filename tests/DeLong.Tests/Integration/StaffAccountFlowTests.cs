using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Staff;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class StaffAccountFlowTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Admin_can_create_update_and_reset_staff_with_temporary_password_rules()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<AuditService>();
        services.AddScoped<StaffAccountService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var service = scope.ServiceProvider.GetRequiredService<StaffAccountService>();

        foreach (var roleName in StaffRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid> { Id = Guid.CreateVersion7(), Name = roleName });
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(x => x.Description)));
            }
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property
        {
            Id = Guid.CreateVersion7(),
            Code = $"STAFF-{suffix}",
            Name = $"Staff Test {suffix}",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var adminEmail = $"admin-{suffix}@example.test";
        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = adminEmail,
            Email = adminEmail,
            DisplayName = "Integration Admin",
            EmailConfirmed = true,
            IsActive = true,
            LockoutEnabled = true
        };
        var adminCreate = await userManager.CreateAsync(admin, "Admin-temp-123");
        Assert.True(adminCreate.Succeeded, string.Join("; ", adminCreate.Errors.Select(x => x.Description)));
        var adminRole = await userManager.AddToRoleAsync(admin, StaffRoles.Admin);
        Assert.True(adminRole.Succeeded, string.Join("; ", adminRole.Errors.Select(x => x.Description)));
        db.UserPropertyAccesses.Add(new UserPropertyAccess { UserId = admin.Id, PropertyId = property.Id });
        await db.SaveChangesAsync();

        var staffEmail = $"staff-{suffix}@example.test";
        var temporaryPassword = "Temp-staff-123";
        var (created, createError) = await service.CreateAsync(
            admin.Id,
            new CreateStaffAccountRequest(
                "Nhân viên Test",
                $"staff-{suffix}",
                staffEmail,
                StaffRoles.Staff,
                [property.Id],
                temporaryPassword));

        Assert.Null(createError);
        Assert.NotNull(created);
        Assert.True(created!.IsActive);
        Assert.True(created.MustChangePassword);
        Assert.Equal(StaffRoles.Staff, created.Role);
        Assert.Single(created.Properties);
        Assert.Equal(property.Id, created.Properties[0].Id);

        var staffUser = await userManager.FindByEmailAsync(staffEmail);
        Assert.NotNull(staffUser);
        Assert.True(await userManager.CheckPasswordAsync(staffUser!, temporaryPassword));
        Assert.True(await userManager.IsInRoleAsync(staffUser!, StaffRoles.Staff));
        Assert.True(await db.UserPropertyAccesses.AnyAsync(x => x.UserId == staffUser!.Id && x.PropertyId == property.Id));

        // Simulate historical/bad data with more than one operational role. Updating the
        // account must normalize it back to exactly the role selected by the Admin.
        var extraRole = await userManager.AddToRoleAsync(staffUser!, StaffRoles.Viewer);
        Assert.True(extraRole.Succeeded, string.Join("; ", extraRole.Errors.Select(x => x.Description)));

        var (updated, updateError) = await service.UpdateAsync(
            admin.Id,
            staffUser!.Id,
            new UpdateStaffAccountRequest(
                "Nhân viên Dọn phòng",
                $"staff-{suffix}",
                staffEmail,
                StaffRoles.Housekeeping,
                [property.Id],
                true));

        Assert.Null(updateError);
        Assert.NotNull(updated);
        Assert.Equal(StaffRoles.Housekeeping, updated!.Role);
        Assert.True(await userManager.IsInRoleAsync(staffUser, StaffRoles.Housekeeping));
        Assert.False(await userManager.IsInRoleAsync(staffUser, StaffRoles.Staff));
        Assert.False(await userManager.IsInRoleAsync(staffUser, StaffRoles.Viewer));
        Assert.Single((await userManager.GetRolesAsync(staffUser)).Where(StaffRoles.IsAllowed));

        var resetPassword = "Reset-staff-456";
        var resetError = await service.ResetPasswordAsync(
            admin.Id,
            staffUser.Id,
            new ResetStaffPasswordRequest(resetPassword));
        Assert.Null(resetError);

        staffUser = await userManager.FindByIdAsync(staffUser.Id.ToString());
        Assert.NotNull(staffUser);
        Assert.True(staffUser!.MustChangePassword);
        Assert.True(await userManager.CheckPasswordAsync(staffUser, resetPassword));
        Assert.False(await userManager.CheckPasswordAsync(staffUser, temporaryPassword));

        var (_, selfDemoteError) = await service.UpdateAsync(
            admin.Id,
            admin.Id,
            new UpdateStaffAccountRequest(
                admin.DisplayName,
                admin.UserName!,
                admin.Email!,
                StaffRoles.Manager,
                [property.Id],
                true));
        Assert.Equal("Bạn không thể tự thay đổi vai trò của chính mình.", selfDemoteError);

        var (_, selfDeactivateError) = await service.UpdateAsync(
            admin.Id,
            admin.Id,
            new UpdateStaffAccountRequest(
                admin.DisplayName,
                admin.UserName!,
                admin.Email!,
                StaffRoles.Admin,
                [property.Id],
                false));
        Assert.Equal("Bạn không thể tự ngừng tài khoản đang đăng nhập.", selfDeactivateError);
    }
}
