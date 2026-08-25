# Kiến trúc DeLongHomestay

## Customer identity và loyalty (2026-08-20)

- Tài khoản khách dùng ASP.NET Core Identity, tách khỏi nhân viên bằng `ApplicationUser.IsCustomerAccount` và role `Customer`.
- Khách đăng nhập bằng số điện thoại/mật khẩu hoặc mã TOTP Authenticator; nhân viên đăng nhập bằng username hoặc email và cũng có thể bật TOTP.
- `CustomerAccountLink` liên kết Identity user với `Customer` theo cơ sở, làm nguồn cho hồ sơ, lịch sử booking và điểm.
- CCCD vẫn mã hóa bằng `IdentityDocumentStorage`. Kho tài khoản dùng `(propertyId, userId)`; booking mới của khách đã đăng nhập nhận bản sao mã hóa tại `(propertyId, bookingId)`. API khách chỉ trả trạng thái có/không, không trả ảnh cũ.
- Điểm là sổ append-only `LoyaltyLedgerEntry`; booking hoàn tất nhận `floor(total / mức tiền cho 1 điểm)` đúng một lần. Công tắc theo cơ sở mặc định tắt.
- Đăng nhập quản trị là hai bước chuẩn: xác minh mật khẩu trước, sau đó mới yêu cầu TOTP nếu user đã bật 2FA. `Authentication:AdminEmergencyBypassTwoFactor` là break-glass chỉ dành cho role `Admin`, mặc định tắt, vẫn bắt buộc đúng mật khẩu, xóa lockout hiện tại và ghi log Critical khi sử dụng.

## Baseline production

DeLongHomestay là **modular monolith nhỏ**, cố ý chỉ dùng 1 production project và 1 test project.

```text
DeLongHomestay/
├── demo/
├── docs/
├── skills/
├── src/
│   └── DeLong.Web/
│       ├── Pages/
│       ├── Domain/
│       ├── Features/
│       ├── Data/
│       ├── Identity/
│       ├── Common/
│       └── wwwroot/
└── tests/
    └── DeLong.Tests/
        ├── Unit/
        └── Integration/
```

## Stack

- .NET 10 / ASP.NET Core Razor Pages.
- Vue 3 dùng in-DOM progressive enhancement, không SPA.
- Minimal APIs cho các mutation/CRUD không cần reload trang.
- EF Core + Npgsql + PostgreSQL.
- ASP.NET Core Identity + cookie authentication.
- Không Docker trong workflow phát triển của dự án.
- Không Alpine.js, Vue Router, Pinia hoặc Repository Pattern nếu chưa có nhu cầu thật.

## Request flow

```text
Browser
  ├── GET page -> Razor Page -> initial HTML + initial JSON
  └── interaction -> Vue -> fetch -> Minimal API -> Feature Service -> AppDbContext -> PostgreSQL
```

Razor Pages sở hữu navigation, auth, initial render và public pages. Vue chỉ sở hữu interaction trong từng page scope: modal, loading, filter, form reactive, toast, inline update.

## Vue convention trong .cshtml

Ưu tiên cú pháp đầy đủ để không xung đột ký tự `@` của Razor:

```html
<button v-on:click="save" v-bind:disabled="saving">Lưu</button>
<input v-model="form.name" />
<div v-if="modal.open">...</div>
```

Mỗi page có app scope nhỏ (`#rooms-page`, `#calendar-page`...), không mount một Vue app toàn website. Không dùng Vue Router/Pinia.

## API convention

- Prefix admin API: `/api/admin/...`.
- GET/POST/PUT/PATCH/DELETE đúng semantics.
- Error response dùng ProblemDetails.
- API authorization server-side; UI hide button không được xem là security.
- POST/PUT/PATCH/DELETE dùng antiforgery token gửi qua `X-CSRF-TOKEN`.
- `wwwroot/js/core/api.js` là wrapper fetch dùng chung.

## Data convention

