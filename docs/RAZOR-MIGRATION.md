# Mapping Demo → Production Razor Pages

## Pay2S payment boundary

- Razor/Vue chỉ khởi tạo phiên và hiển thị/điều hướng trạng thái; Pay2S IPN là nguồn sự thật thanh toán.
- Hồ sơ Pay2S không dùng cấu hình chung rồi override. Mỗi `property_id` có đúng một hồ sơ; cơ sở muốn dùng chung thì tự nhập cùng thông tin.
- Migration `AddPay2SPayments` thêm `property_pay2_s_settings` và `pay2_s_payment_intents`; phải áp dụng trước khi bật Pay2S.
- Migration `AddPay2SSettlementGrace` bổ sung `release_at_utc`, mặc định đệm IPN 3 phút, dấu vết callback và quyết định xử lý `PaidAfterExpiry`.
- Migration `AddConsecutiveSlotBookings` thêm giá cả ngày trên `Room` và bảng snapshot `booking_rate_segments`. Phải áp dụng trước khi bật UI chọn nhiều khung.
- Migration `AddBookingEmailAndTelegramNotifications` thêm cấu hình Telegram/email hướng dẫn khách và hai outbox `notification_telegram_outbox`, `booking_guest_guide_emails`. Phải áp dụng trước khi bật các kênh thông báo mới.

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

- Trang độc lập `/Admin/RoomConditionReports` progressive-enhance bằng Vue, có bảng lọc/trạng thái/chi tiết và form mobile bottom-sheet; `/Admin/Housekeeping` chỉ liên kết sang chức năng này.
- Camera/multi-file input hỗ trợ ảnh và video, không giới hạn số lượng file nghiệp vụ. Ảnh chỉ tạo preview/tối ưu sơ bộ phía client; server luôn giải mã, sửa orientation, resize và xuất WebP lại. Video MP4/WebM/MOV được kiểm tra định dạng và giới hạn 250 MB mỗi file.
- Tag mẫu được chép trực tiếp vào nội dung có thể sửa; báo cáo bắt buộc đánh giá 1–5 sao và hiển thị rõ trạng thái xử lý trong bảng.
- Form upload dùng `DeLongApi.postForm`, antiforgery và kiểm tra quyền cơ sở phía server.
