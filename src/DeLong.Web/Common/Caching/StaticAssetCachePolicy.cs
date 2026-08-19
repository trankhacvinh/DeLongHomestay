using Microsoft.AspNetCore.Http;

namespace DeLong.Web.Common.Caching;

public static class StaticAssetCachePolicy
{
    public const string DevelopmentCacheControl = "no-store,no-cache,max-age=0,must-revalidate";
    public const string DevelopmentPolicyHeader = "development-no-store";

    public static void Apply(HttpContext context, bool isDevelopment)
    {
        var headers = context.Response.Headers;

        if (isDevelopment)
        {
            headers.CacheControl = DevelopmentCacheControl;
            headers.Pragma = "no-cache";
            headers.Expires = "0";
            headers["X-DeLong-Cache-Policy"] = DevelopmentPolicyHeader;
            return;
        }

        var request = context.Request;
        headers.CacheControl = request.Query.ContainsKey("v")
            ? "public,max-age=31536000,immutable"
            : request.Path.StartsWithSegments("/uploads/rooms")
                ? "public,max-age=2592000"
                : "public,max-age=3600";
    }
}
