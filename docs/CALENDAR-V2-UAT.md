# Calendar V2 UAT

Calendar V2 giữ nguyên Calendar V1 nhưng là **một màn hình/menu riêng**. V1 dùng `/Admin/Calendar`; V2 dùng `/Admin/CalendarV2`. V2 xem từng phòng: ngày chạy dọc, các khung giờ/qua đêm chạy ngang. Availability được tính từ interval thời gian thực tế của booking.

## Admin

1. Mở sidebar Admin và xác nhận có hai mục riêng: **Lịch phòng V1** và **Lịch phòng V2**.
2. Mở **Lịch phòng V1**: phải vào `/Admin/Calendar`, giao diện tổng quan cũ không có switch V1/V2.
3. Mở **Lịch phòng V2**: phải vào `/Admin/CalendarV2`, tiêu đề `Lịch phòng theo khung giờ`; không hiển thị bảng tổng quan V1 và không có switch V1/V2 trong nội dung.
4. Ngay khi V2 mở, bên dưới tiêu đề phải hiện thanh chọn phòng `[‹] [Tên phòng] [›]`, thanh ngày và chú thích. Trong lúc gọi API phải có dòng `Đang tải lịch phòng…`; nếu bootstrap/API lỗi phải hiện thông báo lỗi ngay trong panel, **không được chỉ còn một card trắng**.
5. Regression quan trọng: `#calendar-page-data` nằm bên trong Vue root và có thể không còn trong DOM sau khi `admin-calendar.js` mount. V2 phải tìm cơ sở theo chuỗi fallback `JSON → Vue state → query propertyId → property switcher → property-scoped admin link`, không được phụ thuộc vào private Vue state duy nhất.
6. Nếu danh sách phòng cũng không còn từ JSON/Vue, V2 phải tự gọi `GET /api/admin/properties/{propertyId}/rooms/`. Có phòng active thì tên phòng đầu tiên phải xuất hiện và availability phải được tải; chỉ hiện `chưa có phòng` khi API phòng thực sự trả về không có phòng active.
7. `document.documentElement.dataset.calendarV2Property` phải chứa property GUID đã resolve. `document.documentElement.dataset.calendarV2` phải phản ánh `initial`, `room`, `request-error`, `rooms-request-error`... thay vì fail im lặng.
8. Hai placeholder V1 (`.calendar-toolbar-card`, `.calendar-wrap`) trên trang standalone V2 phải bị force-hide; CSS base không được làm chúng hiện thành card trắng.
9. V2 hiển thị một phòng tại một thời điểm; bấm `[‹]` / `[›]` để chuyển phòng.
10. Bấm `‹ 7 ngày`, `Hôm nay`, `7 ngày ›` để đổi khoảng ngày; trang không cần reload.
11. Chỉ các rate TimeSlot / Overnight tạo cột. Nightly không tạo cột riêng.
12. Với slot 12:00–15:00 và booking 12:00–14:00, pill occupied phải chiếm khoảng 2/3; phần còn lại ghi `Còn 14:00–15:00`.
13. Bấm phần occupied: mở modal chi tiết booking hiện có.
14. Bấm slot trống hoàn toàn: mở modal tạo booking với phòng/ngày/rate/giờ/giá đã điền sẵn.
15. Bấm phần free của slot partial: mở modal tạo booking với đúng khoảng giờ còn trống, rate để tùy chỉnh và giá chỉ là gợi ý.
16. Mở Calendar V2 ở tab A, thay đổi booking ở tab B: tab A tự cập nhật qua operations realtime; không F5.
17. Giữ một hold đến hết hạn: Calendar V2 đang mở tự nhả phần thời gian sau hold sweep.

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
