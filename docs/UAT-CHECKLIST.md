# UAT Checklist — DeLongHomestay

Dùng checklist này sau khi apply migrations/seed trên `delong_dev`.

## Tài khoản khách và tích điểm

- [ ] Đăng ký bằng số điện thoại/mật khẩu và đồng ý đúng phiên bản điều khoản.
- [ ] Đăng nhập bằng mật khẩu và TOTP; nhập sai phải tăng bộ đếm khóa.
- [ ] Tải CCCD hai mặt, tải lại hồ sơ và chỉ thấy trạng thái “Đã có”.
- [ ] Đặt phòng khi đã đăng nhập và xác nhận quản trị đọc được bản CCCD mã hóa của booking.
- [ ] Tắt tích điểm thì booking hoàn tất không tạo điểm; bật lên thì booking kế tiếp chỉ tạo đúng một dòng điểm, làm tròn xuống đúng cấu hình.
- [ ] Nhân viên đăng nhập bằng username hoặc email; tài khoản khách không vào được trang quản trị.
- [ ] User chưa bật Authenticator chỉ thấy username/email và mật khẩu; user đã bật chỉ thấy bước nhập TOTP sau khi mật khẩu đúng.
- [ ] Bật tạm `Authentication:AdminEmergencyBypassTwoFactor=true`: chỉ role Admin có mật khẩu đúng bỏ qua được TOTP và hệ thống ghi log Critical; tắt lại ngay sau khôi phục.

## 1. Login / quyền

- [ ] Admin login/logout thành công.
- [ ] Manager không truy cập chức năng chỉ dành Admin (nếu có).
- [ ] Staff không thấy trang Thu chi/Báo cáo.
- [ ] Housekeeping chỉ thao tác dọn phòng, không sửa booking/finance.
- [ ] Viewer xem Thu chi/Báo cáo nhưng không ghi chi phí.
- [ ] User không thể đổi `propertyId` URL/API sang cơ sở không được cấp quyền.
- [ ] Nếu user có từ 2 cơ sở, selector cơ sở xuất hiện và mọi menu giữ đúng `propertyId`.

## 2. Phòng / khung giá

- [ ] Có đúng 6 phòng seed của De Long.
- [ ] Giá/khung giờ đúng dữ liệu đã chuẩn hóa.
- [ ] Thêm khung giá bằng modal, không reload.
- [ ] Sửa giá/giờ bằng modal.
- [ ] End <= Start tự thành khung qua đêm.
- [ ] Ngừng khung giá không xóa lịch sử.
- [ ] Calendar không còn dùng khung giá đã ngừng.
- [ ] Soạn hướng dẫn phòng có tiêu đề, danh sách và liên kết; lưu lại không còn thẻ/script nguy hiểm.

## 3. Khách hàng

- [ ] Thêm khách hàng.
- [ ] Sửa khách hàng.
- [ ] Số điện thoại được normalize.
- [ ] Booking với số điện thoại cũ dùng lại customer thay vì tạo trùng.

## 4. Booking / Calendar

- [ ] Calendar hiển thị 7 ngày đúng timezone Việt Nam.
- [ ] Click ô trống mở modal booking.
- [ ] Chọn preset tự điền giờ/giá.
- [ ] Sửa giờ thực tế khác preset vẫn lưu được.
- [ ] Tạo booking Held.
- [ ] Tạo booking Confirmed.
- [ ] Requested không khóa phòng.
- [ ] Held/Confirmed/CheckedIn khóa phòng.
- [ ] Booking chồng giờ trả 409 với thông báo dễ hiểu.
- [ ] Hai request đồng thời không thể tạo booking chồng giờ (DB guard).
- [ ] Sửa phòng/giờ booking có conflict protection.
- [ ] Requested → Held/Confirmed/Cancelled đúng.
- [ ] Held → Confirmed/Cancelled đúng.
- [ ] Confirmed → CheckedIn/Cancelled/NoShow đúng.
- [ ] CheckedIn → Completed đúng.
- [ ] Booking terminal không sửa giờ/phòng được.
- [ ] Audit timeline ghi Created/Updated/StatusChanged và actor.
- [ ] Trang đặt thành công hiện đúng hướng dẫn của phòng và tải được PDF mở hợp lệ.
- [ ] Tra cứu đúng mã + SĐT hiện hướng dẫn và tải được PDF.
- [ ] Booking `Completed`, `Cancelled` hoặc `NoShow` không còn tra cứu hay tải PDF được.

## 5. Thanh toán

- [ ] Ghi cọc/thu tiền bằng modal, không reload.
- [ ] PaidAmount cập nhật ngay.
- [ ] BalanceAmount cập nhật ngay.
- [ ] Refund không vượt quá net paid.
- [ ] Void yêu cầu lý do.
- [ ] Void giữ giao dịch trong lịch sử.
- [ ] Không void Receipt nếu làm net paid âm do Refund đã tồn tại.

## 6. Checkout / Dọn phòng

- [ ] Check-out Completed tự đổi phòng sang Dirty.
- [ ] Phòng xuất hiện ở cột Bẩn.
- [ ] Dirty → Cleaning.
- [ ] Cleaning → Clean.
- [ ] Có thể sửa lại Cleaning → Dirty nếu chưa đạt.
- [ ] Housekeeping update không reload trang.
- [ ] Lịch công việc lấy đúng giờ nhận/trả thực tế của booking đã giữ/xác nhận/đang ở.
- [ ] Booking hủy/không đến không xuất hiện trong lịch dọn phòng.
- [ ] Chế độ Văn bản sắp theo ngày → giờ → phòng và sao chép được để gửi lao công.
- [ ] Offset dọn trước check-in/sau check-out mặc định 0, lưu theo cơ sở và áp dụng đúng khi đổi ngày.

## 7. Thu chi

- [ ] Thu khách = Receipt hợp lệ trong tháng.
- [ ] Hoàn tiền = Refund hợp lệ.
- [ ] Chi phí không tính khoản đã void.
- [ ] Dòng tiền ròng = Thu - Hoàn - Chi.
- [ ] Ghi chi phí bằng modal.
- [ ] Void chi phí yêu cầu lý do và giữ lịch sử.
- [ ] Điều hướng tháng giữ đúng cơ sở.

## 8. Báo cáo

- [ ] Giá trị booking không bị nhầm với tiền thực thu.
- [ ] Booked hours hợp lý.
- [ ] Net receipts khớp Payment ledger.
- [ ] Expenses khớp Expense ledger.
- [ ] Outstanding khớp tổng balance booking chưa hủy/no-show.
- [ ] Phân tích theo phòng/nguồn đúng.
- [ ] Trend 6 tháng hiển thị đúng timezone/property.

## 9. Mobile

- [ ] Sidebar/topbar usable trên màn hình nhỏ.
- [ ] Calendar có thể cuộn ngang rõ ràng.
- [ ] Modal booking/payment/expense không tràn màn hình.
- [ ] Housekeeping board chuyển thành một cột.
- [ ] Buttons đủ lớn để thao tác cảm ứng.

## 10. Trước merge/go-live

- [ ] `dotnet build -c Release` pass.
- [ ] `dotnet test` pass.
- [ ] PostgreSQL integration test pass.
- [ ] Không có password/connection string thật trong Git.
- [ ] Backup/restore thử thành công.
- [ ] UAT trên dữ liệu clone thực tế.
