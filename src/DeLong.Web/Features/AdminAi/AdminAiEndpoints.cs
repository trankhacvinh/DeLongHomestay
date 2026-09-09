using System.Security.Claims;
using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Mvc;

namespace DeLong.Web.Features.AdminAi;

public static class AdminAiEndpoints
{
    public static IEndpointRouteBuilder MapAdminAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/ai")
            .RequireAuthorization("UseAdminAi").AddEndpointFilter<PropertyAccessFilter>().WithTags("Admin AI");
        group.MapGet("/profile", async (Guid propertyId, AdminAiSettingsService service, CancellationToken ct) => Results.Ok(await service.GetAsync(propertyId, ct)));
        group.MapPut("/profile", async (Guid propertyId, SaveAiProfileRequest request, ClaimsPrincipal user, AdminAiSettingsService service, CancellationToken ct) =>
        {
            var (value, error) = await service.SaveAsync(propertyId, request, UserId(user), ct);
            return error is null ? Results.Ok(value) : Problem(error);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/chat", async (Guid propertyId, AiChatRequest request, ClaimsPrincipal user, AdminAiService service, CancellationToken ct) =>
        {
            var (value, error) = await service.ChatAsync(propertyId, UserId(user), request, ct);
            return error is null ? Results.Ok(value) : Problem(error);
        }).RequireRateLimiting("admin-ai").AddEndpointFilter<ApiAntiforgeryFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(32L * 1024 * 1024));
        group.MapGet("/usage", async (Guid propertyId, AdminAiService service, CancellationToken ct) => Results.Ok(await service.UsageAsync(propertyId, ct)));
        group.MapGet("/conversations", async (Guid propertyId, ClaimsPrincipal user, AdminAiService service, CancellationToken ct) => Results.Ok(await service.ConversationsAsync(propertyId, UserId(user), ct)));
        group.MapPost("/conversations", async (Guid propertyId, ClaimsPrincipal user, AdminAiService service, CancellationToken ct) =>
            Results.Ok(new { conversationId = await service.CreateConversationAsync(propertyId, UserId(user), ct) }))
            .RequireRateLimiting("admin-ai").AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapGet("/conversations/{conversationId:guid}", async (Guid propertyId, Guid conversationId, ClaimsPrincipal user, AdminAiService service, CancellationToken ct) => Results.Ok(await service.MessagesAsync(propertyId, UserId(user), conversationId, ct)));
        group.MapGet("/conversations/{conversationId:guid}/attachments", async (Guid propertyId, Guid conversationId, ClaimsPrincipal user, AiAttachmentService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(propertyId, UserId(user), conversationId, ct)));
        group.MapPost("/conversations/{conversationId:guid}/attachments", async (Guid propertyId, Guid conversationId, ClaimsPrincipal user, HttpRequest request, AiAttachmentService service, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Problem("Yêu cầu tải tệp không hợp lệ.");
            var form = await request.ReadFormAsync(ct);
            var (value, error) = await service.UploadAsync(propertyId, UserId(user), conversationId, form.Files, ct);
            return error is null ? Results.Ok(value) : Problem(error);
        }).RequireRateLimiting("admin-ai").AddEndpointFilter<ApiAntiforgeryFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(32L * 1024 * 1024));
        group.MapPost("/proposals/{proposalId:guid}/apply", async (Guid propertyId, Guid proposalId, ClaimsPrincipal user, AdminAiService service, CancellationToken ct) =>
        {
            var (value, error) = await service.ApplyAsync(propertyId, UserId(user), proposalId, ct);
            return error is null ? Results.Ok(value) : Problem(error, value);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        group.MapPost("/proposals/{proposalId:guid}/reject", async (Guid propertyId, Guid proposalId, ClaimsPrincipal user, AdminAiService service, CancellationToken ct) =>
        {
            var (value, error) = await service.RejectAsync(propertyId, UserId(user), proposalId, ct);
            return error is null ? Results.Ok(value) : Problem(error, value);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();
        return app;
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Problem(string detail, object? data = null) => Results.Problem(title: "Không thể xử lý trợ lý AI", detail: detail, statusCode: 400, extensions: data is null ? null : new Dictionary<string, object?> { ["proposal"] = data });
}
