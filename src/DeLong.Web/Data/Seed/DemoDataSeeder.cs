using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Data.Seed;

public static class DemoDataSeeder
{
    public static void AddOperations(AppDbContext db, Property property, Guid adminId, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Demo clock must be UTC.", nameof(nowUtc));
        var day = DateTime.SpecifyKind(nowUtc.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
        var names = new[] { "Nguyễn An (mẫu)", "Trần Bình (mẫu)", "Lê Chi (mẫu)", "Phạm Dũng (mẫu)", "Hoàng Hà (mẫu)", "Vũ Linh (mẫu)" };
        var statuses = new[] { BookingStatus.Completed, BookingStatus.CheckedIn, BookingStatus.Confirmed,
            BookingStatus.Requested, BookingStatus.Held, BookingStatus.Cancelled };
        var rooms = property.Rooms.OrderBy(x => x.SortOrder).ToArray();
        for (var i = 0; i < rooms.Length; i++)
        {
            var customer = new Customer
            {
                PropertyId = property.Id, Name = names[i], Phone = $"00000000{i + 1:00}",
                NormalizedPhone = DeLong.Web.Features.Customers.CustomerService.NormalizePhone($"00000000{i + 1:00}"), Email = $"guest{i + 1}@example.invalid",
                Note = "Khách giả lập để demo, không dùng để liên lạc."
            };
            db.Customers.Add(customer);
            for (var n = 0; n < 5; n++)
            {
                var status = n == 0 ? statuses[i] : i == 5 && n == 1 ? BookingStatus.NoShow : n < 3 ? BookingStatus.Completed : BookingStatus.Confirmed;
                var start = n == 0 ? (i == 1 ? nowUtc.AddHours(-2) : day.AddDays(i == 0 ? -2 : i + 1).AddHours(14))
                    : day.AddDays(n < 3 ? -7 * n - i : 7 * n + i).AddHours(14);
                var end = n == 0 && i == 1 ? nowUtc.AddHours(4) : start.AddHours(19);
                var amount = rooms[i].FullDayPrice!.Value;
                var booking = new Booking
                {
                    PropertyId = property.Id, RoomId = rooms[i].Id, CustomerId = customer.Id,
                    Code = $"DEMO-{i + 1:00}-{n + 1:00}", Type = BookingType.MultiDay,
                    CheckInUtc = start, CheckOutUtc = end, Status = status,
                    RoomAmount = amount, UnitPrice = amount, NightCount = 1, RateName = "Lưu trú demo",
                    Source = n % 2 == 0 ? "Website" : "Walk-in", Note = "Dữ liệu giả lập; không phải giao dịch thực tế."
                };
                db.Bookings.Add(booking);
                db.BookingRateSegments.Add(new BookingRateSegment
                {
                    BookingId = booking.Id, ServiceDate = DateOnly.FromDateTime(start.AddHours(7)),
                    CheckInUtc = start, CheckOutUtc = end, RateName = "Lưu trú demo",
                    ListPrice = amount, AppliedAmount = amount, PricingRule = "demo-snapshot"
                });
                db.AuditLogs.Add(new AuditLog
                {
                    PropertyId = property.Id, EntityType = nameof(Booking), EntityId = booking.Id,
                    Action = "DemoSeed", ActorUserId = adminId,
                    AfterJson = System.Text.Json.JsonSerializer.Serialize(new { booking.Code, booking.Status, booking.TotalAmount })
                });
                if (status == BookingStatus.Cancelled)
                {
                    foreach (var type in new[] { PaymentType.Receipt, PaymentType.Refund })
                        db.Payments.Add(new Payment
                        {
                            PropertyId = property.Id, BookingId = booking.Id, Type = type, Amount = amount / 2,
                            Method = PaymentMethod.BankTransfer, OccurredAtUtc = nowUtc,
                            RecordedByUserId = adminId, Reference = booking.Code, Note = "Cọc và hoàn cọc mẫu."
                        });
                }
                if (status is BookingStatus.Completed or BookingStatus.CheckedIn or BookingStatus.Confirmed)
                    db.Payments.Add(new Payment
                    {
                        PropertyId = property.Id, BookingId = booking.Id,
                        Amount = status == BookingStatus.Completed ? amount : amount / 2,
                        Method = n % 2 == 0 ? PaymentMethod.Cash : PaymentMethod.BankTransfer,
                        OccurredAtUtc = start < nowUtc ? start : nowUtc, RecordedByUserId = adminId,
                        Reference = booking.Code, Note = "Thu mẫu để trình diễn báo cáo."
                    });
            }
            rooms[i].HousekeepingStatus = (HousekeepingStatus)(i % 3);
            rooms[i].HousekeepingUpdatedAtUtc = nowUtc;
            rooms[i].HousekeepingUpdatedByUserId = adminId;
        }
        var categories = new[] { "Điện nước", "Giặt ủi", "Vật tư phòng", "Vệ sinh", "Bảo trì", "Marketing" };
        for (var i = 0; i < categories.Length; i++)
            db.Expenses.Add(new Expense
            {
                PropertyId = property.Id, Category = categories[i], Description = $"Chi phí {categories[i]} mẫu",
                Amount = 150_000m + i * 75_000m, OccurredAtUtc = nowUtc.AddDays(-i),
                Method = PaymentMethod.Cash, Vendor = "Nhà cung cấp mẫu", RecordedByUserId = adminId,
                Reference = $"DEMO-EXP-{i + 1}", Note = "Dữ liệu giả lập."
            });
        db.Vouchers.Add(new Voucher
        {
            PropertyId = property.Id, Code = "WELCOME10", NormalizedCode = "WELCOME10",
            Description = "Ưu đãi demo 10%", DiscountPercent = 10,
            AppliesTo = VoucherApplicability.TimeSlot | VoucherApplicability.Overnight | VoucherApplicability.FullDay,
            StartsAtUtc = day.AddDays(-1), EndsAtUtc = day.AddDays(60), Status = VoucherStatus.Active,
            TotalUsageLimit = 100, PerCustomerUsageLimit = 1, CreatedByUserId = adminId
        });
    }
}
