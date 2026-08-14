using System.Security.Claims;
using DeLong.Web.Common.Security;

namespace DeLong.Web.Features.Staff;

public static class StaffAccountEndpoints
{
    public static IEndpointRouteBuilder MapStaffAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/staff")
            .RequireAuthorization("ManageStaff")
            .WithTags("Staff");

        group.MapGet("/", async (
            ClaimsPrincipal user,
            StaffAccountService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId)) return Results.Unauthorized();
            return Results.Ok(await service.GetPageDataAsync(actorUserId, cancellationToken));
        });

        group.MapPost("/", async (
            ClaimsPrincipal user,
            CreateStaffAccountRequest request,
            StaffAccountService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId)) return Results.Unauthorized();
            var (account, error) = await service.CreateAsync(actorUserId, request, cancellationToken);
            return error is not null
                ? Results.Problem(title: "Không thể tạo tài khoản", detail: error, statusCode: StatusCodes.Status400BadRequest)
                : Results.Created($"/api/admin/staff/{account!.Id}", account);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/{userId:guid}", async (
            ClaimsPrincipal user,
            Guid userId,
            UpdateStaffAccountRequest request,
            StaffAccountService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId)) return Results.Unauthorized();
            var (account, error) = await service.UpdateAsync(actorUserId, userId, request, cancellationToken);
            if (account is null && error == "Không tìm thấy tài khoản.") return Results.NotFound();
            return error is not null
                ? Results.Problem(title: "Không thể cập nhật tài khoản", detail: error, statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(account);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/{userId:guid}/reset-password", async (
            ClaimsPrincipal user,
            Guid userId,
            ResetStaffPasswordRequest request,
            StaffAccountService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId)) return Results.Unauthorized();
            var error = await service.ResetPasswordAsync(actorUserId, userId, request, cancellationToken);
            return error is null
                ? Results.NoContent()
                : Results.Problem(title: "Không thể đặt lại mật khẩu", detail: error, statusCode: StatusCodes.Status400BadRequest);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/{userId:guid}/unlock", async (
            ClaimsPrincipal user,
            Guid userId,
            StaffAccountService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId)) return Results.Unauthorized();
            var error = await service.UnlockAsync(actorUserId, userId, cancellationToken);
            return error is null
                ? Results.NoContent()
                : Results.Problem(title: "Không thể mở khóa tài khoản", detail: error, statusCode: StatusCodes.Status400BadRequest);
        })
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
