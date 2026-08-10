# UAT Checklist — DeLongHomestay

Dùng checklist này sau khi apply migrations/seed trên `delong_dev`.

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
