using Microsoft.AspNetCore.Mvc;

namespace DeLong.Web.Features.PublicAi;

public static class PublicAiEndpoints
{
    public static IEndpointRouteBuilder MapPublicAiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/ai/status", async ([FromQuery] string? siteSlug, PublicAiService service, CancellationToken ct) =>
            Results.Ok(new { enabled = await service.IsEnabledAsync(siteSlug, ct) })).AllowAnonymous().RequireRateLimiting("public-ai");
        app.MapPost("/api/public/ai/chat", async (HttpContext http, [FromQuery] string? siteSlug, PublicAiChatRequest request, PublicAiService service, CancellationToken ct) =>
        {
            var (value, error) = await service.ChatAsync(siteSlug, request, http.Connection.RemoteIpAddress?.ToString(), ct);
            return error is null ? Results.Ok(value) : Results.Problem(title: "Không thể trả lời", detail: error, statusCode: 400);
        }).AllowAnonymous().RequireRateLimiting("public-ai");
        return app;
    }
}
