namespace DeLong.Web.Common.Security;

/// <summary>
/// Content-Security-Policy values. The public site keeps admin-managed custom code, inline
/// bootstrap scripts and in-DOM Vue templates working, so script-src is not restricted yet.
/// The baseline still blocks plugins, &lt;base&gt; hijacking and framing by other sites. Staff
/// pages, where CCCD photos are viewed, also forbid network requests and form posts to other
/// origins so an injected script cannot send guest data elsewhere.
/// </summary>
public static class SecurityHeaders
{
    public const string BaselinePolicy = "object-src 'none'; base-uri 'self'; frame-ancestors 'self'";

    public const string StaffPolicy = BaselinePolicy + "; connect-src 'self' blob: data:; form-action 'self'";

    public static string ContentSecurityPolicyFor(PathString path) => IsStaffArea(path) ? StaffPolicy : BaselinePolicy;

    public static bool IsStaffArea(PathString path) =>
        path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase);
}
