using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeLong.Web.Features.PublicAi;

public sealed record PublicAiProviderEnvelope(string Message, PublicAiDraftExtraction? Draft, string SuggestedRoomCode = "");
public sealed record PublicAiDraftExtraction(bool Requested, string RoomCode, string StayDate,
    IReadOnlyList<string> RateNames, int GuestCount, string VoucherCode);

public static class PublicAiResponseProtocol
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static JsonElement Schema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            message = new { type = "string" },
            suggestedRoomCode = new { type = "string" },
            draft = new
            {
                anyOf = new object[]
                {
                    new { type = "null" },
                    new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            requested = new { type = "boolean" },
                            roomCode = new { type = "string" },
                            stayDate = new { type = "string" },
                            rateNames = new { type = "array", items = new { type = "string" } },
                            guestCount = new { type = "integer", minimum = 0, maximum = 100 },
                            voucherCode = new { type = "string" }
                        },
                        required = new[] { "requested", "roomCode", "stayDate", "rateNames", "guestCount", "voucherCode" }
                    }
                }
            }
        },
        required = new[] { "message", "suggestedRoomCode", "draft" }
    });

    public static PublicAiProviderEnvelope? Parse(string text)
    {
        try
        {
            var result = JsonSerializer.Deserialize<PublicAiProviderEnvelope>(text, Json);
            return string.IsNullOrWhiteSpace(result?.Message) ? null : result;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
