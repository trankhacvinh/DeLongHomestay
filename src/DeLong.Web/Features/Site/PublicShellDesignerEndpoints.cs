using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Site;

public static class PublicShellDesignerEndpoints
{
    public static IEndpointRouteBuilder MapPublicShellDesignerEndpoints(this IEndpointRouteBuilder app)
    {
        var property = app.MapGroup("/api/admin/properties/{propertyId:guid}/site/designer")
            .RequireAuthorization("ManageSiteContent")
            .AddEndpointFilter<PropertyAccessFilter>();

        property.MapGet("/", async (Guid propertyId, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.Properties.AsNoTracking().AnyAsync(x => x.Id == propertyId, ct)) return Results.NotFound();
            return Results.Ok(await PublicShellDesignerStore.ReadAsync(db, propertyId, ct));
        });

        property.MapPut("/", async (
            Guid propertyId,
            SavePublicShellDesignerRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await db.Properties.AsNoTracking().AnyAsync(x => x.Id == propertyId, ct)) return Results.NotFound();
            var (settings, error) = await PublicShellDesignerStore.SaveAsync(db, propertyId, request, ct);
            return error is null
                ? Results.Ok(settings)
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["designer"] = [error] });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        var global = app.MapGroup("/api/admin/site/global/designer")
            .RequireAuthorization("ManageProperties");

        global.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await PublicShellDesignerStore.ReadAsync(db, null, ct)));

        global.MapPut("/", async (
            SavePublicShellDesignerRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var (settings, error) = await PublicShellDesignerStore.SaveAsync(db, null, request, ct);
            return error is null
                ? Results.Ok(settings)
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["designer"] = [error] });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        app.MapGet("/api/public/site-designer", async (
            string? siteSlug,
            AppDbContext db,
            PublicPropertyResolver resolver,
            CancellationToken ct) =>
        {
            Guid? propertyId = null;
            if (!string.IsNullOrWhiteSpace(siteSlug))
            {
                var resolved = await resolver.ResolveAsync(siteSlug, ct);
                if (resolved is null) return Results.NotFound();
                propertyId = resolved.Id;
            }
            return Results.Ok(await PublicShellDesignerStore.ReadAsync(db, propertyId, ct));
        }).AllowAnonymous();

        return app;
    }
}
