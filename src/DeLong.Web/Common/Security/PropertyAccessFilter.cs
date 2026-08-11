namespace DeLong.Web.Common.Security;

public sealed class PropertyAccessFilter(PropertyAccessService propertyAccess) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var routeValue = context.HttpContext.Request.RouteValues["propertyId"]?.ToString();
        if (!Guid.TryParse(routeValue, out var propertyId))
        {
            return Results.BadRequest();
        }

        var allowed = await propertyAccess.CanAccessAsync(
            context.HttpContext.User,
            propertyId,
            context.HttpContext.RequestAborted);

        return allowed ? await next(context) : Results.Forbid();
    }
}
