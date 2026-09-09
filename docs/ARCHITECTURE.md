# Kiến trúc DeLongHomestay

## Pricing authority

`PricingService` là nguồn tính giá server-side cho booking khung giờ và xem trước voucher. Lịch public chỉ ước tính để phản hồi nhanh; khi gửi form, server đọc lại giá, ngày đặc biệt và quyền đặt trước khi khóa phòng. Client không được quyết định tổng tiền.

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
- Thông báo booking nội bộ dùng outbox bền vững cho email và Telegram, retry ở background worker; lỗi kênh ngoài không rollback booking. Danh sách email, Telegram bot token và chat ID cấu hình theo cơ sở; bot token được bảo vệ bằng ASP.NET Core Data Protection.
- Email gửi khách có outbox/audit riêng `BookingGuestGuideEmail`. Hệ thống có thể tự xếp hàng hướng dẫn khi booking chuyển `Confirmed`, gửi thông báo khi booking chuyển `Cancelled`, hiển thị trạng thái check-in trong chi tiết booking/lịch và cho phép nhân viên gửi lại. Tiêu đề/nội dung HTML mẫu được cấu hình theo cơ sở bằng tập biến cho phép; HTML được làm sạch server-side, outbox giữ cả snapshot HTML và text fallback để lịch sử không đổi theo mẫu mới.

## Booking invariant (milestone tiếp theo)

- `Requested` không khóa phòng.
- `Held`, `Confirmed`, `CheckedIn` khóa khoảng thời gian.
- Conflict phải được kiểm tra ở service và bảo vệ ở PostgreSQL/transaction.
- Booking/payment/expense đã phát sinh không hard-delete; dùng cancel/void/archive + audit.
- Pay2S dùng `PropertyPay2SSettings` theo từng cơ sở; access/secret key được bảo vệ bằng ASP.NET Core Data Protection. `Pay2SPaymentIntent` là trạng thái kỹ thuật riêng, còn `Payment` chỉ được ghi khi IPN có chữ ký hợp lệ xác nhận tiền đã đến.
- Booking công khai thanh toán toàn bộ và được giữ bằng payment intent bền vững trong DB. `PaymentExpiresAt` đóng quyền mở/tạo thanh toán; `ReleaseAt` mặc định muộn hơn 3 phút để chờ IPN đang truyền về trước khi hủy booking và giải phóng phòng. IPN hợp lệ trong khoảng đệm vẫn xác nhận bình thường; IPN thành công sau `ReleaseAt` vẫn tạo `Payment`, đánh dấu `PaidAfterExpiry`, không tự giành lại phòng và tạo cảnh báo bắt buộc quản trị xử lý.
- Public time-slot booking cho chọn một chuỗi khung liền nhau theo thứ tự cấu hình, chỉ mở rộng về phía sau và có giới hạn số ngày riêng. Server tự xác thực lại chuỗi, giá cả ngày và mức giảm nhiều khung; `BookingRateSegment` giữ snapshot từng khung và số tiền áp dụng.
- Voucher là aggregate riêng theo cơ sở gồm `Voucher`, sổ cái `VoucherRedemption` và outbox `VoucherEmailDelivery`. Giá nhiều khung/cả ngày được tính trước voucher; voucher chỉ giảm `Booking.RoomAmount`, còn `Booking.DiscountAmount` giữ số tiền giảm snapshot. Khi booking chờ Pay2S, lượt được khóa bằng `SELECT ... FOR UPDATE` và ghi `Reserved` trong cùng transaction; thành công thành `Redeemed`, hủy/chưa trả hết hạn thành `Released`. Booking đã trả chỉ được hoàn lượt thủ công có lý do và audit. Voucher 100% xác nhận trực tiếp, không tạo payment giả.
- Khi khách gửi form, booking được tạo thẳng ở trạng thái `Held`. PostgreSQL exclusion constraint quyết định duy nhất request thắng trong tình huống đồng thời; chỉ booking đã giữ thành công mới được tạo Pay2S intent. Không còn marker giữ phòng 3 phút trước thanh toán.
- Booking từ 3 khách trở lên bắt buộc hai bộ CCCD trước/sau. Bộ thứ nhất có thể lấy từ tài khoản; bộ thứ hai chỉ thuộc booking và không ghi đè kho CCCD tài khoản.
- Khoản `PaidAfterExpiry` chỉ được kết thúc bằng một quyết định có lưu dấu: hoàn tiền, chuyển phòng/thời gian khác, hoặc xác nhận thủ công sau khi server kiểm tra phòng còn trống.

## Phân quyền mục tiêu

Admin, Manager, Staff, Housekeeping, Viewer. Ngoài role còn có `UserPropertyAccess` để giới hạn cơ sở mà user được phép truy cập.
## Room condition reports

- `RoomConditionReport` is a property- and room-scoped operational record, separate from booking and housekeeping room status.
- `RoomConditionReportImage` lưu metadata cho cả ảnh và video. Ảnh được sửa orientation/resize/WebP qua `IRoomImageStorage`; video MP4/WebM/MOV được kiểm tra chữ ký file và lưu qua `IRoomConditionMediaStorage`.
- Mỗi báo cáo có `Rating` từ 1–5 sao, mức độ nghiệp vụ và trạng thái `New`/`InProgress`/`Resolved`; đây là chức năng quản trị độc lập tại `/Admin/RoomConditionReports`.
- `RoomConditionTag` contains reusable property-scoped presets. Each report stores the selected tag names as a JSON snapshot so historical wording does not change when presets are edited later.
- Staff mutations use the `ManageHousekeeping` policy, `PropertyAccessFilter`, antiforgery validation and server-side file validation.
# Admin AI assistant

- `Features/AdminAi` tích hợp OpenAI Responses API và Google Gemini `generateContent` qua `HttpClient`.
- API key được mã hóa bằng ASP.NET Core Data Protection, tách theo cơ sở và không trả lại trình duyệt.
- Chỉ role `Admin` được truy cập; mọi endpoint tiếp tục kiểm tra phạm vi cơ sở, antiforgery và rate limit.
- AI chỉ nhận snapshot vận hành đã lọc, không có DbContext/SQL/CCCD/secrets. Mutation chỉ tạo `AiChangeProposal`; Admin phải xác nhận trước khi service nghiệp vụ thực thi trong transaction.
- Allowlist mutation hiện tại: tạo phòng kèm khung giá, tạo voucher, tạo ngày đặc biệt và cập nhật cấu hình combo/cuối tuần.
- Hội thoại và tin nhắn được lưu theo `(propertyId, userId)` để Admin xem lại lịch sử; tệp đính kèm chỉ thuộc đúng hội thoại/người tải và không có URL công khai.
- AI chỉ nhận DOCX, PDF, JPG, PNG và WEBP đã qua kiểm tra chữ ký/kích thước. DOCX được trích văn bản với XML DTD bị cấm; PDF và ảnh đi qua input đa phương thức của provider. Nội dung tệp luôn được coi là dữ liệu không tin cậy, không phải system instruction.
- `AiUsageRecord` giữ token thật từ provider và snapshot chi phí USD ước tính theo đơn giá input/output do Admin cấu hình. Ngân sách tháng chặn lượt gọi mới khi đạt hạn mức; không được gọi số tiền này là hóa đơn chính xác của provider.
