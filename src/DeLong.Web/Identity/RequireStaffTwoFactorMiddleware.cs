namespace DeLong.Web.Identity;

/// <summary>
/// Khi bật <c>Authentication:RequireStaffTwoFactor</c>, tài khoản nhân viên chưa bật Authenticator chỉ được vào trang
/// cài đặt Authenticator (và đổi mật khẩu, đăng xuất) cho tới khi bật xong. Tài khoản khách không bị ảnh hưởng.
/// </summary>
public sealed class RequireStaffTwoFactorMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly PathString AuthenticatorPath = new("/Account/Authenticator");

    public async Task InvokeAsync(HttpContext context)
    {
        var missing = context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(DeLongClaimTypes.TwoFactorMissing, "true") &&
            configuration.GetValue<bool>("Authentication:RequireStaffTwoFactor");
        var path = context.Request.Path;
        var guarded = path.StartsWithSegments("/Admin") || path.StartsWithSegments("/api/admin");
        if (!missing || !guarded)
        {
            await next(context);
            return;
        }

        if (path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Cần bật xác thực 2 lớp",
                detail = "Tài khoản nhân viên phải bật Authenticator trước khi tiếp tục.",
                status = StatusCodes.Status403Forbidden
            });
            return;
        }

        var returnUrl = $"{context.Request.PathBase}{path}{context.Request.QueryString}";
        context.Response.Redirect($"{AuthenticatorPath}?required=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
}
