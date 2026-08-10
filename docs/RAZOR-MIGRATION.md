# Mapping Demo → ASP.NET Core Razor Pages

## Cấu trúc đề xuất

```text
src/DeLong.Web/
├── Pages/
│   ├── Index.cshtml
│   ├── Rooms/
│   │   ├── Index.cshtml
│   │   └── Detail.cshtml
│   ├── Booking/
│   │   ├── Create.cshtml
│   │   └── Success.cshtml
│   └── Admin/
│       ├── Index.cshtml
│       ├── Calendar.cshtml
│       ├── Bookings/
│       ├── Customers/
│       ├── Housekeeping/
│       ├── Finance/
│       ├── Reports/
│       └── Settings/
├── Application/
├── Domain/
├── Infrastructure/
└── wwwroot/
```

## Mapping file

| Demo | Razor Pages target |
|---|---|
| `demo/index.html` | `Pages/Index.cshtml` |
| `demo/rooms.html` | `Pages/Rooms/Index.cshtml` |
| `demo/room-detail.html` | `Pages/Rooms/Detail.cshtml` |
| `demo/booking.html` | `Pages/Booking/Create.cshtml` |
| `demo/admin/calendar.html` | `Pages/Admin/Calendar.cshtml` |
| `store.addBooking()` | `BookingService.CreateAsync()` |
| `roomHasConflict()` | domain query + transaction guard |
| localStorage state | PostgreSQL + EF Core/Npgsql |

## CSS/HTML

`demo/assets/css/styles.css` được xem là giao diện chuẩn để port vào `wwwroot/css`. Khi chuyển Razor, ưu tiên giữ class names để giảm rework.

## JavaScript

Giữ JavaScript UI nhỏ (modal/calendar interaction) nhưng dữ liệu phải lấy từ server. Không để business rule quan trọng chỉ ở client.

## Booking concurrency

Khi 2 nhân viên/khách tạo booking cùng lúc, production phải kiểm tra conflict trong transaction trước commit. Cần test race condition trước go-live.

## Authentication

Production dùng authentication server-side, cookie secure, authorization theo role/property access. Tài khoản demo không được mang sang production.
