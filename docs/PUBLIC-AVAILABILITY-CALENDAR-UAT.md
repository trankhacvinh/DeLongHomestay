# Public Availability Calendar V2 UAT

Khối CMS `AvailabilityCalendar` dùng chung API interval `/api/public/room-availability`; không tạo lịch hoặc nguồn booking thứ hai và không thay đổi schema dữ liệu.

1. Trong CMS hoặc Visual Editor, thêm khối **Lịch phòng trống V2**, đặt số ngày từ `1–14`, lưu và tải lại trang public.
2. Chuyển phòng bằng nút trước/sau; tên phòng, cơ sở, mã phòng và lịch phải đổi đồng bộ.
3. Đổi ngày bắt đầu, dùng Hôm nay hoặc tiến/lùi 7 ngày; các ngày và khung giờ phải tải lại đúng.
4. Khung `available` cho phép bấm; `partial` và `occupied` chỉ hiển thị trạng thái, không mở form đặt nguyên khung.
5. Bấm khung trống mở modal chứa đúng form booking hiện tại với ngày, phòng và rate đã chọn.
6. Modal không đóng khi bấm backdrop hoặc nhấn Escape; chỉ nút `×` đóng. Trên mobile modal chiếm toàn màn hình.
7. Trong modal, đăng nhập tài khoản khách và xác nhận tên/email/CCCD đã có tiếp tục dùng đúng hành vi của trang booking chính.
8. Response public không được chứa booking id, tên khách, điện thoại, email, ghi chú hay ảnh CCCD.
9. Editor **Hướng dẫn dành cho khách** có nút Mở rộng editor riêng; nhấn Escape hoặc Thu gọn trả giao diện về trạng thái ban đầu.
