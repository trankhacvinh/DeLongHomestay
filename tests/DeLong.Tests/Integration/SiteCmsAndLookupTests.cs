using System.Security.Claims;
using System.Text.Json.Nodes;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Properties;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class SiteCmsAndLookupTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Property_creation_grants_creator_access_and_site_content_is_sanitized()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminId = Guid.CreateVersion7();
        db.Users.Add(new ApplicationUser
        {
            Id = adminId,
            UserName = $"cms-{suffix}@example.test",
            NormalizedUserName = $"CMS-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            Email = $"cms-{suffix}@example.test",
            NormalizedEmail = $"CMS-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            DisplayName = "CMS Integration Admin",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, adminId.ToString())], "integration");
        var principal = new ClaimsPrincipal(identity);
        var propertyService = new PropertyAdminService(db);
        var (created, createError) = await propertyService.CreateAsync(new SavePropertyRequest
        {
            Code = $"CMS-{suffix}",
            Name = "CMS Test Property",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = true
        }, principal);

        Assert.Null(createError);
        Assert.NotNull(created);
        Assert.True(await db.UserPropertyAccesses.AnyAsync(x => x.UserId == adminId && x.PropertyId == created!.Id));

        var siteService = new SiteContentService(db);
        var site = await siteService.GetAdminAsync(created!.Id);
        Assert.NotNull(site);
        Assert.True(site!.Sections.Count >= 4);

        var (adminSettings, adminError) = await siteService.SaveSettingsAsync(created.Id, new SaveSiteSettingsRequest
        {
            SiteName = "CMS Public Name",
            MetaTitle = "CMS SEO Title",
            MetaDescription = "CMS SEO Description",
            CanonicalBaseUrl = "https://example.test",
            CustomJs = "window.cmsAdmin = true;",
            CustomCss = ".cms-test{display:block}",
            RobotsIndex = true
        }, allowCustomCode: true);
        Assert.Null(adminError);
        Assert.Equal("window.cmsAdmin = true;", adminSettings!.CustomJs);

        var (managerSettings, managerError) = await siteService.SaveSettingsAsync(created.Id, new SaveSiteSettingsRequest
        {
            SiteName = "Manager Updated Name",
            MetaTitle = "Manager SEO Title",
            CustomJs = "window.shouldNotReplace = true;",
            CustomCss = ".should-not-replace{}",
            RobotsIndex = true
        }, allowCustomCode: false);
        Assert.Null(managerError);
        Assert.Equal("window.cmsAdmin = true;", managerSettings!.CustomJs);
        Assert.Equal(".cms-test{display:block}", managerSettings.CustomCss);

        var (richText, richError) = await siteService.CreateSectionAsync(created.Id, new SaveHomeSectionRequest
        {
            Type = "RichText",
            Name = "Sanitized block",
            Variant = "narrow",
            ContentJson = "{\"html\":\"<h2>Xin chào</h2><script>alert(1)</script><p>Nội dung sạch</p>\"}",
            IsVisible = true
        });
        Assert.Null(richError);
        Assert.NotNull(richText);
        var richJson = JsonNode.Parse(richText!.ContentJson)!.AsObject();
        var sanitizedHtml = richJson["html"]!.GetValue<string>();
        Assert.Contains("Xin chào", sanitizedHtml);
        Assert.Contains("Nội dung sạch", sanitizedHtml);
        Assert.DoesNotContain("<script", sanitizedHtml, StringComparison.OrdinalIgnoreCase);

        var (updated, updateError) = await propertyService.UpdateAsync(created.Id, new SavePropertyRequest
        {
            Code = created.Code,
            Name = "CMS Test Property Updated",
            TimeZoneId = "Asia/Ho_Chi_Minh",
            IsActive = false
        });
        Assert.Null(updateError);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Booking_lookup_requires_code_and_phone_and_returns_payment_summary()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Code == SiteContentService.PublicPropertyCode);
        if (property is null)
        {
            property = new Property
            {
                Id = Guid.CreateVersion7(),
                Code = SiteContentService.PublicPropertyCode,
                Name = "De Long Integration",
                TimeZoneId = "Asia/Ho_Chi_Minh",
                IsActive = true
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        }

        if (!await db.Set<PropertySiteSettings>().AnyAsync(x => x.PropertyId == property.Id))
        {
            db.Set<PropertySiteSettings>().Add(new PropertySiteSettings
            {
                PropertyId = property.Id,
                SiteName = "De Long Integration",
                Phone = "0352291921",
                Address = "Long Thành, Đồng Nai"
            });
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            Code = $"LOOKUP-{suffix}",
            Name = "Lookup Test Room",
            Slug = $"lookup-{suffix}",
            Capacity = 2,
            IsActive = true,
            IsPublished = true
        };
        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            Name = "Lookup Guest",
            Phone = "0987654321",
            NormalizedPhone = "0987654321"
        };
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            RoomId = room.Id,
            CustomerId = customer.Id,
            Code = $"BK-LOOKUP-{suffix.ToUpperInvariant()}",
            Type = BookingType.TimeSlot,
            CheckInUtc = new DateTime(2026, 8, 20, 7, 0, 0, DateTimeKind.Utc),
            CheckOutUtc = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
            Status = BookingStatus.Confirmed,
            RoomAmount = 300_000m,
            ExtraAmount = 50_000m,
            DiscountAmount = 20_000m,
            Source = "Integration"
        };
        var receipt = new Payment
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            BookingId = booking.Id,
            Type = PaymentType.Receipt,
            Amount = 150_000m,
            OccurredAtUtc = DateTime.UtcNow,
            Method = PaymentMethod.Cash
        };
        var refund = new Payment
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            BookingId = booking.Id,
            Type = PaymentType.Refund,
            Amount = 30_000m,
            OccurredAtUtc = DateTime.UtcNow,
            Method = PaymentMethod.Cash
        };
        var voidedReceipt = new Payment
        {
            Id = Guid.CreateVersion7(),
            PropertyId = property.Id,
            BookingId = booking.Id,
            Type = PaymentType.Receipt,
            Amount = 999_000m,
            OccurredAtUtc = DateTime.UtcNow,
            Method = PaymentMethod.Cash,
            IsVoided = true,
            VoidedAtUtc = DateTime.UtcNow,
            VoidReason = "Integration void"
        };

        db.Rooms.Add(room);
        db.Customers.Add(customer);
        db.Bookings.Add(booking);
        db.Payments.AddRange(receipt, refund, voidedReceipt);
        await db.SaveChangesAsync();

        var service = new PublicBookingLookupService(db);
        var found = await service.LookupAsync(booking.Code.ToLowerInvariant(), "+84 987 654 321");
        Assert.NotNull(found);
        Assert.Equal("Đã xác nhận", found!.StatusLabel);
        Assert.Equal(330_000m, found.TotalAmount);
        Assert.Equal(120_000m, found.PaidAmount);
        Assert.Equal(210_000m, found.Balance);
        Assert.Equal("0352291921", found.PropertyPhone);

        Assert.Null(await service.LookupAsync(booking.Code, "0900000000"));
        Assert.Null(await service.LookupAsync("BK-NOT-FOUND", customer.Phone));
    }
}
