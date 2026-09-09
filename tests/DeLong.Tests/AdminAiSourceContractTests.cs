using Xunit;

namespace DeLong.Tests;

public sealed class AdminAiSourceContractTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Ai_endpoints_are_admin_only_property_scoped_and_antiforgery_protected()
    {
        var program = Read("src/DeLong.Web/Program.cs");
        var endpoints = Read("src/DeLong.Web/Features/AdminAi/AdminAiEndpoints.cs");
        Assert.Contains("UseAdminAi\", policy => policy.RequireRole(\"Admin\")", program);
        Assert.Contains("AddEndpointFilter<PropertyAccessFilter>()", endpoints);
        Assert.Contains("AddEndpointFilter<ApiAntiforgeryFilter>()", endpoints);
        Assert.Contains("RequireRateLimiting(\"admin-ai\")", endpoints);
    }

    [Fact]
    public void Ai_key_is_protected_and_never_returned_by_profile_contract()
    {
        var protector = Read("src/DeLong.Web/Features/AdminAi/AiCredentialProtector.cs");
        var contracts = Read("src/DeLong.Web/Features/AdminAi/AdminAiContracts.cs");
        Assert.Contains("IDataProtectionProvider", protector);
        Assert.Contains("ApiKeyConfigured", contracts);
        Assert.DoesNotContain("ProtectedApiKey", contracts);
    }

    [Fact]
    public void Ai_mutations_require_persisted_preview_and_explicit_apply()
    {
        var service = Read("src/DeLong.Web/Features/AdminAi/AdminAiService.cs");
        Assert.Contains("AiProposalStatus.Pending", service);
        Assert.Contains("ExpiresAtUtc", service);
        Assert.Contains("BeginTransactionAsync", service);
        Assert.Contains("RollbackAsync", service);
        Assert.Contains("Applied:", service);
        Assert.DoesNotContain("ExecuteSql", service);
    }

    [Fact]
    public void Ai_uses_safe_defaults_batches_changes_and_can_update_existing_room_rate()
    {
        var service = Read("src/DeLong.Web/Features/AdminAi/AdminAiService.cs");
        var provider = Read("src/DeLong.Web/Features/AdminAi/AiProviderClient.cs");
        Assert.Contains("AiProposalType.UpdateRoomRate", service);
        Assert.Contains("AiProposalType.Batch", service);
        Assert.Contains("RetryableError", service);
        Assert.Contains("Không yêu cầu Admin xác nhận bằng lời", service);
        Assert.Contains("Mặc định voucher khi Admin không chỉ định", service);
        Assert.Contains("chỉ hỏi lại nếu không xác định được đúng đối tượng", service);
        Assert.Contains("AiResponseProtocol.Parse", service);
        Assert.Contains("type = \"json_schema\"", provider);
    }

    [Fact]
    public void Ai_preview_is_a_readable_table_and_desktop_drawer_is_wider()
    {
        var script = Read("src/DeLong.Web/wwwroot/js/core/admin-ai-chat.js");
        var styles = Read("src/DeLong.Web/wwwroot/css/admin-ai-enhancements.css");
        Assert.Contains("ai-proposal-table", script);
        Assert.Contains("operationLabels", script);
        Assert.Contains("width:min(680px,100vw)", styles);
    }

    [Fact]
    public void Ai_settings_panels_keep_form_and_usage_content_inside_panel_padding()
    {
        var styles = Read("src/DeLong.Web/wwwroot/css/admin-ai.css");

        Assert.Contains(".ai-settings-grid>.panel{min-width:0}", styles, StringComparison.Ordinal);
        Assert.Contains(".ai-settings-grid>.panel>.form-grid{padding:18px 20px 0}", styles, StringComparison.Ordinal);
        Assert.Contains("margin:18px 20px", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Ai_supports_scoped_history_safe_attachments_and_budget_tracking()
    {
        var endpoints = Read("src/DeLong.Web/Features/AdminAi/AdminAiEndpoints.cs");
        var attachmentService = Read("src/DeLong.Web/Features/AdminAi/AiAttachmentService.cs");
        var chat = Read("src/DeLong.Web/wwwroot/js/core/admin-ai-chat.js");
        var settings = Read("src/DeLong.Web/Pages/Admin/Ai/Index.cshtml");
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        Assert.Contains("/conversations/{conversationId:guid}/attachments", endpoints);
        Assert.Contains("PropertyId == propertyId", attachmentService);
        Assert.Contains("x.Conversation.UserId == userId", attachmentService);
        Assert.Contains("UploadedByUserId = userId", attachmentService);
        Assert.Contains("DtdProcessing.Prohibit", attachmentService);
        Assert.Contains("data-ai-history-list", chat);
        Assert.Contains("data-ai-attachment-input", layout);
        Assert.Contains("data-ai-budget", settings);
    }

    [Fact]
    public void Ai_can_preview_safe_site_and_seo_changes_but_not_custom_code()
    {
        var capabilities = Read("src/DeLong.Web/Features/AdminAi/AiCapabilities.cs");
        var configuration = Read("src/DeLong.Web/Features/AdminAi/AdminAiService.Configuration.cs");
        var chat = Read("src/DeLong.Web/wwwroot/js/core/admin-ai-chat.js");

        Assert.Contains("UpdateSiteSettings", capabilities);
        Assert.Contains("metaTitle?", capabilities);
        Assert.Contains("Không được sửa custom CSS/JS", capabilities);
        Assert.Contains("SiteSettingsSnapshot", configuration);
        Assert.Contains("SaveSettingsAsync(proposal.PropertyId", configuration);
        Assert.Contains("false, ct", configuration);
        Assert.Contains("proposal.status === 'Pending'", chat);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src/DeLong.Web"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy workspace root.");
    }
}
