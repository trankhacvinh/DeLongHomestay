using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeLong.Web.Common.Operations;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;

namespace DeLong.Web.Features.PublicAi;

public sealed record PublicAiContentDocument(string Title, string? Url, string Text);
public sealed record PublicAiContentKnowledge(IReadOnlyList<PublicAiContentDocument> Documents, string Version);

public sealed class PublicAiContentService(
    SiteContentService siteContent,
    CustomPageStore pages,
    StoragePaths paths,
    IConfiguration configuration)
{
    private static readonly HashSet<string> TextFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "subtitle", "heading", "body", "text", "html", "description", "question", "answer", "label"
    };

    public async Task<PublicAiContentKnowledge> GetAsync(PublicPropertyContext property, CancellationToken ct)
    {
        var documents = new List<PublicAiContentDocument>();
        var global = await siteContent.GetGlobalPublicSectionsAsync(ct);
        var local = await siteContent.GetPublicAsync(property.SiteSlug, ct);
        AddSections(documents, global, "Trang chủ", null);
        if (local is not null) AddSections(documents, local.Sections, property.Name, null);
        foreach (var page in await pages.GetPublicKnowledgePagesAsync(property.Id, ct))
            AddSections(documents, page.Sections, page.Title, page.Url);

        var policy = await new BookingPolicyStore(paths, configuration).GetAsync(property.Id, ct);
        documents.Add(new(policy.PolicyTitle, null, NotificationEmailTemplateRenderer.ToPlainText(policy.PolicyText)));
        var json = JsonSerializer.Serialize(documents);
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return new(documents, version);
    }

    private static void AddSections(List<PublicAiContentDocument> documents, IReadOnlyList<HomeSectionDto> sections,
        string title, string? url)
    {
        foreach (var section in sections.Where(x => x.IsVisible && x.Type is "Faq" or "PolicyGrid" or "RichText"))
        {
            try
            {
                using var content = JsonDocument.Parse(section.ContentJson);
                var parts = new List<string>();
                ReadText(content.RootElement, parts);
                var text = string.Join('\n', parts);
                if (!string.IsNullOrWhiteSpace(text)) documents.Add(new($"{title} · {section.Name}", url, text));
            }
            catch (JsonException)
            {
                // Invalid CMS content is omitted just as it cannot be rendered as a content block.
            }
        }
    }

    private static void ReadText(JsonElement value, List<string> parts)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in value.EnumerateObject())
            {
                if (TextFields.Contains(field.Name) && field.Value.ValueKind == JsonValueKind.String)
                {
                    var text = NotificationEmailTemplateRenderer.ToPlainText(field.Value.GetString());
                    if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                }
                else if (field.Name == "items") ReadText(field.Value, parts);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    parts.Add(NotificationEmailTemplateRenderer.ToPlainText(item.GetString()));
                else ReadText(item, parts);
        }
    }
}
