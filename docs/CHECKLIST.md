# Checklist nghiệm thu

## Foundation

- [x] `dotnet restore` pass trên GitHub Actions.
- [x] `dotnet build -c Release` pass trên GitHub Actions.
- [x] `dotnet test -c Release` pass trên GitHub Actions.
- [x] JavaScript page modules được `node --check` trong CI.
- [ ] PostgreSQL local có database `delong_dev`.
- [ ] Connection string thật nằm trong User Secrets, không nằm trong Git.
- [x] Initial migration được EF Core sinh và commit.
- [x] Migration có `btree_gist` + exclusion constraint chống overlap booking.
- [ ] Apply InitialCreate thành công trên PostgreSQL local.
- [ ] Seed tạo De Long + 6 phòng đúng rates.
- [ ] Seed admin từ User Secrets.
- [ ] Login/logout Identity hoạt động với PostgreSQL local.
- [x] API kiểm tra role + `UserPropertyAccess`.

## Vue/API mẫu Rooms

- [ ] Trang `/Admin/Rooms` render initial data từ PostgreSQL local.
- [x] Vue mount trong page scope, không SPA.
- [x] Search/filter không reload.
- [x] Thêm phòng dùng modal + POST API.
- [x] Sửa phòng dùng modal + PUT API.
- [x] Ngừng phòng dùng confirm modal + DELETE API.
- [x] Mutation API dùng antiforgery token.
- [x] API validation dùng ProblemDetails.
- [ ] Kiểm tra trực tiếp loading state/toast trên browser với PostgreSQL local.

## Customer + Booking

- [x] Customer entity/service/API.
- [x] Nhận diện khách cũ theo normalized phone trong cùng cơ sở.
- [x] Booking status/rules.
- [x] Create booking qua API.
- [x] C# conflict check.
- [x] PostgreSQL overlap guard cho Held/Confirmed/CheckedIn.
- [x] Conflict API trả `409 ProblemDetails`.
- [x] Calendar Razor + Vue đọc dữ liệu server.
- [x] Click ô trống mở modal tạo booking.
- [x] Preset rate tự điền giờ/giá nhưng cho sửa giờ thực tế.
- [x] Booking detail/status actions không reload.
- [x] Trang Booking search/filter.
- [x] Trang Customers add/edit modal.
- [ ] Edit booking room/time/amount.
- [ ] Audit log booking.
- [ ] Integration test race condition trên `delong_test`.
- [ ] Kiểm tra calendar trực tiếp trên browser với PostgreSQL local.

## Trước Payments milestone

- [ ] CurrentProperty resolver/selector thay seed property ID trong các PageModel.
- [ ] Chốt logic thanh toán/cọc/hoàn tiền.
- [ ] Thêm Payment ledger thay vì số tiền thanh toán nằm trong Booking.
- [ ] Check-out tự tạo housekeeping task/trạng thái Bẩn.
- [ ] Áp dụng migration `AddRoomConditionReports` trước khi mở chức năng báo cáo phòng.
- [ ] Kiểm tra nhân viên chỉ thấy/tạo báo cáo tại cơ sở được cấp quyền.
- [ ] Chụp trực tiếp và chọn nhiều ảnh trên iPhone/Android; xác nhận ảnh xoay đúng và WebP tải nhanh.
- [ ] Xác nhận báo cáo cần 1-12 ảnh và không mất nội dung khi server trả lỗi validation.
