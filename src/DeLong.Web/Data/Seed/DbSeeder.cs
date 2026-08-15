using System.Text.Json;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Data.Seed;

public static class DbSeeder
{
    public static readonly Guid DeLongPropertyId = Guid.Parse("0198A5A0-1000-7000-8000-000000000001");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedPropertyAsync(db);
        await SeedWebsiteStarterAsync(db);
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
            SiteSlug = "de-long",
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

        foreach (var room in roomDefinitions) property.Rooms.Add(room);
        db.Properties.Add(property);
        await db.SaveChangesAsync();
    }

    private static async Task SeedWebsiteStarterAsync(AppDbContext db)
    {
        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == DeLongPropertyId);
        if (property is null) return;

        var settings = await db.Set<PropertySiteSettings>().SingleOrDefaultAsync(x => x.PropertyId == property.Id);
        if (settings is null)
        {
            settings = new PropertySiteSettings
            {
                PropertyId = property.Id,
                SiteName = property.Name,
                Tagline = "Long Thành · Đồng Nai",
                MetaTitle = "De Long Homestay · Nghỉ theo giờ, qua đêm tại Long Thành",
                MetaDescription = "Khám phá phòng nghỉ riêng tư tại De Long Homestay, xem giá và lịch trống rõ ràng rồi gửi yêu cầu đặt phòng trực tiếp.",
                RobotsIndex = true
            };
            db.Set<PropertySiteSettings>().Add(settings);
        }

        if (!await db.Set<HomeSection>().AnyAsync(x => x.PropertyId == property.Id))
        {
            db.Set<HomeSection>().AddRange(
                Section(property.Id, 0, "Hero", "Mở đầu", "split", new
                {
                    eyebrow = "DE LONG HOMESTAY",
                    title = "Một khoảng nghỉ riêng tư, vừa đủ để chậm lại.",
                    body = "Chọn căn phòng phù hợp cho một buổi nghỉ ngắn, qua đêm hoặc vài ngày tại Long Thành.",
                    primaryText = "Đặt phòng", primaryUrl = "/booking",
                    secondaryText = "Xem phòng", secondaryUrl = "/rooms",
                    imageUrl = ""
                }),
                Section(property.Id, 1, "AvailabilitySearch", "Đặt phòng nhanh", "booking-bar", new
                {
                    title = "Chọn ngày bạn muốn ghé"
                }),
                Section(property.Id, 2, "FeatureGrid", "Câu chuyện cơ sở", "split", new
                {
                    eyebrow = "MỘT NƠI ĐỂ NGHỈ CHẬM",
                    title = "Không gian vừa đủ riêng tư cho nhịp nghỉ của bạn.",
                    body = "De Long tập trung vào trải nghiệm đặt phòng đơn giản: xem đúng phòng, đúng giá, đúng khung giờ và được nhân viên xác nhận trực tiếp.",
                    items = new[] { "Không gian riêng tư", "Giá hiển thị rõ ràng", "Khung giờ linh hoạt", "Xác nhận trực tiếp" },
                    imageUrl = ""
                }),
                Section(property.Id, 3, "RoomGrid", "Phòng nổi bật", "editorial-cards", new
                {
                    eyebrow = "KHÔNG GIAN",
                    title = "Chọn căn phòng hợp với nhịp của bạn",
                    limit = 6
                }),
                Section(property.Id, 4, "Cta", "CTA đặt phòng", "offer", new
                {
                    title = "Chọn một khoảng nghỉ cho hôm nay.",
                    body = "Xem lịch trống theo ngày và gửi yêu cầu đặt phòng chỉ trong vài bước.",
                    buttonText = "Đặt phòng", buttonUrl = "/booking"
                })
            );
        }

        if (!await db.Set<HomeSection>().AnyAsync(x => x.PropertyId == null))
        {
            db.Set<HomeSection>().AddRange(
                Section(null, 0, "Hero", "Mở đầu trang chung", "split", new
                {
                    eyebrow = "DE LONG HOMESTAY",
                    title = "Chọn một không gian cho nhịp nghỉ của bạn.",
                    body = "Xem các cơ sở và phòng đang mở, sau đó đặt trực tiếp tại đúng nơi bạn muốn ghé.",
                    primaryText = "Xem tất cả phòng", primaryUrl = "/rooms",
                    secondaryText = "Khám phá các cơ sở", secondaryUrl = "/#co-so",
                    imageUrl = ""
                }),
                Section(null, 1, "AvailabilitySearch", "Đặt phòng nhanh", "booking-bar", new
                {
                    title = "Chọn cơ sở và ngày bạn muốn ghé"
                }),
                Section(null, 2, "FeatureGrid", "Câu chuyện De Long", "split", new
                {
                    eyebrow = "DE LONG HOMESTAY",
                    title = "Mỗi cơ sở một cá tính, cùng một trải nghiệm đặt phòng rõ ràng.",
                    body = "Khách có thể khám phá nhiều cơ sở trên cùng website, xem phòng theo từng nơi và luôn biết mình đang đặt ở đâu.",
                    items = new[] { "Phòng và giá rõ ràng", "Dữ liệu tách theo cơ sở", "Đặt đúng chi nhánh", "Tra cứu thuận tiện" },
                    imageUrl = ""
                }),
                Section(null, 3, "RoomGrid", "Phòng nổi bật", "editorial-cards", new
                {
                    eyebrow = "PHÒNG",
                    title = "Một vài lựa chọn đang mở",
                    mode = "all",
                    limit = 6,
                    propertyQuotas = new Dictionary<string, int>(),
                    roomIds = Array.Empty<Guid>()
                }),
                Section(null, 4, "BranchGrid", "Danh sách cơ sở", "editorial", new
                {
                    eyebrow = "CƠ SỞ",
                    title = "Chọn nơi bạn muốn ghé",
                    propertyIds = Array.Empty<Guid>()
                }),
                Section(null, 5, "Cta", "CTA cuối trang", "offer", new
                {
                    title = "Tìm một căn phòng cho hôm nay.",
                    body = "Chọn cơ sở, ngày sử dụng và căn phòng phù hợp với bạn.",
                    buttonText = "Đặt phòng", buttonUrl = "/booking"
                })
            );
        }

        if (!await db.BlogPosts.AnyAsync(x => x.PropertyId == property.Id))
        {
            var now = DateTime.UtcNow;
            db.BlogPosts.AddRange(
                Blog(property.Id, "mot-buoi-nghi-ngan-o-long-thanh-nen-chuan-bi-gi",
                    "Một buổi nghỉ ngắn ở Long Thành nên chuẩn bị gì?",
                    "Một checklist nhỏ giúp bạn chọn giờ, chuẩn bị đồ dùng và tận dụng khoảng nghỉ ngắn thoải mái hơn.",
                    "<p>Một buổi nghỉ ngắn không cần quá nhiều chuẩn bị. Điều quan trọng nhất là xác định giờ bạn muốn đến, số người và loại không gian phù hợp.</p><h2>Chọn khung giờ trước</h2><p>Xem lịch trống và giá ngay trên website giúp bạn chủ động hơn, đặc biệt vào cuối tuần.</p><h2>Mang theo những gì cần thiết</h2><p>Giấy tờ cá nhân, đồ dùng riêng và những vật dụng phục vụ kế hoạch trong ngày thường là đủ.</p><p>Nếu có yêu cầu đặc biệt, hãy ghi chú khi gửi yêu cầu đặt phòng để nhân viên cơ sở xác nhận trước.</p>", now.AddDays(-8)),
                Blog(property.Id, "chon-khung-gio-hay-qua-dem",
                    "Chọn khung giờ hay qua đêm: đâu là lựa chọn phù hợp?",
                    "Khung giờ phù hợp cho một khoảng nghỉ ngắn; qua đêm phù hợp khi bạn muốn có nhiều thời gian và ít phải để ý đồng hồ.",
                    "<p>Mỗi kiểu lưu trú phù hợp với một nhu cầu khác nhau.</p><h2>Đặt theo khung giờ</h2><p>Phù hợp khi bạn chỉ cần vài giờ riêng tư để nghỉ ngơi hoặc thư giãn giữa lịch trình.</p><h2>Qua đêm</h2><p>Phù hợp khi bạn muốn nghỉ trọn buổi tối và bắt đầu ngày hôm sau thoải mái hơn.</p><p>Trang đặt phòng luôn hiển thị các gói đang mở của từng phòng để bạn so sánh trước khi gửi yêu cầu.</p>", now.AddDays(-5)),
                Blog(property.Id, "3-cach-chon-phong-phu-hop",
                    "3 cách chọn phòng phù hợp cho một kỳ nghỉ riêng tư",
                    "Ưu tiên mục đích chuyến đi, tiện nghi bạn thực sự dùng và ngân sách thay vì chỉ nhìn vào một bức ảnh đẹp.",
                    "<p>Phòng phù hợp không nhất thiết là phòng lớn nhất hoặc đắt nhất.</p><h2>1. Bắt đầu từ mục đích</h2><p>Một buổi nghỉ ngắn, một đêm hay vài ngày sẽ dẫn tới lựa chọn khác nhau.</p><h2>2. Chọn tiện nghi bạn thực sự cần</h2><p>Hãy xem phần tiện nghi và hình ảnh thật của từng phòng trước khi quyết định.</p><h2>3. So sánh giá theo gói</h2><p>Giá theo khung giờ và qua đêm có thể khác nhau; chọn gói đúng nhu cầu sẽ hợp lý hơn.</p>", now.AddDays(-2))
            );
        }

        if (!await db.GlobalEditorialShowcases.AnyAsync())
            db.GlobalEditorialShowcases.Add(new GlobalEditorialShowcase());

        if (!await db.PropertyGalleryItems.AnyAsync(x => x.PropertyId == property.Id) && !string.IsNullOrWhiteSpace(settings.CoverImageUrl))
        {
            db.PropertyGalleryItems.Add(new PropertyGalleryItem
            {
                PropertyId = property.Id,
                ImageUrl = settings.CoverImageUrl,
                AltText = $"Không gian tại {property.Name}",
                Caption = property.Name,
                SortOrder = 0,
                IsPublished = true
            });
        }

        await db.SaveChangesAsync();
    }

    private static HomeSection Section(Guid? propertyId, int sortOrder, string type, string name, string variant, object content) => new()
    {
        PropertyId = propertyId,
        SortOrder = sortOrder,
        Type = type,
        Name = name,
        Variant = variant,
        ContentJson = JsonSerializer.Serialize(content, JsonOptions),
        IsVisible = true
    };

    private static BlogPost Blog(Guid propertyId, string slug, string title, string excerpt, string bodyHtml, DateTime publishedAtUtc) => new()
    {
        PropertyId = propertyId,
        Slug = slug,
        Title = title,
        Excerpt = excerpt,
        BodyHtml = bodyHtml,
        IsPublished = true,
        PublishedAtUtc = publishedAtUtc
    };

    private static Room RoomSeed(string code, string name, int sortOrder, decimal dayPrice, decimal overnightPrice)
    {
        var room = new Room
        {
            Code = code,
            Name = name,
            Slug = code switch
            {
                "COCO-01" => "coco-blue-1",
                "ABAUS-02" => "abaus-2",
                "HONGKONG-03" => "hongkong-3",
                "MOON-04" => "moon-stone-4",
                "AMBER-05" => "amber-stay-5",
                "ROMAN-06" => "la-roman-6",
                _ => code.ToLowerInvariant()
            },
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
                EmailConfirmed = true,
                DisplayName = "De Long Admin",
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
            var roleResult = await userManager.AddToRoleAsync(user, "Admin");
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to assign Admin role: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
            }
        }

        if (!await db.UserPropertyAccesses.AnyAsync(x => x.UserId == user.Id && x.PropertyId == DeLongPropertyId))
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
