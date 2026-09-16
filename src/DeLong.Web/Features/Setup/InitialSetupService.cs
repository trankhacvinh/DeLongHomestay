using System.Data;
using DeLong.Web.Data;
using DeLong.Web.Data.Seed;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Setup;

public sealed class InitialSetupService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<InitialSetupService> logger)
{
    private const int SetupAdvisoryLockKey = 1_647_331_921;

    public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
    {
        var hasAdmin = await db.Users.AsNoTracking().AnyAsync(user =>
            !user.IsCustomerAccount &&
            db.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                db.Roles.Any(role => role.Id == userRole.RoleId && role.NormalizedName == "ADMIN")),
            cancellationToken);
        return !hasAdmin;
    }

    public async Task<InitialSetupResult> CompleteAsync(
        InitialSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({SetupAdvisoryLockKey})",
            cancellationToken);

        if (!await IsRequiredAsync(cancellationToken))
            return InitialSetupResult.Failure("Hệ thống đã có tài khoản quản trị. Setup lần đầu đã được khóa.");

        await DbSeeder.SeedRolesAsync(roleManager);

        var email = request.Email.Trim();
        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            IsCustomerAccount = false
        };

        var createResult = await userManager.CreateAsync(admin, request.Password);
        if (!createResult.Succeeded)
            return InitialSetupResult.Failure(FormatIdentityErrors(createResult));

        var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
        if (!roleResult.Succeeded)
            return InitialSetupResult.Failure(FormatIdentityErrors(roleResult));

        if (request.SeedDeLongData)
        {
            await DbSeeder.SeedDeLongAsync(db, cancellationToken);
            if (!await db.UserPropertyAccesses.AnyAsync(
                    x => x.UserId == admin.Id && x.PropertyId == DbSeeder.DeLongPropertyId,
                    cancellationToken))
            {
                db.UserPropertyAccesses.Add(new UserPropertyAccess
                {
                    UserId = admin.Id,
                    PropertyId = DbSeeder.DeLongPropertyId
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Initial setup completed by administrator {AdminUserId}; De Long starter data seeded: {SeedDeLongData}",
            admin.Id,
            request.SeedDeLongData);
        return InitialSetupResult.Success(admin.Id, request.SeedDeLongData);
    }

    private static string FormatIdentityErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));
}

public sealed record InitialSetupRequest(
    string DisplayName,
    string Email,
    string Password,
    bool SeedDeLongData);

public sealed record InitialSetupResult(
    bool Succeeded,
    Guid? AdminUserId,
    bool SeededDeLongData,
    string? Error)
{
    public static InitialSetupResult Success(Guid adminUserId, bool seededDeLongData) =>
        new(true, adminUserId, seededDeLongData, null);

    public static InitialSetupResult Failure(string error) =>
        new(false, null, false, error);
}
