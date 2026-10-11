using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.PublicAi;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PublicAiCustomerSupportTests
{
    [PricingPostgreSqlFact]
    public async Task Published_knowledge_is_scoped_and_content_changes_bypass_cached_answers()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var suffix = Guid.NewGuid().ToString("N");
        var faq = Section(null, "Faq", $"FAQ-{suffix}", new { items = new[] { new { question = "Pets?", answer = $"FAQ-ANSWER-{suffix}" } } });
        var local = Section(fixture.Property.Id, "PolicyGrid", "Rules", new { items = new[] { new { title = "Rule", body = $"LOCAL-{suffix}" } } });
        var hidden = Section(fixture.Property.Id, "RichText", "Hidden", new { html = $"HIDDEN-{suffix}" });
        hidden.IsVisible = false;
        var other = Section(fixture.Other.Id, "Faq", "Other", new { items = new[] { $"OTHER-{suffix}" } });
        var published = Page(null, "Published", $"published-{suffix}", true, true, $"PAGE-{suffix}");
        var draft = Page(null, "Draft", $"draft-{suffix}", false, true, $"DRAFT-{suffix}");
        var hiddenBlock = Page(fixture.Property.Id, "Hidden block", $"hidden-{suffix}", true, false, $"BLOCK-{suffix}");
        var otherPage = Page(fixture.Other.Id, "Other", $"other-{suffix}", true, true, $"OTHER-PAGE-{suffix}");
        db.AddRange(faq, local, hidden, other, published, draft, hiddenBlock, otherPage);
        await db.SaveChangesAsync();
        await fixture.WritePolicyAsync($"HOUSE-RULES-{suffix}");

        var content = await fixture.Content.GetAsync(fixture.Context, default);
        var text = string.Join('\n', content.Documents.Select(x => x.Text));
        Assert.Contains($"FAQ-ANSWER-{suffix}", text);
        Assert.Contains($"LOCAL-{suffix}", text);
        Assert.Contains($"PAGE-{suffix}", text);
        Assert.Contains($"HOUSE-RULES-{suffix}", text);
        Assert.DoesNotContain($"HIDDEN-{suffix}", text);
        Assert.DoesNotContain($"DRAFT-{suffix}", text);
        Assert.DoesNotContain($"BLOCK-{suffix}", text);
        Assert.DoesNotContain($"OTHER-{suffix}", text);
        Assert.DoesNotContain($"OTHER-PAGE-{suffix}", text);
        Assert.Contains(content.Documents, x => x.Url == $"/published-{suffix}");

        var question = new PublicAiChatRequest($"Chính sách hủy phòng {suffix}?");
        var (first, firstError) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, question, "client", default);
        Assert.Null(firstError);
        Assert.NotNull(first);
        Assert.False(first!.Cached);
        Assert.Contains($"FAQ-ANSWER-{suffix}", Assert.Single(fixture.Handler.Inputs));
        var (cached, cachedError) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, question, "client", default);
        Assert.Null(cachedError);
        Assert.True(cached!.Cached);
        Assert.Single(fixture.Handler.Inputs);

        faq.ContentJson = JsonSerializer.Serialize(new { items = new[] { new { question = "Pets?", answer = $"NEW-FAQ-{suffix}" } } });
        await db.SaveChangesAsync();
        await fixture.WritePolicyAsync($"NEW-RULES-{suffix}");
        var changed = await fixture.Content.GetAsync(fixture.Context, default);
        Assert.NotEqual(content.Version, changed.Version);
        var (updated, updateError) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, question, "client", default);
        Assert.Null(updateError);
        Assert.False(updated!.Cached);
        Assert.Equal(2, fixture.Handler.Inputs.Count);
        Assert.Contains($"NEW-FAQ-{suffix}", fixture.Handler.Inputs[1]);
        Assert.Contains($"NEW-RULES-{suffix}", fixture.Handler.Inputs[1]);

        published.ContentJson = JsonSerializer.Serialize(new { title = "Published", slug = $"published-{suffix}", isPublished = false });
        await db.SaveChangesAsync();
        Assert.DoesNotContain((await fixture.Content.GetAsync(fixture.Context, default)).Documents, x => x.Text.Contains($"PAGE-{suffix}"));
    }

    [PricingPostgreSqlFact]
    public async Task Email_lookup_is_private_scoped_deduplicated_and_rate_limited_without_provider_call()
    {
        await using var fixture = await Fixture.CreateAsync();
        var email = $"guest-{Guid.NewGuid():N}@example.test";
        foreach (var property in new[] { fixture.Property, fixture.Other })
        {
            var room = new Room { Property = property, Code = "ROOM", Name = "PRIVATE-ROOM" };
            var customer = new Customer { Property = property, Name = "PRIVATE-NAME", Phone = "0901234567", NormalizedPhone = "0901234567", Email = email };
            fixture.Db.Add(new Booking
            {
                Property = property, Room = room, Customer = customer, Code = $"BK-{Guid.NewGuid():N}"[..20],
                Status = BookingStatus.Confirmed, CheckInUtc = DateTime.UtcNow.AddDays(1), CheckOutUtc = DateTime.UtcNow.AddDays(1).AddHours(3),
                RoomAmount = 250000
            });
        }
        var token = new string('a', 40);
        fixture.Db.AiBookingDrafts.Add(new AiBookingDraft
        {
            PropertyId = fixture.Property.Id,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),
            StateJson = JsonSerializer.Serialize(new PublicAiDraftExtraction(true, "ROOM", "2026-10-20", [], 2, "")),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        });
        await fixture.Db.SaveChangesAsync();
        var (known, error) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new($"Tra cứu bằng email {email}", token), "client", default);
        Assert.Null(error);
        Assert.Equal(PublicBookingEmailLookupService.ResponseMessage, known!.Message);
        Assert.Null(known.Draft);
        Assert.DoesNotContain("PRIVATE-", known.Message);
        var (unknown, unknownError) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new("Tra cứu bằng email missing@example.test"), "client", default);
        Assert.Null(unknownError);
        Assert.Equal(known.Message, unknown!.Message);
        await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new(email), "client", default);
        var queued = await fixture.Db.BookingGuestGuideEmails.AsNoTracking().Where(x => x.PropertyId == fixture.Property.Id).ToListAsync();
        var mail = Assert.Single(queued);
        Assert.Equal(email, mail.RecipientEmail);
        Assert.Contains("PRIVATE-ROOM", mail.BodyText);
        Assert.False(await fixture.Db.BookingGuestGuideEmails.AnyAsync(x => x.PropertyId == fixture.Other.Id));
        Assert.Empty(fixture.Handler.Inputs);
        var (clarify, clarifyError) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new("Tra cứu bằng email"), "client", default);
        Assert.Null(clarifyError);
        Assert.Contains("email đã dùng", clarify!.Message);
        await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new(email), "client", default);
        await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new(email), "client", default);
        var (blocked, blockedError) = await fixture.Service.ChatAsync(fixture.Property.SiteSlug, new(email), "client", default);
        Assert.Null(blocked);
        Assert.Contains("quá nhiều", blockedError!);
    }

    private static HomeSection Section(Guid? propertyId, string type, string name, object content) =>
        new() { PropertyId = propertyId, Type = type, Name = name, ContentJson = JsonSerializer.Serialize(content) };

    private static HomeSection Page(Guid? propertyId, string title, string slug, bool published, bool visible, string text) =>
        Section(propertyId, CustomPageStore.MetadataSectionType, title, new
        {
            title, slug, isPublished = published,
            sections = new[] { new { id = Guid.NewGuid(), type = "RichText", name = title, isVisible = visible,
                contentJson = JsonSerializer.Serialize(new { html = $"<p>{text}</p>" }) } }
        });

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Inputs { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var input = document.RootElement.GetProperty("input");
            Inputs.Add(input.ValueKind == JsonValueKind.String ? input.GetString()! : string.Join('\n', input.EnumerateArray()
                .SelectMany(x => x.GetProperty("content").EnumerateArray()).Select(x => x.GetProperty("text").GetString())));
            var text = JsonSerializer.Serialize(new { message = "Vui lòng xem chính sách công khai của cơ sở.", suggestedRoomCode = "", draft = (object?)null });
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            {
                status = "completed", output = new[] { new { content = new[] { new { type = "output_text", text } } } },
                usage = new { input_tokens = 10, output_tokens = 5 }
            })) };
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required AppDbContext Db { get; init; }
        public required Property Property { get; init; }
        public required Property Other { get; init; }
        public required PublicAiService Service { get; init; }
        public required PublicAiContentService Content { get; init; }
        public required Handler Handler { get; init; }
        public required HttpClient Http { get; init; }
        public required string Root { get; init; }
        public PublicPropertyContext Context => new(Property.Id, Property.Code, Property.Name, Property.TimeZoneId, Property.SiteSlug!);
        public Task WritePolicyAsync(string text)
        {
            var directory = Path.Combine(Root, "booking-settings");
            Directory.CreateDirectory(directory);
            return File.WriteAllTextAsync(Path.Combine(directory, Property.Id.ToString("N") + ".json"),
                JsonSerializer.Serialize(new { policyTitle = "House rules", policyText = $"<p>{text}</p>", policyVersion = 1 }));
        }
        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")).Options);
            await db.Database.MigrateAsync();
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var property = new Property { Code = $"PUBAI-{suffix}", SiteSlug = $"pubai-{suffix}", Name = "Public AI", TimeZoneId = "Asia/Ho_Chi_Minh" };
            var other = new Property { Code = $"OTHERAI-{suffix}", SiteSlug = $"otherai-{suffix}", Name = "Other AI" };
            var protector = new AiCredentialProtector(new EphemeralDataProtectionProvider());
            db.AddRange(property, other);
            db.PropertyAiProfiles.Add(new PropertyAiProfile { Property = property, IsEnabled = true, IsPublicAiEnabled = true,
                Provider = AiProviderKind.OpenAi, Model = "test-model", ProtectedApiKey = protector.Protect("test-only") });
            await db.SaveChangesAsync();
            var root = Path.Combine(Path.GetTempPath(), $"delong-public-ai-{Guid.NewGuid():N}");
            var paths = new StoragePaths(root, root, new PathString("/test"), false, false, false);
            var resolver = new PublicPropertyResolver(db);
            var content = new PublicAiContentService(new SiteContentService(db, resolver), new CustomPageStore(db), paths, new ConfigurationBuilder().Build());
            var audit = new AuditService(db);
            var pricing = new PricingService(db, audit);
            var handler = new Handler();
            var http = new HttpClient(handler);
            var service = new PublicAiService(db, resolver, new AiAccessGateway(db), new AiKnowledgeSnapshotService(db),
                new AiResponseCacheService(db), new AdminAiSettingsService(db, protector), new AiProviderClient(http),
                new PublicBookingService(db, new BookingService(db, new CustomerService(db), audit), resolver, pricing),
                new PublicBookingLookupService(db, resolver), new PublicAiLookupRateGuard(), pricing,
                new PublicBookingEmailLookupService(db, resolver), content);
            return new() { Db = db, Property = property, Other = other, Service = service, Content = content,
                Handler = handler, Http = http, Root = root };
        }
        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await Db.DisposeAsync();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
