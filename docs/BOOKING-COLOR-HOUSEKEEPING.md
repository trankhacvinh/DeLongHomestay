# Màu lịch và lịch dọn phòng

## Quy tắc

- Màu booking tự động giữ nguyên logic hiện có. `bookings.display_color` null là tự động; giá trị trong palette là màu thủ công, chỉ phục vụ lịch nội bộ.
- PUT booking chỉ đổi màu khi `UpdateDisplayColor = true`. Client cũ không gửi trường này vẫn giữ màu thủ công. Chọn Tự động gửi null để khôi phục màu từ dữ liệu hiện tại.
- Không thay đổi giá, công nợ, trạng thái hoặc khoảng thời gian bởi thao tác màu. Quyền và antiforgery dùng API booking hiện có. Đơn đã đóng vẫn giữ giới hạn chỉnh sửa hiện có.
- Dọn phòng tìm lượt kế tiếp theo cùng room_id và giờ thực tế, bao gồm lượt đã hoàn tất để lịch quá khứ chính xác. Giữ giới hạn 4 giờ đã được chủ dự án xác nhận.
- Gộp nếu giờ dọn sau checkout không vượt giờ khách kế tiếp đến. Bỏ công việc chuẩn bị riêng của lượt đã gộp. Nếu xa hơn 4 giờ, tắt đèn sau checkout và giữ công việc chuẩn bị riêng.
- Mobile mặc định cuộn ngang, cột ngày sticky. Vừa màn hình là tùy chọn; không đổi giờ hoặc số ca.

## Triển khai

Migration `20261011130916_AddBookingDisplayColor` chỉ thêm cột varchar(7) nullable. Áp dụng trước khi chạy bản mới. Không sửa dữ liệu booking/payment cũ.

## Nghiệm thu

Kiểm thử PostgreSQL: màu lưu/đọc lại, giữ khi client không gửi màu, về tự động, chặn palette sai, giữ số tiền/trạng thái; dọn phòng gần/xa 4 giờ, khách kế tiếp đã hoàn tất, hủy, qua nửa đêm; hồi quy occupancy booking hoàn tất.
Kiểm thử JavaScript: override màu chỉ thay appearance, room block vẫn giữ màu khóa, thu tiền, zoom/fit.
Browser layout fixture: kiểm tra mobile/tablet/desktop; cần UAT đăng nhập và thiết bị thật trước khi xác nhận production.

## Kết quả kiểm tra source

- Build .NET thành công; 22 bài .NET/PostgreSQL liên quan đạt, không skip.
- 14 bài JavaScript lịch/thu tiền/zoom đạt; `node --check` cả hai script lịch đạt.
- Chrome layout fixture 390/768/1440px đạt: đủ 4 ca, cột ngày sticky, không tràn body; mobile/tablet cuộn ngang.
- Migration đã chạy trên database kiểm thử cục bộ. Chưa chạy production, chưa deploy, chưa UAT iPhone/Zalo.

## File thay đổi

- `Features/Housekeeping/HousekeepingService.cs`
- `Domain/Entities/Booking.cs`; `Features/Bookings/BookingContracts.cs`, `BookingService.cs`, `BookingDisplayColors.cs`
- `Features/Operations/AvailabilityIntervalService.cs`
- `Data/AppDbContext.cs`; `Data/Migrations/20261011130916_AddBookingDisplayColor.cs`, `.Designer.cs`, `AppDbContextModelSnapshot.cs`
- `Pages/Admin/Calendar.cshtml`, `CalendarV2.cshtml`; `Pages/Shared/_BookingDisplayColor.cshtml`
- `wwwroot/js/pages/admin-calendar.js`, `admin-calendar-v2.js`; `wwwroot/css/admin-calendar-v2.css`
- `tests/DeLong.Tests/Integration/HousekeepingScheduleIntegrationTests.cs`, `BookingDisplayColorTests.cs`
- `tests/javascript/admin-calendar-completed.test.cjs`, `admin-calendar-simplified.test.cjs`, `admin-calendar-zoom.test.cjs`
- `docs/BOOKING-COLOR-HOUSEKEEPING.md`, `docs/ROADMAP.md`

## Metadata ca phòng và trình bày văn bản lao công

- Việc dọn gộp để đón lượt tiếp theo lấy ca của lượt khách sắp nhận cùng phòng (giới hạn gộp 4 giờ giữ nguyên).
- Việc chuẩn bị riêng lấy ca của chính lượt khách; dọn trả phòng không gộp lấy ca vừa trả.
- Ca được đối chiếu với giờ thật và các mức giá/ca hiện có của từng phòng, theo thứ tự ca trên lịch; không dùng mốc giờ cố định chung.
- Khoảng qua đêm còn một phần vẫn thuộc ca qua đêm nếu nằm trong khoảng ca đó. Khoảng không khớp hoặc kéo dài qua nhiều ca có `shiftOrder = null`.
- Theo feedback mới, văn bản không chia theo metadata ca: mỗi ngày chỉ có một tiêu đề ngày, cách một dòng trống trước các công việc. Các việc cùng giờ nằm sát nhau; các mốc giờ khác nhau cách một dòng trống, sắp tăng dần theo giờ địa phương. Văn bản hiển thị và sao chép dùng chung `scheduleText`.
- Giữ metadata `shiftOrder` hiện có của API và logic gộp công việc/bật tắt đèn; không đổi dữ liệu booking, giá, payment hoặc schema.
