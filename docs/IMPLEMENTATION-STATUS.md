# Implementation Status

Cập nhật cho branch production `agent/dotnet10-foundation`.

## Kiến trúc đã chốt

- .NET 10 ASP.NET Core Razor Pages.
- Một project production: `src/DeLong.Web`.
- Một project test: `tests/DeLong.Tests`.
- Vue 3 in-DOM progressive enhancement trong Razor (`v-on`, `v-model`, `v-bind`).
- Minimal APIs + shared `fetch`/antiforgery wrapper.
- EF Core/Npgsql + PostgreSQL.
- ASP.NET Core Identity + Role + `UserPropertyAccess`.
- Không Docker.

## Đã triển khai ở source

- Identity/login/logout và role nền.
- Multi-property data model + resolver/selector UI.
- Phòng + archive phòng.
- Preset RoomRate + trang Cấu hình thêm/sửa/ngừng khung giá.
- Khách hàng + normalized phone.
- Booking create/edit/status workflow.
- Lịch phòng 7 ngày, modal Vue, không reload cho mutation.
- C# conflict check + PostgreSQL exclusion constraint chống overlap.
- Payment ledger Receipt/Refund + void.
- Paid/Balance tính từ ledger.
- Housekeeping Clean/Dirty/Cleaning.
- Checkout tự chuyển phòng thành Dirty.
- Expense ledger + void.
- Finance snapshot/tháng.
- Management Reports.
- Generic AuditLog; Booking mutations có audit timeline.

## EF migrations đã commit

1. `InitialCreate`
2. `AddPayments`
3. `AddHousekeepingState`
4. `AddAuditAndExpenses`

## CI

Workflow `.NET` kiểm tra restore, Release build, xUnit tests và JavaScript syntax.

Workflow `PostgreSQL Integration` không dùng Docker. Nó khởi động PostgreSQL cài sẵn trên GitHub runner, apply migrations và kiểm tra database-level booking overlap constraint (`23P01`).

## Còn phải kiểm tra trên máy development

- Cài PostgreSQL trực tiếp trên máy.
- Tạo `delong_dev` và `delong_test`.
- Đặt connection strings/admin seed bằng .NET User Secrets.
- `dotnet ef database update`.
- Chạy ứng dụng và UAT trên browser desktop/mobile.

## Các phần tiếp theo

- Hoàn thiện dashboard vận hành.
- Hoàn thiện audit cho Payment/Expense và các mutation nhạy cảm khác.
- Public room catalog + availability + booking request.
- Import dữ liệu Excel cần thiết.
- UAT/go-live/backup/logging.
