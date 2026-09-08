using System.Net;
using System.Text.Json;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class AiProtocolTests
{
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
            usageMetadata = new { promptTokenCount = 12, candidatesTokenCount = 5, thoughtsTokenCount = 8 }
        }));
        using var http = new HttpClient(handler);
        var result = await new AiProviderClient(http).GenerateAsync(AiProviderKind.Gemini, "test-only", "test-model", "system", "input", 2000, default);
        Assert.Equal("{\"message\":\"Xin chào\"}", result.Text);
        Assert.Equal(13, result.OutputTokens);
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
            usage = new { input_tokens = 10, output_tokens = 9 }
        }));
        using var http = new HttpClient(handler);
        var result = await new AiProviderClient(http).GenerateAsync(AiProviderKind.OpenAi, "test-only", "test-model", "system", "input", 2000, default);
        Assert.Equal("Hi", AiResponseProtocol.Parse(result.Text)!.Message);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.True(request.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
    }

    internal sealed class FakeHandler(string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }
}
