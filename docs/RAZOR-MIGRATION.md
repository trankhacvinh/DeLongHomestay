# Mapping Demo → Production Razor Pages

## Public assets — 1.8.22

- Build/publish bằng .NET SDK tự sinh CSS bundle trang chủ qua `Build/PublicHomeCss.targets`; không sửa trực tiếp file sinh và không cần npm trên Ubuntu. Publish phải sao chép đủ `wwwroot`, gồm bundle và 4 font WOFF2.
- Razor vẫn render HTML đầu; Index chọn bundle qua ViewData, các trang khác giữ CSS riêng. Giữ hash asset, cookie/antiforgery và header HTML hiện có; không cache availability theo TTL dài.
- Không có migration cho thay đổi này. Rollback bản app/static assets đồng bộ, giữ schema/dữ liệu nghiệp vụ. Xem [ADR](ADR-PUBLIC-ASSET-BUILD.md).

## Kho và tiền mặt — 1.8.15

- Bản 1.8.16 cần thêm migration `20261028032715_AddInventoryImportReceipts` sau migration kho ban đầu, lưu kết quả import để retry an toàn. Không có data backfill. Xem [chi tiết](INVENTORY-IMPORT-COMPACT-2026-10-28.md).

- Hai Razor Pages `/Admin/Inventory`, `/Admin/Cash` render dữ liệu ban đầu ngoài Vue root; Vue chỉ quản lý phạm vi trang. API mutation có authorization, kiểm tra quyền chi nhánh/kho và antiforgery; không thêm dependency.
- Cần migration `20261027162525_AddSharedInventoryAndCash` trước khi chạy bản mới. Migration chỉ thêm schema, không tự khởi tạo kho hoặc nhập Excel. Admin khởi tạo kho và chọn chi nhánh quản lý trong giao diện.
- Bảng mới: `inventory_warehouses`, `inventory_units`, `inventory_items`, `inventory_documents`, `inventory_document_lines`, `cash_receipts`. Thêm 3 cột nullable cho `expenses`: `room_id`, `request_key`, `request_hash`. Khi rollback ứng dụng sau khi đã có nghiệp vụ mới, giữ schema/dữ liệu; không chạy Down để xóa bảng.
- Đã rehearsal trên PostgreSQL UTF-8 riêng: migrate schema cũ, chèn khoản chi cũ, migrate bản mới hai lần và xác nhận giữ tiền/phòng rỗng; không có pending model change. Production migration/deploy/UAT chưa thực hiện.

## Pay2S payment boundary

- Razor/Vue chỉ khởi tạo phiên và hiển thị/điều hướng trạng thái; Pay2S IPN là nguồn sự thật thanh toán.
- Hồ sơ Pay2S không dùng cấu hình chung rồi override. Mỗi `property_id` có đúng một hồ sơ; cơ sở muốn dùng chung thì tự nhập cùng thông tin.
- Migration `AddPay2SPayments` thêm `property_pay2_s_settings` và `pay2_s_payment_intents`; phải áp dụng trước khi bật Pay2S.
- Migration `AddPay2SSettlementGrace` bổ sung `release_at_utc`, mặc định đệm IPN 3 phút, dấu vết callback và quyết định xử lý `PaidAfterExpiry`.
- Migration `AddConsecutiveSlotBookings` thêm giá cả ngày trên `Room` và bảng snapshot `booking_rate_segments`. Phải áp dụng trước khi bật UI chọn nhiều khung.
- Migration `AddBookingEmailAndTelegramNotifications` thêm cấu hình Telegram/email hướng dẫn khách và hai outbox `notification_telegram_outbox`, `booking_guest_guide_emails`. Phải áp dụng trước khi bật các kênh thông báo mới.
- Migration `AddAutomaticBookingLifecycle` thêm hai công tắc tự động nhận/trả phòng trên `Property`, mặc định bật cho cả cơ sở hiện có và cơ sở mới.
- Migration `AddVoucherSystem` thêm voucher, sổ cái lượt sử dụng, outbox email voucher và mẫu email theo cơ sở. Phải áp dụng trước khi mở menu Voucher hoặc cho khách nhập mã.

## Nguyên tắc

- Phân quyền nội dung dùng policy `ManageMediaContent` cho `Admin`, `Manager`, `Media`; quyền này không bao gồm booking, giá, tài chính, cấu hình website hoặc phạm vi cơ sở ngoài `UserPropertyAccess`.

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
- Cài đặt mới truy cập `/setup` sau khi migration hoàn tất để tạo Admin đầu tiên. Setup dùng Razor form + antiforgery, tự khóa khi đã có Admin và chỉ seed credential rỗng cho các tích hợp ngoài.
## Báo cáo tình trạng phòng

