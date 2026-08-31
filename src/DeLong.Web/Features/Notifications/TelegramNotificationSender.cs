using System.Net.Http.Json;
using System.Text.Json;

namespace DeLong.Web.Features.Notifications;

public sealed class TelegramNotificationSender : IDisposable
{
    private readonly HttpClient client = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    }) { Timeout = TimeSpan.FromSeconds(15) };

    public async Task SendAsync(
        string botToken,
        IEnumerable<string> chatIds,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken)) throw new InvalidOperationException("Telegram bot token is missing.");
        var text = messageText.Trim();
        if (text.Length is < 1 or > 4096) throw new InvalidOperationException("Telegram message must contain 1–4096 characters.");

        foreach (var chatId in chatIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var response = await client.PostAsJsonAsync(
                $"https://api.telegram.org/bot{botToken}/sendMessage",
                new { chat_id = chatId, text, disable_web_page_preview = true },
                cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode && IsSuccessful(payload)) continue;
            throw new InvalidOperationException(ReadError(payload) ?? $"Telegram trả về HTTP {(int)response.StatusCode}.");
        }
    }

    private static bool IsSuccessful(string payload)
    {
        try { return JsonDocument.Parse(payload).RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean(); }
        catch (JsonException) { return false; }
    }

    private static string? ReadError(string payload)
    {
        try
        {
            var root = JsonDocument.Parse(payload).RootElement;
            return root.TryGetProperty("description", out var description) ? description.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    public void Dispose() => client.Dispose();
}
