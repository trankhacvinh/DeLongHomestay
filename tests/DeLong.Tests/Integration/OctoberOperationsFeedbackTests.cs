using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.Payments;
using DeLong.Web.Features.PublicRooms;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class OctoberOperationsFeedbackTests
{
    private static async Task<AppDbContext> Database()
    {
        var connection = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection), "An isolated test database is required.");
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        return db;
    }
    private static Booking Seed(AppDbContext db)
    {
        var property = new Property { Code = Guid.NewGuid().ToString("N")[..16], Name="Synthetic", TimeZoneId="Asia/Ho_Chi_Minh" };
        var room = new Room {Property=property,Code="R",Name="Synthetic",IsActive=true,IsPublished=true};
        room.Rates.Add(new RoomRate {Name="Day",StartTime=new(11,0),EndTime=new(14,0),Price=210000,IsActive=true});
        var customer = new Customer {Property=property,Name="Synthetic",Phone="0900000000",NormalizedPhone="0900000000",IsActive=true};
        var booking = new Booking {Property=property,Room=room,Customer=customer,Code="SYNTHETIC",CheckInUtc=DateTime.UtcNow.AddDays(10),CheckOutUtc=DateTime.UtcNow.AddDays(10).AddHours(3),Status=BookingStatus.Held,RoomAmount=210000};
        db.Bookings.Add(booking);return booking;
    }
    [Fact] public async Task Mobile_layout_round_trips_through_database_and_public_catalog()
    {
        await using var db=await Database();var booking=Seed(db);await db.SaveChangesAsync();
        Assert.True((await new PublicRoomContentService(db).GetCatalogAsync(booking.PropertyId)).Rooms.Single().PublicCalendarMobileAllRooms);
        booking.Property.PublicCalendarMobileAllRooms=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        Assert.False((await new PublicRoomContentService(db).GetCatalogAsync(booking.PropertyId)).Rooms.Single().PublicCalendarMobileAllRooms);
        Assert.Equal(210000,await db.Bookings.Where(x=>x.Id==booking.Id).Select(x=>x.RoomAmount).SingleAsync());
    }
    [Fact] public async Task Telegram_filters_event_outbox_and_does_not_duplicate_status_notification()
    {
        await using var db=await Database();var booking=Seed(db);
        var settings=new PropertyNotificationSettings {Property=booking.Property,InAppBookingEnabled=true,TelegramBookingEnabled=true,TelegramBotTokenProtected="synthetic-protected-token",TelegramChatIds="synthetic-chat",TelegramEventTypes="booking-confirmed"};
        db.Add(settings);await db.SaveChangesAsync();
        var service=new BookingNotificationService(db,new NotificationRealtimeBroker(),NullLogger<BookingNotificationService>.Instance);
        await service.NotifyBookingCreatedAsync(booking.PropertyId,booking.Id);
        Assert.Equal(0,await db.NotificationTelegramOutbox.CountAsync(x=>x.PropertyId==booking.PropertyId));
        booking.Status=BookingStatus.Confirmed;await db.SaveChangesAsync();
        await service.NotifyBookingStatusChangedAsync(booking.PropertyId,booking.Id,BookingStatus.Held,BookingStatus.Confirmed);
        await service.NotifyBookingStatusChangedAsync(booking.PropertyId,booking.Id,BookingStatus.Held,BookingStatus.Confirmed);
        Assert.Equal(1,await db.NotificationTelegramOutbox.CountAsync(x=>x.PropertyId==booking.PropertyId));
        settings.TelegramEventTypes="";await db.SaveChangesAsync();
        await service.NotifyBookingStatusChangedAsync(booking.PropertyId,booking.Id,BookingStatus.Confirmed,BookingStatus.Cancelled);
        Assert.Equal(1,await db.NotificationTelegramOutbox.CountAsync(x=>x.PropertyId==booking.PropertyId));
    }
    [Fact] public async Task Audit_pages_and_activity_are_scoped_and_record_payment_changes_without_secrets()
    {
        await using var db=await Database();var booking=Seed(db);
        var user=new ApplicationUser{Id=Guid.NewGuid(),UserName=Guid.NewGuid().ToString("N"),DisplayName="Synthetic operator"};db.Users.Add(user);await db.SaveChangesAsync();
        var audit=new AuditService(db);audit.Add(booking.PropertyId,"Booking",booking.Id,"Created",user.Id,after:new {booking.RoomAmount});await db.SaveChangesAsync();
        for(var i=0;i<55;i++)audit.Add(booking.PropertyId,"Booking",booking.Id,"Updated",user.Id,new{RoomAmount=210000},new{RoomAmount=210000+i});
        await db.SaveChangesAsync();
        Assert.Equal(50,(await audit.GetEntityHistoryAsync(booking.PropertyId,"Booking",booking.Id)).Count);
        Assert.Equal(6,(await audit.GetEntityHistoryAsync(booking.PropertyId,"Booking",booking.Id,skip:50)).Count);
        Assert.Empty(await audit.GetEntityHistoryAsync(Guid.NewGuid(),"Booking",booking.Id));
        var activity=JsonSerializer.Serialize(await audit.GetBookingActivityAsync(booking.PropertyId,booking.Id),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("Synthetic operator",activity);
        var payments=new PaymentService(db,auditService:audit);
        var receipt=await payments.AddAsync(booking.PropertyId,booking.Id,new CreatePaymentRequest{Amount=210000,Type=PaymentType.Receipt,Method=PaymentMethod.BankTransfer},user.Id);
        Assert.Null(receipt.Error);await payments.VoidAsync(booking.PropertyId,receipt.Payment!.Id,"Synthetic correction",user.Id);
        var history=await audit.GetEntityHistoryAsync(booking.PropertyId,"Booking",booking.Id);
        Assert.Contains(history,x=>x.Action=="PaymentReceived" && x.ActorUserId==user.Id);
        Assert.Contains(history,x=>x.Action=="PaymentVoided" && x.ActorUserId==user.Id);
        Assert.Equal(BookingStatus.Held,await db.Bookings.Where(x=>x.Id==booking.Id).Select(x=>x.Status).SingleAsync());
    }
}