- Trang độc lập `/Admin/RoomConditionReports` progressive-enhance bằng Vue, có bảng lọc/trạng thái/chi tiết và form mobile bottom-sheet; `/Admin/Housekeeping` chỉ liên kết sang chức năng này.
- Camera/multi-file input hỗ trợ ảnh và video, không giới hạn số lượng file nghiệp vụ. Ảnh chỉ tạo preview/tối ưu sơ bộ phía client; server luôn giải mã, sửa orientation, resize và xuất WebP lại. Video MP4/WebM/MOV được kiểm tra định dạng và giới hạn 250 MB mỗi file.
- Tag mẫu được chép trực tiếp vào nội dung có thể sửa; báo cáo bắt buộc đánh giá 1–5 sao và hiển thị rõ trạng thái xử lý trong bảng.
- Form upload dùng `DeLongApi.postForm`, antiforgery và kiểm tra quyền cơ sở phía server.

## AI Operating Foundation

- Customer/Staff/Admin AI dùng chung gateway và typed-tool catalog phía server.
- Admin AI mutation dùng typed proposal có preview trước/sau, stale check, apply một lần, batch rollback và audit riêng người yêu cầu/người duyệt.
- Cache câu trả lời có bản PostgreSQL để tồn tại qua restart; khóa gồm `propertyId`, audience, intent chuẩn hóa, tham số canonical và data version.
- Không lưu API key, CCCD, payment credential hoặc dữ liệu booking cá nhân trong knowledge snapshot/cache công khai.
- Migration `AddPublicAiBookingDrafts` thêm draft đặt phòng không chứa PII, TTL 30 phút và chỉ lưu SHA-256 của token công khai.
Staff/Manager/Housekeeping/Viewer dùng drawer `staff-ai-chat.js` và endpoint read-only `/staff-ai/chat`; Admin tiếp tục dùng drawer proposal riêng. Không dùng chung endpoint mutation của Admin cho vai trò vận hành.

## SePay

Áp dụng `20260915091623_AddSePayProvider` trước khi dùng bản có SePay. Razor `/payment/sepay` hiển thị QR; server webhook/API đối soát mới ghi Payment. Giữ callback Pay2s và Data Protection key ring. Không rollback schema khi đã có tiền SePay. Xem [SEPAY-PAYMENTS.md](SEPAY-PAYMENTS.md).

## Scheduled room closures (2026-10-09)

`20261009142839_AddRoomBookingSchedules` adds `room_booking_blocks` with property/room scoping, UTC boundaries, actor references, soft cancellation and an active-range index. Existing indefinite room locks and bookings/payments are unchanged. Rebuild the Linux migration bundle and apply before serving the new calendar/management page; see [ROOM-BOOKING-LOCK.md](ROOM-BOOKING-LOCK.md).

### Admin VND input formatting

RobinHerbots Inputmask 5.0.9 is vendored under `wwwroot/vendor/inputmask` with its MIT license. `DeLongMoneyInput` registers a Vue `moneyInput` directive per booking/calendar app; do not combine it with `v-model` on the same input. Bind numeric value and setter so grouped text (`250.000`) never enters API money fields as a string. No jQuery dependency or public-page loading.

## Setup website mới / demo thương hiệu (2026-10-12)

- Xem `docs/WINDOWS-DEMO-SETUP.md`. Production giữ AutoMigrate/SeedOnStartup=false; migrate riêng trước khi chạy.
- GET `/` khi chưa có Admin chuyển tới `/setup`; setup khóa sau commit, Admin được cấp quyền cơ sở mới.
- Không thay đổi schema. Cần nghiệm thu setup, booking/payment/report và mobile trên Windows trước khi bàn giao.

## Giãn lịch dọn phòng (2026-10-13)

- Cấu hình theo cơ sở: bật/tắt giãn lịch, thời lượng (mặc định 30 phút), số lao công (1), dọn sớm tối đa (30 phút), giờ bắt đầu/kết thúc làm việc theo múi giờ cơ sở. Mặc định tắt; cần nhập giờ làm việc trước khi bật.
- Chỉ việc chuẩn bị đón khách được chuyển sớm. Giữ quy tắc gộp 4 giờ và việc sau trả phòng. Không xếp dọn trong booking thật hoặc khoảng khóa đang hiệu lực; khóa đã hủy không chặn dọn.
- Ưu tiên việc cùng giờ ít linh hoạt hơn, tránh trùng người và cùng phòng, đảm bảo đủ thời lượng trước nhận khách. Nếu không xếp được: giữ giờ gốc, báo cần phân công thủ công trên lịch và văn bản sao chép. Các việc ngoài giờ làm vẫn giữ và báo; không tự xóa việc ban đêm.
- Migration `20261013013412_AddHousekeepingStaggerSettings` chỉ thêm cấu hình vào `properties`; không cập nhật booking/payment/khoảng khóa. Áp dụng migration trước deploy; bật tính năng sau khi xác nhận giờ làm việc.
- Nghiệm thu: phòng khóa kết thúc 10:00 có thể dọn 10:00; kết thúc 10:30 không được chuyển sớm; khách còn ở, hết giờ làm, deadline ngắn, hai lao công, qua ngày, cấu hình tắt, không mất/trùng công việc và sao chép cảnh báo.
