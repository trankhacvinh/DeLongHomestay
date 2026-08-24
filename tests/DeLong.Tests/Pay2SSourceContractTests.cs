using Xunit;

namespace DeLong.Tests;

public sealed class Pay2SSourceContractTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Settings_are_property_scoped_and_public_booking_redirects_to_payment()
    {
        var settings = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Pages/Admin/Settings/Index.cshtml"));
        var booking = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/wwwroot/js/pages/public-booking.js"));
        Assert.Contains("Hồ sơ Pay2S của cơ sở", settings);
        Assert.Contains("/api/admin/properties/${this.propertyId}/pay2s/settings",
            File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/wwwroot/js/pages/admin-settings.js")));
        Assert.Contains("result.paymentUrl", booking);
        Assert.Contains("window.location.assign(result.paymentUrl)", booking);
    }

    [Fact]
    public void Expired_or_cancelled_payment_cannot_resume_from_the_client_only()
    {
        var program = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Program.cs"));
        var endpoints = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SEndpoints.cs"));
        var pageScript = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/wwwroot/js/pages/pay2s-return.js"));

        Assert.Contains("payment_session_closed", endpoints);
        Assert.Contains("Pay2SPaymentIntentStatus.Pending", endpoints);
        Assert.Contains("BookingStatus.Cancelled", endpoints);
        Assert.DoesNotContain("result.PayUrl, result.ExpiresAtUtc", endpoints);
        Assert.Contains("/${encodeURIComponent(orderId)}/resume", pageScript);
        Assert.Contains("resume.hidden = !result.canResume", pageScript);
        Assert.Contains("AddPolicy(\"pay2s-status\"", program);
        Assert.Contains("AddPolicy(\"pay2s-ipn\"", program);
        Assert.Contains("RequireRateLimiting(\"pay2s-status\")", endpoints);
        Assert.Contains("RequireRateLimiting(\"pay2s-ipn\")", endpoints);
        Assert.Contains("error?.status === 429", pageScript);
        Assert.Contains("pollDelayMs", pageScript);
        Assert.Contains("confirm-return", endpoints);
        Assert.Contains("confirmSignedReturn", pageScript);
        Assert.Contains("m2signature", pageScript);
        Assert.Contains("MapGet(\"/api/public/payments/pay2s/{orderId}/confirm-return\"", endpoints);
    }

    [Fact]
    public void Public_payment_pages_do_not_load_admin_visual_editor_assets()
    {
        var layout = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Pages/Shared/_Layout.cshtml"));
        var api = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/wwwroot/js/core/api.js"));

        Assert.Contains("public-editor-enabled", layout);
        Assert.Contains("classList.contains('public-editor-enabled')", api);
        Assert.Contains("objectPayload?.message", api);
    }

    [Fact]
    public void Payment_expiry_and_room_release_are_distinct_server_side_times()
    {
        var service = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SService.cs"));
        var endpoints = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SEndpoints.cs"));
        Assert.Contains("CalculateReleaseAtUtc", service);
        Assert.Contains("release_at_utc <=", service);
        Assert.Contains("InSettlementGrace", endpoints);
        Assert.Contains("LastCallbackErrorCode", endpoints);
    }

    [Fact]
    public void Staff_qr_has_no_grace_and_booking_mutations_close_old_intents()
    {
        var endpoints = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SEndpoints.cs"));
        var publicEndpoints = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/PublicBooking/PublicBookingEndpoints.cs"));
        var bookings = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Bookings/BookingService.cs"));
        var payments = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/PaymentService.cs"));
        var pay2s = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SService.cs"));

        Assert.Contains("useSettlementGrace: false", endpoints);
        Assert.Contains("useSettlementGrace: true", publicEndpoints);
        Assert.Contains("Pay2SIntentLifecycleManager.CloseOpenIntentsAsync", bookings);
        Assert.Contains("Pay2SIntentLifecycleManager.CloseOpenIntentsAsync", payments);
        Assert.Contains("FOR UPDATE", pay2s);
        Assert.Contains("transaction_conflict", pay2s);
        Assert.Contains("string.IsNullOrWhiteSpace(request.TransId)", pay2s);
        Assert.Contains("request.TransId > 0 ? request.TransId : null", pay2s);
    }

    [Fact]
    public void Collection_link_uses_the_current_shared_endpoint_and_v2_signature_contract()
    {
        var client = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SClient.cs"));
        var service = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SService.cs"));
        var settings = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SSettingsService.cs"));

        Assert.Contains("https://payment.pay2s.vn/v1/gateway/api/create", client);
        Assert.Contains("const string requestType = \"pay2s\"", client);
        Assert.Contains("signatureVersion=", client);
        Assert.Contains("&extraData=", client);
        Assert.Contains("created.OrderInfo", service);
        Assert.Contains("sandbox-payment.pay2s.vn", settings);
    }

    [Fact]
    public void Provider_request_id_is_persisted_and_legacy_intents_are_resolved_by_order_id()
    {
        var service = File.ReadAllText(Path.Combine(Root, "src/DeLong.Web/Features/Payments/Pay2SService.cs"));

        Assert.Contains("string.IsNullOrWhiteSpace(created.RequestId) ? requestId : created.RequestId.Trim()", service);
        Assert.Contains("WHERE order_id = {request.OrderId}", service);
        Assert.DoesNotContain("WHERE order_id = {request.OrderId} AND request_id = {request.RequestId}", service);
        Assert.Contains("intent.RequestId.StartsWith(\"RQ\", StringComparison.Ordinal)", service);
        Assert.Contains("intent.RequestId = request.RequestId.Trim()", service);
        Assert.Contains("verifySignature(profile.AccessKey, profile.SecretKey)", service);
    }

    private static string FindRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(current, "src", "DeLong.Web")))
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        return current;
    }
}
