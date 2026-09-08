using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiEnvelope(string Message, AiOperationEnvelope? Proposal);
public sealed record AiOperationEnvelope(AiProposalType Type, string Summary, JsonElement Payload);

public static class AiResponseProtocol
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // A bounded envelope shared by both providers. Business payloads are independently
    // validated and resolved against the property before a proposal can be stored.
    public static JsonElement Schema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            message = new { type = "string" },
            proposal = new
            {
                anyOf = new object[]
                {
                    new { type = "null" },
                    new
                    {
                        type = "object", additionalProperties = false,
                        properties = new
                        {
                            type = new { type = "string", @enum = Enum.GetNames<AiProposalType>() },
                            summary = new { type = "string" },
                            payloadJson = new { type = "string" }
                        },
                        required = new[] { "type", "summary", "payloadJson" }
                    }
                }
            }
        },
        required = new[] { "message", "proposal" }
    });

    public static AiEnvelope? Parse(string text)
    {
        try
        {
            var clean = text.Trim();
            if (clean.StartsWith("```", StringComparison.Ordinal))
            {
                var newline = clean.IndexOf('\n');
                var end = clean.LastIndexOf("```", StringComparison.Ordinal);
                if (newline >= 0 && end > newline) clean = clean[(newline + 1)..end].Trim();
            }
            var root = JsonNode.Parse(clean) as JsonObject;
            if (root is null) return null;
            if (root["proposal"] is JsonObject proposal && proposal["payloadJson"] is JsonValue payload)
            {
                proposal["payload"] = JsonNode.Parse(payload.GetValue<string>());
                proposal.Remove("payloadJson");
            }
            var result = root.Deserialize<AiEnvelope>(Json);
            if (string.IsNullOrWhiteSpace(result?.Message)) return null;
            if (result.Proposal is { } p &&
                (!Enum.IsDefined(p.Type) || p.Payload.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(p.Summary)))
                return null;
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException) { return null; }
    }

    public static bool IsRetry(string text) =>
        new[] { "retry", "thử lại", "thu lai", "làm lại", "lam lai" }.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase);
}
