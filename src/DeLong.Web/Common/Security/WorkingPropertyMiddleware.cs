using Microsoft.AspNetCore.WebUtilities;

namespace DeLong.Web.Common.Security;

public sealed class WorkingPropertyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentPropertyService currentPropertyService)
    {
        if (!ShouldResolveWorkingProperty(context))
        {
            await next(context);
            return;
        }

        var accessible = await currentPropertyService.GetAccessibleAsync(context.User, context.RequestAborted);
        if (accessible.Count == 0)
        {
            await next(context);
            return;
        }

        Guid? requestedPropertyId = null;
        var requestedValue = context.Request.Query["propertyId"].ToString();
        if (Guid.TryParse(requestedValue, out var parsed)) requestedPropertyId = parsed;

        var current = await currentPropertyService.ResolveAsync(
            context.User,
            requestedPropertyId,
            context.RequestAborted);

        if (current is not null)
        {
            await next(context);
            return;
        }

        // More than one accessible property and no valid explicit/remembered selection.
        // Never choose the first property implicitly: force an intentional working context.
        var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        var selectionUrl = QueryHelpers.AddQueryString("/Admin/SelectProperty", "returnUrl", returnUrl);
        context.Response.Redirect(selectionUrl);
    }

    private static bool ShouldResolveWorkingProperty(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return false;
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) return false;
        if (!context.Request.Path.StartsWithSegments("/Admin")) return false;
        if (context.Request.Path.StartsWithSegments("/Admin/SelectProperty")) return false;
        return true;
    }
}
