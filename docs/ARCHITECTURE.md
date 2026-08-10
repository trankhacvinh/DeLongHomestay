# Kiến trúc DeLongHomestay

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

## Booking invariant (milestone tiếp theo)

- `Requested` không khóa phòng.
- `Held`, `Confirmed`, `CheckedIn` khóa khoảng thời gian.
- Conflict phải được kiểm tra ở service và bảo vệ ở PostgreSQL/transaction.
- Booking/payment/expense đã phát sinh không hard-delete; dùng cancel/void/archive + audit.

## Phân quyền mục tiêu

Admin, Manager, Staff, Housekeeping, Viewer. Ngoài role còn có `UserPropertyAccess` để giới hạn cơ sở mà user được phép truy cập.
