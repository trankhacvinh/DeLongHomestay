using System.Net;
using System.Text.Json;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class AiProtocolTests
{
    [Fact]
    public void Cache_key_is_stable_for_equivalent_intent_and_parameter_order_but_scoped_by_property()
    {
        var propertyId = Guid.NewGuid();
        var first = AiResponseCacheService.CreateKey(propertyId, AiAudience.Customer, "  ROOM   Availability ", new { roomId = 2, date = "2026-09-10" }, 7);
        var equivalent = AiResponseCacheService.CreateKey(propertyId, AiAudience.Customer, "room availability", new Dictionary<string, object>
        {
            ["date"] = "2026-09-10",
            ["roomId"] = 2
        }, 7);
        var otherProperty = AiResponseCacheService.CreateKey(Guid.NewGuid(), AiAudience.Customer, "room availability", new { roomId = 2, date = "2026-09-10" }, 7);

        Assert.Equal(first, equivalent);
        Assert.NotEqual(first, otherProperty);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Tool_registry_never_exposes_internal_or_mutating_tools_to_customer()
    {
        var registry = new AiToolRegistry();
        var customer = registry.For(AiAudience.Customer, _ => true);
        var staffWithoutFinance = registry.For(AiAudience.Staff, permission => permission == "UseStaffAi");
        var admin = registry.For(AiAudience.Admin, _ => true);

        Assert.DoesNotContain(customer, x => x.IsMutation || x.Name is "payment_summary" or "operations_summary" or "business_report");
        Assert.DoesNotContain(staffWithoutFinance, x => x.Name == "payment_summary");
        Assert.Contains(admin, x => x.Name == "configuration_proposal" && x.IsMutation);
    }

    [Fact]
    public void Schema_envelope_decodes_payload_without_allowing_unknown_action()
    {
        var envelope = JsonSerializer.Serialize(new { message = "Đổi giá cuối tuần", proposal = new
        {
            type = "ConfigureRoomRates", summary = "Các khung giá đang hoạt động", payloadJson = """{"allRooms":true,"allRates":true,"changes":{"weekendPrice":300000}}"""
        } });
        var result = AiResponseProtocol.Parse(envelope);
        Assert.Equal(AiProposalType.ConfigureRoomRates, result!.Proposal!.Type);
        Assert.Equal(300000, result.Proposal.Payload.GetProperty("changes").GetProperty("weekendPrice").GetInt32());
        Assert.Null(AiResponseProtocol.Parse(envelope.Replace("ConfigureRoomRates", "DeletePayments")));
    }

    [Theory]
    [InlineData("""{"message":"chưa xong","proposal":{""")]
    [InlineData("""{"message":null,"proposal":null}""")]
    [InlineData("Đã áp dụng thay đổi")]
    [InlineData("""{"message":"X","proposal":{"type":999,"summary":"X","payload":{}}}""")]
    public void Malformed_or_unstructured_output_cannot_become_a_proposal(string text) => Assert.Null(AiResponseProtocol.Parse(text));

    [Fact]
    public async Task Gemini_combines_all_non_thought_parts_and_reports_truncation_and_usage()
    {
        using var handler = new FakeHandler(JsonSerializer.Serialize(new
        {
            candidates = new[] { new { finishReason = "MAX_TOKENS", content = new { parts = new object[]
            {
                new { text = "private thought", thought = true }, new { text = "{\"message\":" }, new { text = "\"Xin chào\"}" }
            } } } },
            usageMetadata = new { promptTokenCount = 12, candidatesTokenCount = 5, thoughtsTokenCount = 8, cachedContentTokenCount = 7 }
        }));
        using var http = new HttpClient(handler);
        var result = await new AiProviderClient(http).GenerateAsync(AiProviderKind.Gemini, "test-only", "test-model", "system", "input", 2000, default);
        Assert.Equal("{\"message\":\"Xin chào\"}", result.Text);
        Assert.Equal(13, result.OutputTokens);
        Assert.Equal(7, result.CachedInputTokens);
        Assert.Equal("MAX_TOKENS", result.FinishReason);
        Assert.Contains("responseJsonSchema", handler.RequestBody);
    }

    [Fact]
    public async Task OpenAi_combines_output_parts_and_sends_strict_schema()
    {
        using var handler = new FakeHandler(JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[] { new { content = new[] { new { type = "output_text", text = "{\"message\":" }, new { type = "output_text", text = "\"Hi\",\"proposal\":null}" } } } },
            usage = new { input_tokens = 10, output_tokens = 9, input_tokens_details = new { cached_tokens = 6 } }
        }));
        using var http = new HttpClient(handler);
        var result = await new AiProviderClient(http).GenerateAsync(AiProviderKind.OpenAi, "test-only", "test-model", "system", "input", 2000, default);
        Assert.Equal("Hi", AiResponseProtocol.Parse(result.Text)!.Message);
        Assert.Equal(6, result.CachedInputTokens);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.True(request.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task OpenAi_sends_images_as_multimodal_content_and_extracted_word_as_untrusted_text()
    {
        using var handler = new FakeHandler(JsonSerializer.Serialize(new
        {
            status = "completed", output_text = "{\"message\":\"OK\",\"proposal\":null}",
            usage = new { input_tokens = 20, output_tokens = 4 }
        }));
        using var http = new HttpClient(handler);
        var attachments = new AiProviderAttachment[]
        {
            new("room.png", "image/png", [0x89, 0x50], null),
            new("guide.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [], "Hướng dẫn nhận phòng")
        };
        await new AiProviderClient(http).GenerateAsync(AiProviderKind.OpenAi, "test-only", "test-model", "system", "Cập nhật nội dung", 2000, attachments, default);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var content = request.RootElement.GetProperty("input")[0].GetProperty("content");
        Assert.Contains("Hướng dẫn nhận phòng", content[0].GetProperty("text").GetString());
        Assert.Equal("input_image", content[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task DeepSeek_uses_chat_completions_json_mode_and_reads_usage()
    {
        using var handler = new FakeHandler(JsonSerializer.Serialize(new
        {
            id = "ds-response-1",
            choices = new[] { new { finish_reason = "stop", message = new { content = "{\"message\":\"OK\",\"proposal\":null}" } } },
            usage = new { prompt_tokens = 14, completion_tokens = 6, prompt_cache_hit_tokens = 8 }
        }));
        using var http = new HttpClient(handler);

        var result = await new AiProviderClient(http).GenerateAsync(AiProviderKind.DeepSeek, "test-only", "deepseek-v4-flash", "system", "input", 2000, default);

        Assert.Equal("OK", AiResponseProtocol.Parse(result.Text)!.Message);
        Assert.Equal(14, result.InputTokens);
        Assert.Equal(6, result.OutputTokens);
        Assert.Equal(8, result.CachedInputTokens);
        Assert.Equal("https://api.deepseek.com/chat/completions", handler.RequestUri);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("json_object", request.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Contains("JSON Schema", request.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    internal sealed class FakeHandler(string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? RequestUri { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri?.ToString();
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }
}