- PostgreSQL schema/tên object dùng snake_case.
- Primary key dùng UUID; entity mới ưu tiên UUIDv7.
- Tiền dùng `decimal`, database `numeric(18,2)` hoặc precision phù hợp.
- Thời gian nghiệp vụ lưu UTC/`timestamptz`, UI hiển thị theo `Property.TimeZoneId` (`Asia/Ho_Chi_Minh` cho De Long).
- `property_id` có ngay từ đầu để hỗ trợ nhiều cơ sở.
- Payment, audit, housekeeping là entity riêng; không nhồi JSON vào booking.
- Thời điểm việc dọn phòng được suy ra từ giờ booking thật và hai offset theo cơ sở trên `Property`: số phút trước check-in và sau check-out; mặc định đều `0`.
- `Room.GuestGuideHtml` là nội dung hướng dẫn hiện hành của phòng, được soạn bằng editor và làm sạch server-side. Trang đặt thành công và tra cứu chỉ đọc hướng dẫn của phòng, không sao chép HTML vào booking.
- PDF hướng dẫn chỉ chứa mã đơn, tên phòng và nội dung hướng dẫn; không chứa dữ liệu cá nhân. Tra cứu và tải PDF bằng mã + số điện thoại bị từ chối khi booking đã ở trạng thái terminal (`Completed`, `Cancelled`, `NoShow`).

## Booking invariant (milestone tiếp theo)

- `Requested` không khóa phòng.
- `Held`, `Confirmed`, `CheckedIn` khóa khoảng thời gian.
- Conflict phải được kiểm tra ở service và bảo vệ ở PostgreSQL/transaction.
- Booking/payment/expense đã phát sinh không hard-delete; dùng cancel/void/archive + audit.
- Pay2S dùng `PropertyPay2SSettings` theo từng cơ sở; access/secret key được bảo vệ bằng ASP.NET Core Data Protection. `Pay2SPaymentIntent` là trạng thái kỹ thuật riêng, còn `Payment` chỉ được ghi khi IPN có chữ ký hợp lệ xác nhận tiền đã đến.
- Booking công khai thanh toán toàn bộ và được giữ bằng payment intent bền vững trong DB. `PaymentExpiresAt` đóng quyền mở/tạo thanh toán; `ReleaseAt` mặc định muộn hơn 3 phút để chờ IPN đang truyền về trước khi hủy booking và giải phóng phòng. IPN hợp lệ trong khoảng đệm vẫn xác nhận bình thường; IPN thành công sau `ReleaseAt` vẫn tạo `Payment`, đánh dấu `PaidAfterExpiry`, không tự giành lại phòng và tạo cảnh báo bắt buộc quản trị xử lý.
- Khoản `PaidAfterExpiry` chỉ được kết thúc bằng một quyết định có lưu dấu: hoàn tiền, chuyển phòng/thời gian khác, hoặc xác nhận thủ công sau khi server kiểm tra phòng còn trống.

## Phân quyền mục tiêu

Admin, Manager, Staff, Housekeeping, Viewer. Ngoài role còn có `UserPropertyAccess` để giới hạn cơ sở mà user được phép truy cập.
## Room condition reports

- `RoomConditionReport` is a property- and room-scoped operational record, separate from booking and housekeeping room status.
- `RoomConditionReportImage` lưu metadata cho cả ảnh và video. Ảnh được sửa orientation/resize/WebP qua `IRoomImageStorage`; video MP4/WebM/MOV được kiểm tra chữ ký file và lưu qua `IRoomConditionMediaStorage`.
- Mỗi báo cáo có `Rating` từ 1–5 sao, mức độ nghiệp vụ và trạng thái `New`/`InProgress`/`Resolved`; đây là chức năng quản trị độc lập tại `/Admin/RoomConditionReports`.
- `RoomConditionTag` contains reusable property-scoped presets. Each report stores the selected tag names as a JSON snapshot so historical wording does not change when presets are edited later.
- Staff mutations use the `ManageHousekeeping` policy, `PropertyAccessFilter`, antiforgery validation and server-side file validation.
