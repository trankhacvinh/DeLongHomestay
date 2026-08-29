# UAT chọn nhiều khung liên tiếp

1. Cấu hình 4 khung A, B, C, D theo đúng `SortOrder`; D là qua đêm.
2. Trong sửa phòng, bật giá cả ngày và nhập giá riêng.
3. Trong Cài đặt → Quy tắc đặt phòng online, nhập giới hạn ngày và các mức giảm, ví dụ `2:5, 3:10`.
4. Ở lịch public, chọn bắt đầu từ B. Xác nhận chỉ C, D rồi A ngày sau lần lượt có thể nối tiếp; A cùng ngày và các khung bị bỏ qua không thể thêm.
5. Chọn đủ A+B+C+D cùng ngày. Kiểm tra thanh sticky dùng giá cả ngày.
6. Chọn nhiều khung chưa đủ ngày. Kiểm tra mức giảm cao nhất thỏa số khung.
7. Bấm Đặt phòng. Modal chỉ còn tóm tắt và thông tin khách, không còn bộ chọn phòng/khung.
8. Với 1–2 khách, yêu cầu một CCCD trước/sau. Với từ 3 khách, yêu cầu thêm đủ hai mặt CCCD người thứ hai; tài khoản có CCCD chỉ miễn bộ đầu.
9. Gửi đồng thời hai request cùng phòng/khoảng giờ. Chỉ một request sang Pay2S; request còn lại nhận HTTP 409 và lịch được tải lại.
10. Tắt cấu hình Pay2S rồi gửi. Booking tạm phải chuyển `Cancelled`, không để phòng bị khóa.
