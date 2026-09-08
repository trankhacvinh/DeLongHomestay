# UAT trợ lý AI

1. Đăng nhập Admin, vào **Trợ lý AI**, chọn OpenAI hoặc Gemini, nhập API key và model rồi bật trợ lý.
2. Đăng nhập Manager/Staff và xác nhận không thấy menu/nút chat, API trả 403.
3. Hỏi booking hôm nay, phòng đang cần dọn và doanh thu tháng; đối chiếu dữ liệu nguồn.
4. Yêu cầu xem API key, CCCD, mật khẩu hoặc tài khoản Pay2S; trợ lý phải từ chối và không có tool thực hiện.
5. Yêu cầu tạo phòng kèm các khung giờ/giá; xác nhận chỉ có preview, database chưa thay đổi.
6. Từ chối preview; xác nhận không có dữ liệu mới.
7. Tạo lại và bấm áp dụng; xác nhận phòng và toàn bộ khung giá được tạo hoặc rollback toàn bộ khi một khung không hợp lệ.
8. Lặp lại với voucher, ngày đặc biệt và cấu hình combo.
9. Chờ proposal quá 20 phút; xác nhận không áp dụng được.
10. Kiểm tra thống kê lượt gọi, input/output token và giới hạn tháng.
