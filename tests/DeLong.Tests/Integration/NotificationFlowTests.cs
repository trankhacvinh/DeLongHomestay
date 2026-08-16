using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class NotificationFlowTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Booking_notification_is_deduplicated_and_read_state_is_per_user()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"NOTIF-{suffix}", Name = "Notification Test", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Property = property, Code = $"N-{suffix}", Name = "Notification Room", Slug = $"notification-{suffix}", Capacity = 2, IsActive = true, IsPublished = true };
        var customer = new Customer { Property = property, Name = "Notification Guest", Phone = $"09{suffix[..8]}", NormalizedPhone = $"09{suffix[..8]}", IsActive = true };
        var booking = new Booking
        {
            Property = property,
            Room = room,
            Customer = customer,
            Code = $"BK-N-{suffix}",
            Type = BookingType.TimeSlot,
            CheckInUtc = DateTime.UtcNow.AddDays(2),
            CheckOutUtc = DateTime.UtcNow.AddDays(2).AddHours(3),
            Status = BookingStatus.Requested,
            RoomAmount = 250_000m,
            Source = "Website"
        };
        var user1 = NewUser($"notify1-{suffix}@example.test", "Notify One");
        var user2 = NewUser($"notify2-{suffix}@example.test", "Notify Two");
        db.AddRange(property, room, customer, booking, user1, user2);
        db.Add(new PropertyNotificationSettings
        {
            Property = property,
            InAppBookingEnabled = true,
            EmailBookingEnabled = true,
            EmailRecipients = "ops@example.test"
        });
        await db.SaveChangesAsync();

        var service = new BookingNotificationService(db, new NotificationRealtimeBroker(), NullLogger<BookingNotificationService>.Instance);
        await service.NotifyBookingCreatedAsync(property.Id, booking.Id);
        await service.NotifyBookingCreatedAsync(property.Id, booking.Id);

        Assert.Equal(1, await db.Set<PropertyNotification>().CountAsync(x => x.BookingId == booking.Id));
        Assert.Equal(1, await db.Set<NotificationEmailOutbox>().CountAsync(x => x.PropertyId == property.Id));

        var feed1 = await service.GetFeedAsync(property.Id, user1.Id);
        var feed2 = await service.GetFeedAsync(property.Id, user2.Id);
        Assert.Equal(1, feed1.UnreadCount);
        Assert.Equal(1, feed2.UnreadCount);

        var notificationId = Assert.Single(feed1.Items).Id;
        Assert.True(await service.MarkReadAsync(property.Id, notificationId, user1.Id));

        feed1 = await service.GetFeedAsync(property.Id, user1.Id);
        feed2 = await service.GetFeedAsync(property.Id, user2.Id);
        Assert.Equal(0, feed1.UnreadCount);
        Assert.Equal(1, feed2.UnreadCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Email_only_booking_event_is_queued_but_not_visible_in_in_app_feed()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"EMAILONLY-{suffix}", Name = "Email Only Test", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        var room = new Room { Property = property, Code = $"EO-{suffix}", Name = "Email Only Room", Slug = $"email-only-{suffix}", Capacity = 2, IsActive = true, IsPublished = true };
        var customer = new Customer { Property = property, Name = "Email Only Guest", Phone = $"08{suffix}", NormalizedPhone = $"08{suffix}", IsActive = true };
        var booking = new Booking
        {
            Property = property,
            Room = room,
            Customer = customer,
            Code = $"BK-EO-{suffix}",
            Type = BookingType.TimeSlot,
            CheckInUtc = DateTime.UtcNow.AddDays(3),
            CheckOutUtc = DateTime.UtcNow.AddDays(3).AddHours(2),
            Status = BookingStatus.Requested,
            RoomAmount = 300_000m,
            Source = "Website"
        };
        var user = NewUser($"emailonly-{suffix}@example.test", "Email Only User");
        db.AddRange(property, room, customer, booking, user);
        db.Add(new PropertyNotificationSettings
        {
            Property = property,
            InAppBookingEnabled = false,
            EmailBookingEnabled = true,
            EmailRecipients = "ops@example.test"
        });
        await db.SaveChangesAsync();

        var service = new BookingNotificationService(db, new NotificationRealtimeBroker(), NullLogger<BookingNotificationService>.Instance);
        await service.NotifyBookingCreatedAsync(property.Id, booking.Id);

        var persisted = await db.Set<PropertyNotification>().AsNoTracking().SingleAsync(x => x.BookingId == booking.Id);
        Assert.Equal("booking-email-only", persisted.Type);
        Assert.Equal(1, await db.Set<NotificationEmailOutbox>().CountAsync(x => x.NotificationId == persisted.Id));

        var feed = await service.GetFeedAsync(property.Id, user.Id);
        Assert.Empty(feed.Items);
        Assert.Equal(0, feed.UnreadCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Smtp_password_is_encrypted_in_database_and_decrypted_for_delivery()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"SMTP-{suffix}", Name = "SMTP Test", TimeZoneId = "Asia/Ho_Chi_Minh", IsActive = true };
        db.Add(property);
        await db.SaveChangesAsync();

        var protector = new SmtpCredentialProtector(new EphemeralDataProtectionProvider());
        var service = new NotificationSettingsService(db, protector);
        const string password = "smtp-test-password";
        var (saved, error) = await service.SaveAsync(property.Id, new UpdateNotificationSettingsRequest
        {
            InAppBookingEnabled = true,
            EmailBookingEnabled = true,
            EmailRecipients = "owner@example.test;staff@example.test",
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpUseSsl = true,
            SmtpUsername = "mailer@example.test",
            SmtpPassword = password,
            SmtpFromEmail = "mailer@example.test",
            SmtpFromName = "De Long Test"
        });

        Assert.Null(error);
        Assert.NotNull(saved);
        Assert.True(saved!.SmtpPasswordConfigured);

        var entity = await db.Set<PropertyNotificationSettings>().AsNoTracking().SingleAsync(x => x.PropertyId == property.Id);
        Assert.NotEqual(password, entity.SmtpPasswordProtected);
        Assert.StartsWith("dp:v1:", entity.SmtpPasswordProtected);

        var (profile, recipients, profileError) = await service.GetDeliveryProfileAsync(property.Id, true);
        Assert.Null(profileError);
        Assert.NotNull(profile);
        Assert.Equal(password, profile!.Password);
        Assert.Equal(2, recipients.Count);
    }

    private static ApplicationUser NewUser(string email, string displayName) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true,
        DisplayName = displayName,
        IsActive = true,
        SecurityStamp = Guid.NewGuid().ToString("N")
    };
}
