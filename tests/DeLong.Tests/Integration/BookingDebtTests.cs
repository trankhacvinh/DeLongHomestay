using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class BookingDebtTests
{
    [RoomLockPostgresFact]
    public async Task Completed_booking_balance_tracks_partial_receipts_refunds_and_voids_without_changing_status()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);
        await db.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var property = new Property { Code = key, Name = "Synthetic debt" };
        var room = new Room { Property = property, Code = "R", Name = "Synthetic room" };
        var customer = new Customer { Property = property, Name = "Synthetic", Phone = "0900000000", NormalizedPhone = "0900000000" };
        var booking = new Booking { Property = property, Room = room, Customer = customer, Code = key[..12],
            CheckInUtc = new(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc), CheckOutUtc = new(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc),
            RoomAmount = 300000, Status = BookingStatus.Completed, DisplayColor = "#397967" };
        db.Bookings.Add(booking); await db.SaveChangesAsync();
        var service = new BookingService(db, new CustomerService(db), new AuditService(db));
        var payments = new PaymentService(db);
        async Task AssertBalance(decimal expected)
        {
            var row = Assert.Single(await service.GetAllAsync(property.Id));
            Assert.Equal(expected, row.BalanceAmount);
            Assert.Equal(BookingStatus.Completed, row.Status);
            Assert.Equal("#397967", row.DisplayColor);
        }
        async Task<PaymentDto> Add(PaymentType type, decimal amount)
        {
            var result = await payments.AddAsync(property.Id, booking.Id, new CreatePaymentRequest { Type = type, Amount = amount, Method = PaymentMethod.BankTransfer }, null);
            Assert.Null(result.Error); return result.Payment!;
        }
        await AssertBalance(300000);
        await Add(PaymentType.Receipt, 100000); await AssertBalance(200000);
        var second = await Add(PaymentType.Receipt, 200000); await AssertBalance(0);
        var refund = await Add(PaymentType.Refund, 50000); await AssertBalance(50000);
        Assert.Null((await payments.VoidAsync(property.Id, refund.Id, "Synthetic correction", null)).Error);
        await AssertBalance(0);
        Assert.Null((await payments.VoidAsync(property.Id, second.Id, "Synthetic correction", null)).Error);
        await AssertBalance(200000);
        Assert.Empty(await service.GetAllAsync(Guid.NewGuid()));
    }
}
