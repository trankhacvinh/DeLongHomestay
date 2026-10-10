using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Operations;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Site;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class RoomBookingScheduleTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
    private static BookingService Bookings(AppDbContext db) => new(db, new CustomerService(db), new AuditService(db));
    private static RoomBookingBlockService Blocks(AppDbContext db) => new(db, new AuditService(db));
    private static async Task<(Property Property, Room Room, ApplicationUser Actor)> Seed(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Schedule test", SiteSlug = key, IsActive = true, TimeZoneId = "Asia/Ho_Chi_Minh" };
        var room = new Room { Property = property, Code = key, Name = "Blue", IsActive = true, IsPublished = true };
        room.Rates.Add(new RoomRate { Name = "Afternoon", StartTime = new(14,0), EndTime = new(17,0), Price = 250000, Type = RoomRateType.TimeSlot, IsActive = true });
        var actor = new ApplicationUser { UserName = key, DisplayName = "Manager" };
        db.AddRange(property,room,actor); await db.SaveChangesAsync();
        return (property,room,actor);
    }
    private static DateTimeOffset Start => new(2031,1,10,7,0,0,TimeSpan.Zero);
    private static SaveRoomBlockRequest Request(Guid roomId) => new([roomId],"Lý do nội bộ",Start,Start.AddHours(3),false,null,null,null);
    private static CreateBookingRequest Order(Guid roomId, DateTimeOffset? start = null) => new() {
        RoomId=roomId, CustomerName="Guest", CustomerPhone="0935527193", CheckIn=start??Start,
        CheckOut=(start??Start).AddHours(3), RoomAmount=250000, Status=BookingStatus.Confirmed };

    [RoomLockPostgresFact]
    public async Task Schedule_blocks_overlap_but_allows_exact_boundary_and_ending_restores_admission()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        Assert.Null(await Blocks(db).SaveAsync(property.Id,null,Request(room.Id),actor.Id,default));
        Assert.Equal("room_booking_locked",(await Bookings(db).CreateAsync(property.Id,Order(room.Id))).Error?.Code);
        Assert.Null((await Bookings(db).CreateAsync(property.Id,Order(room.Id,Start.AddHours(3)))).Error);
        var batch=await db.RoomBookingBlocks.Where(x=>x.PropertyId==property.Id).Select(x=>x.BatchId).SingleAsync();
        Assert.Null(await Blocks(db).CancelAsync(property.Id,batch,actor.Id,default));
        Assert.Null((await Bookings(db).CreateAsync(property.Id,Order(room.Id))).Error);
        Assert.NotNull((await db.RoomBookingBlocks.SingleAsync(x=>x.BatchId==batch)).CancelledAtUtc);
        Assert.Equal(2,await db.AuditLogs.CountAsync(x=>x.EntityId==batch));
    }

    [RoomLockPostgresFact]
    public async Task Existing_orders_require_explicit_acknowledgement_and_remain_editable()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        var old=(await Bookings(db).CreateAsync(property.Id,Order(room.Id))).Booking!;
        var error=await Blocks(db).SaveAsync(property.Id,null,Request(room.Id),actor.Id,default);
        Assert.Equal("existing_bookings",error?.Code);Assert.Equal(old.Id,Assert.Single(error!.Conflicts!).Id);
        Assert.Empty(await db.RoomBookingBlocks.Where(x=>x.PropertyId==property.Id).ToListAsync());
        Assert.Null(await Blocks(db).SaveAsync(property.Id,null,Request(room.Id) with {AcknowledgeExistingBookings=true,AcknowledgedBookingIds=[old.Id]},actor.Id,default));
        Assert.Equal(old,await Bookings(db).GetAsync(property.Id,old.Id));
        var request=new UpdateBookingRequest {RoomId=room.Id,CustomerId=old.CustomerId,CustomerName=old.CustomerName,CustomerPhone=old.CustomerPhone,CheckIn=Start,CheckOut=Start.AddHours(3),RoomAmount=old.RoomAmount,Note="Updated"};
        Assert.Null((await Bookings(db).UpdateAsync(property.Id,old.Id,request)).Error);
        var other=new Room {PropertyId=property.Id,Code="Other",Name="Other",IsActive=true};db.Add(other);await db.SaveChangesAsync();
        var moving=(await Bookings(db).CreateAsync(property.Id,Order(other.Id))).Booking!;
        Assert.Equal("room_booking_locked",(await new BookingMoveService(db,new AuditService(db),Bookings(db)).MoveAsync(property.Id,moving.Id,new MoveBookingRequest {RoomId=room.Id,TargetDate=new(2031,1,10)})).Error?.Code);
    }

    [RoomLockPostgresFact]
    public async Task Availability_shows_locked_ranges_without_public_reason_and_keeps_other_slots_free()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        Assert.Null(await Blocks(db).SaveAsync(property.Id,null,Request(room.Id),actor.Id,default));
        var dir=Path.Combine(Path.GetTempPath(),"block-test-"+Guid.NewGuid());
        var paths=new StoragePaths(dir,Path.Combine(dir,"media"),"/uploads/rooms",true,true,false);
        try {
            var service=new AvailabilityIntervalService(db,new PublicPropertyResolver(db),paths,new PricingService(db,new AuditService(db)));
            var result=(await service.GetPublicAsync(property.SiteSlug,room.Id,new(2031,1,10),2))!;
            Assert.NotNull(result.BookingScheduleUpdatedAtUtc);
            Assert.Equal("locked",Assert.Single(result.Calendar[0].Slots[0].Occupied).Kind);
            Assert.Null(result.Calendar[0].Slots[0].BookableStartUtc);
            Assert.NotNull(result.Calendar[1].Slots[0].BookableStartUtc);
            Assert.DoesNotContain("Lý do nội bộ",JsonSerializer.Serialize(result));
            var admin=(await service.GetAdminAsync(property.Id,room.Id,new(2031,1,10),1))!;
            Assert.Equal("Lý do nội bộ",Assert.Single(admin.Calendar[0].Slots[0].Occupied).BlockReason);
        } finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }

    [RoomLockPostgresFact]
    public async Task Cross_property_writes_and_invalid_reason_are_rejected()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        Assert.NotNull(await Blocks(db).SaveAsync(Guid.NewGuid(),null,Request(room.Id),actor.Id,default));
        var foreign=new Property {Code=Guid.NewGuid().ToString(),Name="Foreign"};db.Add(foreign);await db.SaveChangesAsync();
        Assert.Equal("room_not_found",(await Blocks(db).SaveAsync(foreign.Id,null,Request(room.Id),actor.Id,default))?.Code);
        Assert.Equal("validation",(await Blocks(db).SaveAsync(property.Id,null,Request(room.Id) with {Reason=new string('x',501)},actor.Id,default))?.Code);
    }

    [RoomLockPostgresFact]
    public async Task Concurrent_booking_waits_for_schedule_commit_then_is_rejected()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        await using var tx=await db.Database.BeginTransactionAsync();
        Assert.Null(await Blocks(db).SaveAsync(property.Id,null,Request(room.Id),actor.Id,default));
        await using var bookingDb=Db();var pending=Bookings(bookingDb).CreateAsync(property.Id,Order(room.Id));
        Assert.NotSame(pending,await Task.WhenAny(pending,Task.Delay(200)));
        await tx.CommitAsync();await tx.DisposeAsync();
        Assert.Equal("room_booking_locked",(await pending.WaitAsync(TimeSpan.FromSeconds(10))).Error?.Code);
    }

    [RoomLockPostgresFact]
    public async Task Booking_committed_first_is_preserved_and_reported_to_schedule_writer()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        await using var tx=await db.Database.BeginTransactionAsync();
        Assert.Null((await Bookings(db).CreateAsync(property.Id,Order(room.Id))).Error);
        await using var blockDb=Db();var pending=Blocks(blockDb).SaveAsync(property.Id,null,Request(room.Id),actor.Id,default);
        Assert.NotSame(pending,await Task.WhenAny(pending,Task.Delay(200)));
        await tx.CommitAsync();await tx.DisposeAsync();
        Assert.Equal("existing_bookings",(await pending.WaitAsync(TimeSpan.FromSeconds(10)))?.Code);
        Assert.Single(await blockDb.Bookings.Where(x=>x.PropertyId==property.Id).ToListAsync());
    }
    [RoomLockPostgresFact]
    public async Task Daily_schedule_updates_release_old_intervals_and_preserve_history()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        var request=Request(room.Id) with {RepeatDaily=true,FromDate=new(2031,1,10),ToDate=new(2031,1,12),Windows=[new(new(14,0),new(17,0))]};
        Assert.Null(await Blocks(db).SaveAsync(property.Id,null,request,actor.Id,default));
        Assert.Equal(3,await db.RoomBookingBlocks.CountAsync(x=>x.PropertyId==property.Id));
        Assert.True(await RoomBookingGuard.HasScheduledLockAsync(db,property.Id,room.Id,Start.UtcDateTime,Start.AddHours(3).UtcDateTime,default));
        var batch=await db.RoomBookingBlocks.Where(x=>x.PropertyId==property.Id).Select(x=>x.BatchId).FirstAsync();
        Assert.Null(await Blocks(db).SaveAsync(property.Id,batch,request with {FromDate=new(2031,1,13),ToDate=new(2031,1,13)},actor.Id,default));
        Assert.False(await RoomBookingGuard.HasScheduledLockAsync(db,property.Id,room.Id,Start.UtcDateTime,Start.AddHours(3).UtcDateTime,default));
        Assert.Equal(3,await db.RoomBookingBlocks.CountAsync(x=>x.PropertyId==property.Id&&x.CancelledAtUtc!=null));
        Assert.Single(await db.RoomBookingBlocks.Where(x=>x.PropertyId==property.Id&&x.CancelledAtUtc==null).ToListAsync());
    }

    [RoomLockPostgresFact]
    public async Task Expired_schedule_does_not_block_new_dates_and_existing_payment_remains_usable()
    {
        await using var db=Db();var (property,room,actor)=await Seed(db);
        db.RoomBookingBlocks.Add(new RoomBookingBlock {PropertyId=property.Id,RoomId=room.Id,BatchId=Guid.NewGuid(),StartUtc=DateTime.UtcNow.AddDays(-2),EndUtc=DateTime.UtcNow.AddDays(-1),Reason="Expired"});
        await db.SaveChangesAsync();
        var old=(await Bookings(db).CreateAsync(property.Id,Order(room.Id))).Booking!;
        Assert.Null(await Blocks(db).SaveAsync(property.Id,null,Request(room.Id) with {AcknowledgeExistingBookings=true,AcknowledgedBookingIds=[old.Id]},actor.Id,default));
        var payments=new DeLong.Web.Features.Payments.PaymentService(db);
        var paid=await payments.AddAsync(property.Id,old.Id,new DeLong.Web.Features.Payments.CreatePaymentRequest {Type=PaymentType.Receipt,Method=PaymentMethod.Cash,Amount=250000},actor.Id);
        Assert.Null(paid.Error);Assert.Equal(0,(await Bookings(db).GetAsync(property.Id,old.Id))!.BalanceAmount);
    }

}
