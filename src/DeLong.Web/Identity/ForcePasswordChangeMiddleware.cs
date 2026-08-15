namespace DeLong.Web.Identity;

public sealed class ForcePasswordChangeMiddleware(RequestDelegate next)
{
    private static readonly PathString ChangePasswordPath = new("/Account/ChangePassword");
    private static readonly PathString LogoutPath = new("/Account/Logout");

    public async Task InvokeAsync(HttpContext context)
    {
        var mustChange = context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(DeLongClaimTypes.MustChangePassword, "true");

        if (!mustChange || IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Cần đổi mật khẩu",
                detail = "Tài khoản phải đổi mật khẩu tạm trước khi tiếp tục sử dụng hệ thống.",
                status = StatusCodes.Status403Forbidden
            });
            return;
        }

        var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        var target = $"{ChangePasswordPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";
        context.Response.Redirect(target);
    }

    private static bool IsAllowed(PathString path) =>
        path.StartsWithSegments(ChangePasswordPath) ||
        path.StartsWithSegments(LogoutPath) ||
        path.StartsWithSegments("/health");
}
