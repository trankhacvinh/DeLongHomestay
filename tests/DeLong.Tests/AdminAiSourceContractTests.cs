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
    public void Ai_supports_deepseek_without_weakening_the_existing_proposal_protocol()
    {
        var provider = Read("src/DeLong.Web/Features/AdminAi/AiProviderClient.cs");
        var settings = Read("src/DeLong.Web/Pages/Admin/Ai/Index.cshtml");

        Assert.Contains("https://api.deepseek.com/chat/completions", provider);
        Assert.Contains("response_format = new { type = \"json_object\" }", provider);
        Assert.Contains("AiResponseProtocol.Schema", provider);
        Assert.Contains("DeepSeek", settings);
    }

    [Fact]
    public void Ai_preview_is_a_readable_table_and_desktop_drawer_is_wider()
    {
        var script = Read("src/DeLong.Web/wwwroot/js/core/admin-ai-chat.js");
        var styles = Read("src/DeLong.Web/wwwroot/css/admin-ai-enhancements.css");
        Assert.Contains("ai-proposal-table", script);
        Assert.Contains("operationLabels", script);
        Assert.Contains(".ai-chat-drawer[data-ai-chat-drawer]{width:min(920px,100vw)}", styles);
        Assert.Contains("grid-template-columns:48px minmax(0,1fr) 88px", styles);
        Assert.Contains("looksLikeHtml(content)", script);
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

    [Fact]
    public void Public_knowledge_snapshot_excludes_customer_payment_secret_and_post_booking_guide_data()
    {
        var snapshot = Read("src/DeLong.Web/Features/AdminAi/AiKnowledgeSnapshotService.cs");
        var endpoints = Read("src/DeLong.Web/Features/AdminAi/AdminAiEndpoints.cs");

        Assert.DoesNotContain("ProtectedApiKey", snapshot);
        Assert.DoesNotContain("GuestGuideHtml", snapshot);
        Assert.DoesNotContain("db.Customers", snapshot);
        Assert.DoesNotContain("db.Payments", snapshot);
        Assert.Contains("/knowledge/rebuild", endpoints);
        Assert.Contains("AddEndpointFilter<ApiAntiforgeryFilter>()", endpoints);
    }

    [Fact]
    public void Public_ai_is_feature_flagged_rate_limited_and_rejects_mutation_output()
    {
        var service = Read("src/DeLong.Web/Features/PublicAi/PublicAiService.cs");
        var endpoints = Read("src/DeLong.Web/Features/PublicAi/PublicAiEndpoints.cs");
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");

        Assert.Contains("AiAudience.Customer", service);
        Assert.Contains("PublicAiResponseProtocol.Parse", service);
        Assert.Contains("Math.Min(profile.MaxOutputTokens, 800)", service);
        Assert.Contains("publicBooking.GetAvailabilityAsync", service);
        Assert.Contains("PublicAiQuestionAnalyzer.Analyze", service);
        Assert.Contains("TimeSpan.FromSeconds(30)", service);
        Assert.Contains("question.BookingCode is null || question.Phone is null", service);
        Assert.Contains("bookingLookup.LookupAsync", service);
        Assert.Contains("Trợ lý không tự thay đổi booking", service);
        Assert.Contains("PublicSlotSelectionRules.ValidateConsecutive", service);
        Assert.Contains("pricingService.CalculateAsync", service);
        Assert.Contains("DateTime.UtcNow.AddMinutes(30)", service);
        Assert.Contains("SHA256.HashData", service);
        Assert.DoesNotContain("CreateRequestAsync", service);
        Assert.DoesNotContain("db.Bookings", service);
        Assert.DoesNotContain("db.Customers", service);
        Assert.Contains("RequireRateLimiting(\"public-ai\")", endpoints);
        Assert.Contains("AllowAnonymous()", endpoints);
        Assert.Contains("data-public-ai-drawer", layout);
        Assert.Contains("@if (!isAdmin)\n{\n    <button class=\"public-ai-launch\"", layout.Replace("\r\n", "\n"));
        Assert.DoesNotContain("@if (!isAdmin && !isGlobalPublic)\n{\n    <button class=\"public-ai-launch\"", layout.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Public_ai_history_is_local_safe_and_booking_lookup_has_a_separate_guard()
    {
        var chat = Read("src/DeLong.Web/wwwroot/js/core/public-ai-chat.js");
        var guard = Read("src/DeLong.Web/Features/PublicAi/PublicAiLookupRateGuard.cs");
        var endpoints = Read("src/DeLong.Web/Features/PublicAi/PublicAiEndpoints.cs");
        var service = Read("src/DeLong.Web/Features/PublicAi/PublicAiService.cs");

        Assert.Contains("delong.publicAiHistory", chat);
        Assert.Contains("30 * 86400000", chat);
        Assert.Contains("conversation.messages.slice(-30)", chat);
        Assert.Contains("copy.textContent = text", chat);
        Assert.DoesNotContain("innerHTML", chat);
        Assert.Contains("response.suggestions", chat);
        Assert.Contains("public-ai-suggestions", chat);
        Assert.Contains("room.Rates.Where(x => x.Available)", service);
        Assert.Contains(".public-ai-drawer[hidden],.public-ai-backdrop[hidden]{display:none!important}",
            Read("src/DeLong.Web/wwwroot/css/public-ai.css"));
        Assert.Contains("height:100dvh", Read("src/DeLong.Web/wwwroot/css/public-ai.css"));
        Assert.Contains("MaximumAttempts = 5", guard);
        Assert.Contains("TimeSpan.FromMinutes(10)", guard);
        Assert.Contains("SHA256.HashData", guard);
        Assert.Contains("RemoteIpAddress", endpoints);
    }

    [Fact]
    public void Staff_ai_is_property_scoped_read_only_audited_and_finance_gated()
    {
        var program = Read("src/DeLong.Web/Program.cs");
        var endpoints = Read("src/DeLong.Web/Features/PublicAi/StaffAiEndpoints.cs");
        var service = Read("src/DeLong.Web/Features/PublicAi/StaffAiService.cs");
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");

        Assert.Contains("UseStaffAi", program);
        Assert.Contains("AddEndpointFilter<PropertyAccessFilter>()", endpoints);
        Assert.Contains("AddEndpointFilter<ApiAntiforgeryFilter>()", endpoints);
        Assert.Contains("AuthorizeAsync(user, \"ViewFinance\")", endpoints);
        Assert.Contains("canViewFinance && ContainsFinanceIntent", service);
        Assert.Contains("Trạng thái phòng hiện tại", service);
        Assert.Contains("OccupyingStatuses.Contains(x.Status)", service);
        Assert.Contains("RoomConditionReportStatus.Resolved", service);
        Assert.Contains("AiAudience.Staff", service);
        Assert.Contains("AiToolExecutionLogs.Add", service);
        Assert.DoesNotContain("db.Customers", service);
        Assert.DoesNotContain("SaveChangesAsync", service.Replace("await db.SaveChangesAsync(ct);", string.Empty));
        Assert.Contains("data-staff-ai-drawer", layout);
    }

    [Fact]
    public void Owner_report_uses_typed_report_service_payment_facts_and_period_metadata()
    {
        var service = Read("src/DeLong.Web/Features/AdminAi/AiBusinessReportService.cs");
        var endpoints = Read("src/DeLong.Web/Features/AdminAi/AdminAiEndpoints.cs");
        var chat = Read("src/DeLong.Web/wwwroot/js/core/admin-ai-chat.js");

        Assert.Contains("ReportService reports", service);
        Assert.Contains("current.NetReceipts", service);
        Assert.Contains("current.Refunds", service);
        Assert.Contains("current.Expenses", service);
        Assert.Contains("current.OccupancyRate", service);
        Assert.Contains("AverageLeadDays", service);
        Assert.Contains("NewCustomers", service);
        Assert.Contains("db.BookingRateSegments.AsNoTracking()", service);
        Assert.Contains("ByBookingType", service);
        Assert.Contains("ByRate", service);
        Assert.Contains("AiBusinessInsightBuilder.Build", service);
        Assert.Contains("Không áp dụng ngay", service);
        Assert.Contains("sample < 10", service);
        Assert.Contains("TimeZone", service);
        Assert.Contains("business_report", service);
        Assert.Contains("AiResponseCacheService cache", service);
        Assert.Contains("TimeSpan.FromMinutes(5)", service);
        Assert.Contains("\"reports\"", service);
        Assert.Contains("ReportUrl", service);
        Assert.Contains("/business-report", endpoints);
        Assert.Contains("dynamicPeriod", chat);
        Assert.Contains("tóm tắt|tình hình|booking|đặt phòng", chat);
        Assert.Contains("textContent", chat);
    }

    [Fact]
    public void Admin_ai_drawer_offers_token_free_report_shortcuts_for_common_periods()
    {
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");

        Assert.Contains("aria-label=\"Lệnh báo cáo nhanh\"", layout);
        Assert.Contains("công suất tuần này", layout);
        Assert.Contains("công suất tháng này", layout);
        Assert.Contains("công suất quý này", layout);
        Assert.Contains("công suất năm nay", layout);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src/DeLong.Web"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy workspace root.");
    }
}
