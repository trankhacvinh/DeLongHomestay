# Housekeeping Schedule V2 UAT

## Nguồn dữ liệu

1. Mở `/Admin/Housekeeping` và chuyển sang **Lịch công việc**.
2. Chọn một ngày có booking Held/Confirmed/CheckedIn: lịch phải dùng đúng giờ nhận/trả thực tế, không dùng giờ preset cứng.
3. Booking Requested, Cancelled và NoShow không tạo việc.
4. Booking Completed vẫn tạo việc **Sau trả phòng** tại giờ checkout.
5. Nếu cùng phòng có khách kế tiếp trong 4 giờ và giờ dọn sau trả phòng không muộn hơn giờ nhận khách mới, gộp thành **Dọn phòng + Mở đèn** tại giờ dọn sau trả phòng; bỏ mục chuẩn bị riêng của khách tiếp theo. Khách đến muộn hơn vẫn có mục mở đèn riêng. Áp dụng cả khi khách tiếp theo nhận vào ngày hôm sau.
6. Đổi giờ, đổi phòng hoặc hủy booking ở tab khác: lịch đang mở tự cập nhật qua operations realtime; poll 15 giây là fallback.
7. Trong **Cấu hình → Thời điểm dọn phòng**, để cả hai giá trị `0`: giờ công việc phải bằng đúng giờ nhận/trả thực tế.
8. Đặt dọn trước nhận phòng `30` phút: booking nhận lúc 14:00 phải sinh việc lúc 13:30, kể cả khi việc chuyển sang ngày hôm trước.
9. Đặt dọn sau trả phòng `15` phút: booking trả lúc 14:00 phải sinh việc lúc 14:15, kể cả khi việc chuyển sang ngày hôm sau.
10. API và database từ chối giá trị nhỏ hơn 0 hoặc lớn hơn 1440.

## Chế độ xem

1. **Tình trạng phòng** giữ nguyên board Sạch/Bẩn/Đang dọn hiện có.
2. **Lịch công việc** sắp việc theo giờ rồi thứ tự phòng; nút **Bắt đầu dọn** chuyển phòng sang Đang dọn.
3. Bộ lọc **Chuẩn bị đón khách** chỉ hiện dòng mở đèn trước check-in.
4. Bộ lọc **Sau trả phòng** hiện dòng dọn/tắt đèn và dòng gộp dọn phòng + mở đèn sau checkout.
5. Khi mới mở trang, **Tất cả** là bộ lọc mặc định và hiển thị cả hai loại.
6. Booking nhận và trả trong cùng ngày có việc chuẩn bị và sau trả phòng; nếu chuẩn bị đã gộp với lần trả phòng trước thì không tạo việc chuẩn bị trùng. Giờ hiển thị vẫn theo offset cấu hình.

## Văn bản

1. Chọn **Văn bản** và xác nhận định dạng:

   ```text
   16/8
   09:30 Dọn phòng Phòng số 1 + Mở đèn
   10:00 dọn phòng Phòng số 2 mở đèn
   ```

2. Khi chuyển sang **Văn bản**, bộ lọc tự về **Tất cả** để nội dung gửi lao công không thiếu việc; sau đó vẫn có thể chủ động chọn lại từng loại.
3. Bấm **Sao chép văn bản**, dán vào Zalo/ghi chú và xác nhận xuống dòng được giữ nguyên.
4. Khi không có việc, không sinh văn bản rỗng khó hiểu mà phải hiện trạng thái không có công việc.
5. Kiểm tra desktop và mobile; thanh ngày, bộ lọc, danh sách và textarea không tràn màn hình.
6. Khách cũ trả 09:30, khách mới nhận 10:30, offset trước nhận 30 phút/sau trả 0 phút: chỉ có dòng 09:30 **Dọn phòng + Mở đèn**, không có dòng mở đèn 10:00. Booking hủy không làm gộp lịch.
