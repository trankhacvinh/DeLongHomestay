using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using DeLong.Web.Features.PublicAi;
using DeLong.Web.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class StaffAiRoomStateTests
{
    [PricingPostgreSqlFact]
    [Trait("Category", "Integration")]
    public async Task Current_room_state_uses_locking_bookings_and_unresolved_condition_reports()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = new Property { Code = $"STAFF-AI-{suffix}", Name = "Staff AI test", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var occupiedRoom = new Room { Property = property, Code = "OCC", Name = "Phòng có khách", HousekeepingStatus = HousekeepingStatus.Clean };
        var availableRoom = new Room { Property = property, Code = "FREE", Name = "Phòng trống", HousekeepingStatus = HousekeepingStatus.Dirty };
        var customer = new Customer { Property = property, Name = "Khách thử", Phone = $"09{suffix[..8]}", NormalizedPhone = $"09{suffix[..8]}" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"staff-{suffix}", DisplayName = "Staff tester" };
        db.AddRange(property, occupiedRoom, availableRoom, customer, user);
        db.PropertyAiProfiles.Add(new PropertyAiProfile
        {
            Property = property,
            Provider = AiProviderKind.OpenAi,
            Model = "test-model",
            IsEnabled = true,
            ProtectedApiKey = "test-only"
        });
        var now = DateTime.UtcNow;
        db.Bookings.Add(new Booking
        {
            Property = property,
            Room = occupiedRoom,
            Customer = customer,
            Code = $"BK-{suffix}",
            Status = BookingStatus.CheckedIn,
            CheckInUtc = now.AddHours(-1),
            CheckOutUtc = now.AddHours(2)
        });
        db.RoomConditionReports.Add(new RoomConditionReport
        {
            Property = property,
            Room = availableRoom,
            ReportedByUserId = user.Id,
            Severity = RoomConditionSeverity.Urgent,
            Status = RoomConditionReportStatus.InProgress,
            Content = "Cần kiểm tra điều hòa"
        });
        await db.SaveChangesAsync();

        var service = new StaffAiService(db, new AiAccessGateway(db));
        var (response, error) = await service.AskAsync(property.Id, user.Id, "phòng nào đang trống và có khách?", null, false, default);

        Assert.Null(error);
        var table = Assert.Single(response!.Tables, x => x.Title == "Trạng thái phòng hiện tại");
        Assert.Contains(table.Rows, x => x[0] == occupiedRoom.Name && x[1] == "Đang có khách" && x[3] == "Không có");
        Assert.Contains(table.Rows, x => x[0] == availableRoom.Name && x[1] == "Đang trống" && x[2] == "Cần dọn" && x[3] == "Khẩn cấp (1)");
        Assert.Contains("1 phòng đang có khách", response.Message);
        Assert.Contains("1 phòng trống", response.Message);
        Assert.Contains("1 phòng có cảnh báo", response.Message);
    }

    [PricingPostgreSqlFact]
    [Trait("Category", "Integration")]
    public async Task Finance_permission_and_property_scope_are_enforced_server_side()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var propertyA = new Property { Code = $"STAFF-A-{suffix}", Name = "Cơ sở được phép", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var propertyB = new Property { Code = $"STAFF-B-{suffix}", Name = "Cơ sở khác", TimeZoneId = "Asia/Ho_Chi_Minh" };
        var roomA = new Room { Property = propertyA, Code = "ROOM-A", Name = $"Phòng A {suffix}", IsActive = true };
        var roomB = new Room { Property = propertyB, Code = "ROOM-B", Name = $"Phòng B bí mật {suffix}", IsActive = true };
        var customerA = new Customer { Property = propertyA, Name = $"Khách A {suffix}", Phone = $"A-{suffix}", NormalizedPhone = $"A-{suffix}" };
        var customerB = new Customer { Property = propertyB, Name = $"Khách B bí mật {suffix}", Phone = $"B-{suffix}", NormalizedPhone = $"B-{suffix}" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"staff-scope-{suffix}", DisplayName = "Staff scope tester" };
        db.AddRange(propertyA, propertyB, roomA, roomB, customerA, customerB, user);
        db.PropertyAiProfiles.Add(new PropertyAiProfile
        {
            Property = propertyA,
            Provider = AiProviderKind.OpenAi,
            Model = "test-model",
            IsEnabled = true,
            ProtectedApiKey = "test-only"
        });

        var now = DateTime.UtcNow;
        var bookingA = new Booking
        {
            Property = propertyA, Room = roomA, Customer = customerA, Code = $"BK-A-{suffix}",
            Status = BookingStatus.Confirmed, CheckInUtc = now.AddHours(-1), CheckOutUtc = now.AddHours(1)
        };
        var bookingB = new Booking
        {
            Property = propertyB, Room = roomB, Customer = customerB, Code = $"BK-B-{suffix}",
            Status = BookingStatus.Confirmed, CheckInUtc = now.AddHours(-1), CheckOutUtc = now.AddHours(1)
        };
        db.Bookings.AddRange(bookingA, bookingB);
        db.Payments.AddRange(
            new Payment { Property = propertyA, Booking = bookingA, Amount = 125_000m, Type = PaymentType.Receipt, OccurredAtUtc = now },
            new Payment { Property = propertyB, Booking = bookingB, Amount = 975_000m, Type = PaymentType.Receipt, OccurredAtUtc = now });
        await db.SaveChangesAsync();

        var service = new StaffAiService(db, new AiAccessGateway(db));
        var (restricted, restrictedError) = await service.AskAsync(
            propertyA.Id, user.Id, "doanh thu và booking hôm nay", null, false, default);

        Assert.Null(restrictedError);
        Assert.NotNull(restricted);
        Assert.DoesNotContain(restricted.Tables, x => x.Title == "Thực thu");
        Assert.Contains("không có quyền xem dữ liệu tài chính", restricted.Message);
        AssertResponseDoesNotContain(restricted, suffix, bookingB.Code, roomB.Name, customerB.Name, customerB.Phone);

        var (allowed, allowedError) = await service.AskAsync(
            propertyA.Id, user.Id, "doanh thu và booking hôm nay", null, true, default);

        Assert.Null(allowedError);
        Assert.NotNull(allowed);
        var finance = Assert.Single(allowed.Tables, x => x.Title == "Thực thu");
        Assert.Equal("125,000 đ", finance.Rows[0][1]);
        Assert.Contains(bookingA.Code, Flatten(allowed));
        AssertResponseDoesNotContain(allowed, suffix, bookingB.Code, roomB.Name, customerB.Name, customerB.Phone, "975,000 đ");
    }

    private static void AssertResponseDoesNotContain(StaffAiResponse response, string suffix, params string[] forbiddenValues)
    {
        var flattened = Flatten(response);
        foreach (var value in forbiddenValues)
            Assert.DoesNotContain(value, flattened, StringComparison.Ordinal);

        Assert.DoesNotContain($"BK-B-{suffix}", flattened, StringComparison.Ordinal);
    }

    private static string Flatten(StaffAiResponse response) =>
        string.Join('\n', response.Tables.SelectMany(x => x.Rows).SelectMany(x => x));
}
