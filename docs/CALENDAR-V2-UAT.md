# Calendar V2 UAT

Calendar V2 giữ nguyên Calendar V1 nhưng là **một màn hình/menu riêng**. V1 dùng `/Admin/Calendar`; V2 dùng `/Admin/CalendarV2`. V2 xem từng phòng: ngày chạy dọc, các khung giờ/qua đêm chạy ngang. Availability được tính từ interval thời gian thực tế của booking.

## Admin

1. Mở sidebar Admin và xác nhận có hai mục riêng: **Lịch phòng V1** và **Lịch phòng V2**.
2. Mở **Lịch phòng V1**: phải vào `/Admin/Calendar`, giao diện tổng quan cũ không có switch V1/V2.
3. Mở **Lịch phòng V2**: phải vào `/Admin/CalendarV2`, tiêu đề `Lịch phòng theo khung giờ`; không hiển thị bảng tổng quan V1 và không có switch V1/V2 trong nội dung.
4. Ngay khi V2 mở, bên dưới tiêu đề phải hiện thanh chọn phòng `[‹] [Tên phòng] [›]`, thanh ngày và bảng availability. Không được chỉ hiện một card trắng/trang trống. DevTools có thể kiểm tra `document.documentElement.dataset.calendarV2`: sau khi tải xong phải khác `initializing` và thường là `initial`.
5. V2 hiển thị một phòng tại một thời điểm với `[‹] [Phòng] [›]`; bấm hai nút để chuyển phòng.
6. Bấm `‹ 7 ngày`, `Hôm nay`, `7 ngày ›` để đổi khoảng ngày; trang không cần reload.
7. Chỉ các rate TimeSlot / Overnight tạo cột. Nightly không tạo cột riêng.
8. Với slot 12:00–15:00 và booking 12:00–14:00, pill occupied phải chiếm khoảng 2/3; phần còn lại ghi `Còn 14:00–15:00`.
9. Bấm phần occupied: mở modal chi tiết booking hiện có.
10. Bấm slot trống hoàn toàn: mở modal tạo booking với phòng/ngày/rate/giờ/giá đã điền sẵn.
11. Bấm phần free của slot partial: mở modal tạo booking với đúng khoảng giờ còn trống, rate để tùy chỉnh và giá chỉ là gợi ý.
12. Mở Calendar V2 ở tab A, thay đổi booking ở tab B: tab A tự cập nhật qua operations realtime; không F5.
13. Giữ một hold đến hết hạn: Calendar V2 đang mở tự nhả phần thời gian sau hold sweep.

## Public room detail

1. Mở trang chi tiết một phòng đã xuất bản.
2. Ngay sau phần `Giá & thời gian` phải có block `Lịch phòng · Chọn thời gian phù hợp`.
3. Cấu trúc ngày dọc / khung ngang giống logic của Admin V2.
4. Slot trống hoàn toàn bấm được; trạng thái chuyển thành `Đang chọn` và nút `Đặt khung này` mang đúng `date`, `room`, `rate` sang booking.
5. Slot partial chỉ cho xem phần giờ còn dư; không cho khách tự book khoảng linh hoạt.
6. Tạo/sửa/hủy booking ở Admin trong lúc trang phòng đang mở: public block tự refresh qua SSE; poll 15 giây là fallback.
7. Mở DevTools Network vào `/api/public/room-availability` và `/api/public/room-availability/stream`: payload không được có bookingId, tên khách, SĐT, email, guest details hay CCCD.

## Regression

- Calendar V1 tiếp tục drag/drop và hiển thị booking nhiều ngày như cũ.
- Menu V1 không bị active khi đang ở V2 và ngược lại.
- Modal booking trên V2 vẫn tải Email / Số khách / CCCD qua guest-details API.
- Admin V2 vẫn nghe operations realtime hiện có; notification SSE không bị dùng làm nguồn occupancy.
- Public booking / room detail cũ vẫn hoạt động khi endpoint availability tạm lỗi; người dùng vẫn có nút Đặt phòng thường.
