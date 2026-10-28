using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class BookingTimeSuggestionIntegrationTests
{
    [RoomLockPostgresFact]
    public async Task Suggestions_use_actual_checkout_property_cleaning_and_active_locks_without_mutating_booking_or_rates()
    {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();var suffix=Guid.NewGuid().ToString("N")[..8];
        var property=new Property{Code="SG"+suffix,Name="Suggestion test",TimeZoneId="Asia/Ho_Chi_Minh",HousekeepingCleaningMinutes=30};
        var room=new Room{Property=property,Name="Suggestion",Code="R"+suffix};
        var rate=new RoomRate{Room=room,Name="Khung 3",StartTime=new(18,0),EndTime=new(21,0),Price=210000,IsActive=true};
        var customer=new Customer{Property=property,Name="Synthetic",Phone="0900000000",NormalizedPhone="0900000000"};
        DateTime Utc(int h,int m=0)=>new DateTimeOffset(2031,1,10,h,m,0,TimeSpan.FromHours(7)).UtcDateTime;
        var booking=new Booking{Property=property,Room=room,Customer=customer,Code="SG"+suffix,Status=BookingStatus.Confirmed,CheckInUtc=Utc(14,30),CheckOutUtc=Utc(17,50),RoomAmount=210000};
        db.AddRange(rate,booking);await db.SaveChangesAsync();
        var service=new BookingTimeSuggestionService(db);var day=new DateOnly(2031,1,10);
        var suggestion=(await service.GetAsync(property.Id,room.Id,day,default))!;
        Assert.Equal(Utc(18,20),Assert.Single(suggestion.Rates).SuggestedStartUtc);Assert.Equal(Utc(21),suggestion.Rates[0].EndUtc);
        booking.CheckOutUtc=Utc(18,10);await db.SaveChangesAsync();Assert.Equal(Utc(18,40),(await service.GetAsync(property.Id,room.Id,day,default))!.Rates[0].SuggestedStartUtc);
        property.HousekeepingCleaningMinutes=15;await db.SaveChangesAsync();Assert.Equal(Utc(18,25),(await service.GetAsync(property.Id,room.Id,day,default))!.Rates[0].SuggestedStartUtc);
        foreach(var status in new[]{BookingStatus.Cancelled,BookingStatus.NoShow,BookingStatus.Requested}){
            booking.Status=status;await db.SaveChangesAsync();Assert.Equal(Utc(18),(await service.GetAsync(property.Id,room.Id,day,default))!.Rates[0].SuggestedStartUtc);
        }
        var block=new RoomBookingBlock{PropertyId=property.Id,RoomId=room.Id,BatchId=Guid.NewGuid(),StartUtc=Utc(18),EndUtc=Utc(18,15),Reason="Synthetic"};
        db.Add(block);await db.SaveChangesAsync();Assert.Equal(Utc(18,15),(await service.GetAsync(property.Id,room.Id,day,default))!.Rates[0].SuggestedStartUtc);
        block.CancelledAtUtc=DateTime.UtcNow;room.IsBookingLocked=true;await db.SaveChangesAsync();Assert.Null((await service.GetAsync(property.Id,room.Id,day,default))!.Rates[0].SuggestedStartUtc);
        Assert.Null(await service.GetAsync(Guid.NewGuid(),room.Id,day,default));
        Assert.Equal(Utc(18,10),(await db.Bookings.AsNoTracking().SingleAsync(x=>x.Id==booking.Id)).CheckOutUtc);
        var unchanged=await db.RoomRates.AsNoTracking().SingleAsync(x=>x.Id==rate.Id);Assert.Equal(new TimeOnly(18,0),unchanged.StartTime);Assert.Equal(210000,unchanged.Price);
    }
}
