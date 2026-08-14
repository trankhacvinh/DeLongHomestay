using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Data.Seed;

public static class DbSeeder
{
    public static readonly Guid DeLongPropertyId = Guid.Parse("0198A5A0-1000-7000-8000-000000000001");

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedPropertyAsync(db);
        await SeedDevelopmentAdminAsync(db, userManager, configuration);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in new[] { "Admin", "Manager", "Staff", "Housekeeping", "Viewer" })
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>
            {
                Id = Guid.CreateVersion7(),
                Name = roleName
            });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to seed role {roleName}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
            }
        }
    }

    private static async Task SeedPropertyAsync(AppDbContext db)
    {
        if (await db.Properties.AnyAsync(x => x.Id == DeLongPropertyId)) return;

        var property = new Property
        {
            Id = DeLongPropertyId,
            Code = "DELONG",
            Name = "De Long Homestay",
            TimeZoneId = "Asia/Ho_Chi_Minh"
        };

        var roomDefinitions = new[]
        {
            RoomSeed("COCO-01", "Coco Blue #1", 1, 250_000m, 360_000m),
            RoomSeed("ABAUS-02", "Abaus #2", 2, 210_000m, 330_000m),
            RoomSeed("HONGKONG-03", "Hongkong #3", 3, 250_000m, 360_000m),
            RoomSeed("MOON-04", "Moon Stone #4", 4, 270_000m, 390_000m),
            RoomSeed("AMBER-05", "Amber Stay #5", 5, 300_000m, 439_000m),
            RoomSeed("ROMAN-06", "La Roman #6", 6, 270_000m, 390_000m)
        };

        foreach (var room in roomDefinitions)
        {
            property.Rooms.Add(room);
        }

        db.Properties.Add(property);
        await db.SaveChangesAsync();
    }

    private static Room RoomSeed(string code, string name, int sortOrder, decimal dayPrice, decimal overnightPrice)
    {
        var room = new Room
        {
            Code = code,
            Name = name,
            Slug = code.ToLowerInvariant(),
            Capacity = 2,
            SortOrder = sortOrder,
            IsActive = true,
            IsPublished = true
        };

        var schedule = code switch
        {
            "COCO-01" => new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(10, 30), new TimeOnly(13, 30)),
                (new TimeOnly(14, 0), new TimeOnly(17, 0)),
                (new TimeOnly(17, 30), new TimeOnly(20, 30)),
                (new TimeOnly(21, 0), new TimeOnly(9, 30))
            },
            "ABAUS-02" or "HONGKONG-03" => new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(11, 0), new TimeOnly(14, 0)),
                (new TimeOnly(14, 30), new TimeOnly(17, 30)),
                (new TimeOnly(18, 0), new TimeOnly(21, 0)),
                (new TimeOnly(21, 30), new TimeOnly(10, 0))
            },
            "MOON-04" => new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(11, 30), new TimeOnly(14, 30)),
                (new TimeOnly(15, 0), new TimeOnly(18, 0)),
                (new TimeOnly(18, 30), new TimeOnly(21, 30)),
                (new TimeOnly(22, 0), new TimeOnly(10, 30))
            },
            "AMBER-05" or "ROMAN-06" => new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(12, 0), new TimeOnly(15, 0)),
                (new TimeOnly(15, 30), new TimeOnly(18, 30)),
                (new TimeOnly(19, 0), new TimeOnly(22, 0)),
                (new TimeOnly(22, 30), new TimeOnly(11, 0))
            },
            _ => throw new InvalidOperationException($"Unknown seeded room code: {code}")
        };

        room.Rates.Add(Rate("Khung 1", 1, schedule[0].Start, schedule[0].End, dayPrice));
        room.Rates.Add(Rate("Khung 2", 2, schedule[1].Start, schedule[1].End, dayPrice));
        room.Rates.Add(Rate("Khung 3", 3, schedule[2].Start, schedule[2].End, dayPrice));
        room.Rates.Add(Rate("Qua đêm", 4, schedule[3].Start, schedule[3].End, overnightPrice, true));
        return room;
    }

    private static RoomRate Rate(
        string name,
        int sortOrder,
        TimeOnly start,
        TimeOnly end,
        decimal price,
        bool overnight = false) => new()
        {
            Name = name,
            SortOrder = sortOrder,
            StartTime = start,
            EndTime = end,
            Type = overnight ? RoomRateType.Overnight : RoomRateType.TimeSlot,
            Price = price,
            IsOvernight = overnight,
            IsActive = true
        };

    private static async Task SeedDevelopmentAdminAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var email = configuration["Seed:AdminEmail"]?.Trim();
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                DisplayName = "De Long Admin",
                EmailConfirmed = true,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to seed admin: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }

        var hasAccess = await db.UserPropertyAccesses
            .AnyAsync(x => x.UserId == user.Id && x.PropertyId == DeLongPropertyId);
        if (!hasAccess)
        {
            db.UserPropertyAccesses.Add(new UserPropertyAccess
            {
                UserId = user.Id,
                PropertyId = DeLongPropertyId
            });
            await db.SaveChangesAsync();
        }
    }
}
