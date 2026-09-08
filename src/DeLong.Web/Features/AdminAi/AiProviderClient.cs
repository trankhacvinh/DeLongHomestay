using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.AdminAi;

public sealed class AiProviderClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AiProviderResult> GenerateAsync(AiProviderKind provider, string apiKey, string model, string system, string input, int maxTokens, CancellationToken ct)
    {
        return provider switch
        {
            AiProviderKind.OpenAi => await OpenAiAsync(apiKey, model, system, input, maxTokens, ct),
            AiProviderKind.Gemini => await GeminiAsync(apiKey, model, system, input, maxTokens, ct),
            _ => throw new InvalidOperationException("Nhà cung cấp AI không được hỗ trợ.")
        };
    }

    private async Task<AiProviderResult> OpenAiAsync(string key, string model, string system, string input, int maxTokens, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new
        {
            model,
            instructions = system,
            input,
            max_output_tokens = maxTokens,
            text = new { format = new { type = "json_schema", name = "admin_ai_response", strict = true, schema = AiResponseProtocol.Schema } }
        });
        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new AiProviderException("openai_error", ReadError(body, response.ReasonPhrase));
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var text = root.TryGetProperty("output_text", out var outputText) ? outputText.GetString() : null;
        if (string.IsNullOrWhiteSpace(text) && root.TryGetProperty("output", out var output))
            text = output.EnumerateArray().SelectMany(x => x.TryGetProperty("content", out var c) ? c.EnumerateArray() : [])
                .Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "output_text")
                .Select(x => x.TryGetProperty("text", out var t) ? t.GetString() : null).Aggregate(new StringBuilder(), (b, t) => b.Append(t)).ToString();
        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        return new(text ?? string.Empty,
            ReadInt(usage, "input_tokens"), ReadInt(usage, "output_tokens"), root.TryGetProperty("id", out var id) ? id.GetString() : null,
            root.TryGetProperty("status", out var status) ? status.GetString() : null);
    }

    private async Task<AiProviderResult> GeminiAsync(string key, string model, string system, string input, int maxTokens, CancellationToken ct)
    {
        var safeModel = Uri.EscapeDataString(model.Trim());
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent");
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(new
        {
            system_instruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = input } } } },
            generationConfig = new { maxOutputTokens = maxTokens, responseMimeType = "application/json", responseJsonSchema = AiResponseProtocol.Schema }
        });
        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new AiProviderException("gemini_error", ReadError(body, response.ReasonPhrase));
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var candidate = root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 ? candidates[0] : default;
        var text = new StringBuilder();
        if (candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
            foreach (var part in parts.EnumerateArray())
                if ((!part.TryGetProperty("thought", out var thought) || !thought.GetBoolean()) && part.TryGetProperty("text", out var partText))
                    text.Append(partText.GetString());
        var usage = root.TryGetProperty("usageMetadata", out var u) ? u : default;
        return new(text.ToString(),
            ReadInt(usage, "promptTokenCount"), ReadInt(usage, "candidatesTokenCount") + ReadInt(usage, "thoughtsTokenCount"), root.TryGetProperty("responseId", out var id) ? id.GetString() : null,
            candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("finishReason", out var finish) ? finish.GetString() : "empty_response");
    }

    private static int ReadInt(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value.GetInt32() : 0;
    private static string ReadError(string body, string? fallback)
    {
        // Provider errors can contain request data. Do not return them to the chat.
        return "Không gọi được nhà cung cấp AI. Vui lòng kiểm tra model, hạn mức và cấu hình kết nối.";
    }
}

public sealed class AiProviderException(string code, string message) : Exception(message) { public string Code { get; } = code; }
