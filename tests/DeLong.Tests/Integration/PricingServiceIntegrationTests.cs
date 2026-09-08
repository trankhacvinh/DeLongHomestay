using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Pricing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PricingServiceIntegrationTests
{
    [PricingPostgreSqlFact]
    [Trait("Category", "Integration")]
    public async Task Weekend_combo_and_special_surcharge_are_calculated_in_the_required_order()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"PRICE-{suffix}", Name = "Pricing test", TimeZoneId = "Asia/Ho_Chi_Minh" };
        db.Add(property);
        await db.SaveChangesAsync();
        db.Add(new PropertyPricingSettings { PropertyId = property.Id, ThreeSlotDiscountEnabled = true, ThreeSlotCount = 3, ThreeSlotDiscountPercent = 10 });
        var saturday = new DateOnly(2027, 1, 2);
        db.Add(new SpecialPricingDay { PropertyId = property.Id, StartDate = saturday, EndDate = saturday, Name = "Lễ thử", BasePriceProfile = PricingDayProfile.Weekend, SurchargePercent = 10, AllowThreeSlotCombo = true });
        await db.SaveChangesAsync();
        var inputs = Enumerable.Range(0, 3).Select(index => new PricingSelectionInput(
            saturday, index, Guid.NewGuid(), $"Ca {index + 1}", 100m, false, 200m)).ToList();

        var service = new PricingService(db, new AuditService(db));
        var (result, error) = await service.CalculateAsync(property.Id, inputs, 4, true, 650m, false, 700m);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(540m, result.RoomAmount);
        Assert.Equal(54m, result.SpecialSurchargeAmount);
        Assert.Equal(594m, result.TotalBeforeVoucher);
        Assert.All(result.Segments, x => Assert.Equal(10m, x.ComboDiscountPercent));
    }

    [PricingPostgreSqlFact]
    [Trait("Category", "Integration")]
    public async Task Full_day_only_rejects_partial_selection_and_uses_weekend_full_day_price()
    {
        var connectionString = Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = $"FULL-{suffix}", Name = "Full day test", TimeZoneId = "Asia/Ho_Chi_Minh" };
        db.Add(property);
        await db.SaveChangesAsync();
        var saturday = new DateOnly(2027, 1, 2);
        db.Add(new SpecialPricingDay { PropertyId = property.Id, StartDate = saturday, EndDate = saturday, Name = "Tết", BasePriceProfile = PricingDayProfile.Weekend, SurchargePercent = 15, BookingMode = SpecialDayBookingMode.FullDayOnly, AllowThreeSlotCombo = false });
        await db.SaveChangesAsync();
        var inputs = Enumerable.Range(0, 4).Select(index => new PricingSelectionInput(
            saturday, index, Guid.NewGuid(), $"Ca {index + 1}", 100m, false, 200m)).ToList();
        var service = new PricingService(db, new AuditService(db));

        var (_, partialError) = await service.CalculateAsync(property.Id, inputs.Take(3).ToList(), 4, true, 650m, false, 700m);
        var (full, fullError) = await service.CalculateAsync(property.Id, inputs, 4, true, 650m, false, 700m);

        Assert.Equal("full_day_only", partialError?.Code);
        Assert.Null(fullError);
        Assert.Equal(700m, full!.RoomAmount);
        Assert.Equal(105m, full.SpecialSurchargeAmount);
        Assert.Equal(805m, full.TotalBeforeVoucher);
    }
}

public sealed class PricingPostgreSqlFactAttribute : FactAttribute
{
    public PricingPostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")))
            Skip = "Cần DELONG_TEST_CONNECTION để chạy kiểm thử PostgreSQL cho PricingService.";
    }
}
