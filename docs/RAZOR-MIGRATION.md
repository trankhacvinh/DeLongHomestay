# Mapping Demo → Production Razor Pages

## Nguyên tắc

`demo/` là UI/UX specification. Không viết lại giao diện tùy tiện khi port production.

- Static HTML → Razor Page.
- `localStorage` mutation → Minimal API + Feature Service.
- `data.js` → PostgreSQL seed/migration.
- Giữ CSS class/token và interaction flow càng nhiều càng tốt.
- Vue 3 được dùng trực tiếp trong Razor markup bằng `v-on`, `v-model`, `v-if`, `v-for`, `v-bind`.
- Business rule quan trọng không nằm trong JavaScript.

## Mapping

| Demo | Production |
|---|---|
| `demo/index.html` | `Pages/Index.cshtml` |
| `demo/rooms.html` | `Pages/Rooms/Index.cshtml` |
| `demo/room-detail.html` | `Pages/Rooms/Detail.cshtml` |
| `demo/booking.html` | `Pages/Booking/Create.cshtml` |
| `demo/admin/calendar.html` | `Pages/Admin/Calendar.cshtml` |
| `store.addBooking()` | `Features/Bookings/BookingService.CreateAsync()` |
| `roomHasConflict()` | BookingService + PostgreSQL conflict guard |
| localStorage state | `AppDbContext` + PostgreSQL |

## Frontend pattern

```text
Razor initial render
   ↓
<script type="application/json">initial state</script>
   ↓
Vue page scope
   ↓ fetch
/api/admin/...
```

Không dùng SPA router. Chuyển trang lớn vẫn đi qua Razor Pages; CRUD/modal/inline actions đi qua API để tránh reload toàn trang.

Housekeeping Schedule là projection từ Booking, không lưu một bản lịch trùng lặp. Hai offset phút được lưu theo `Property`, cấu hình qua Minimal API và áp dụng server-side trước khi Razor/Vue hiển thị.

Hướng dẫn khách là nội dung hiện hành trên `Room`, được làm sạch server-side. Razor hiển thị ở trang thành công/tra cứu; PDF được sinh server-side. Endpoint tra cứu và PDF dùng cùng điều kiện chặn booking terminal để không làm lộ lịch sử lưu trú sau checkout.

## Production security

- ASP.NET Core Identity, không mang demo auth sang.
- Cookie HttpOnly/SameSite; HTTPS production.
- Antiforgery bắt buộc cho mutation API dùng cookie auth.
- Authorization theo role + property access.
- Password/connection string nằm trong User Secrets/environment, không commit Git.
## Báo cáo tình trạng phòng

- Trang `/Admin/Housekeeping` progressive-enhance bằng Vue và có tab `Báo cáo phòng` tối ưu cho điện thoại.
- Camera/multi-file input chỉ tạo preview phía client; server luôn giải mã, sửa orientation, resize và xuất WebP lại.
- Form upload dùng `DeLongApi.postForm`, antiforgery và kiểm tra quyền cơ sở phía server.
