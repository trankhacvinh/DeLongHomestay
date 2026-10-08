# Tra cứu đặt phòng

- Trên di động, nút **Tra cứu đơn** nằm cạnh nút Đặt phòng trong header, độc lập với menu CMS.
- Mã + SĐT: giữ luồng xác minh hiện có. Mã cũ không đổi; mã mới gồm `BK` và 8 ký tự ngẫu nhiên, không dùng ký tự dễ nhầm. Unique index theo cơ sở vẫn bảo vệ trùng mã.
- Email: POST `/api/public/booking-lookup/email` có antiforgery và rate limit `public-lookup`. Chỉ gửi tối đa 20 đơn gần nhất thuộc email khách và cơ sở đang tra cứu. Có cả lịch sử đơn hoàn tất/hủy. Không trả dữ liệu đơn hoặc kết quả tồn tại email ra web.
- Dùng `BookingGuestGuideEmail` với `TemplateKey=BookingLookup`, `Trigger=PublicEmailLookup`. Worker SMTP hiện có gửi và retry. Mỗi email/cơ sở có khoảng chờ 5 phút giữa các yêu cầu đã ghi nhận.
- Email chứa mã, phòng, trạng thái, giờ nhận/trả địa phương, số tiền và ghi chú điều chỉnh thời gian. Không chứa ảnh căn cước hoặc ghi chú nội bộ khác.
- Không thay schema, không cần migration. Phải có SMTP hợp lệ trong cấu hình thông báo của cơ sở.

## Nghiệm thu production

- [ ] Mở trang chủ trên điện thoại: thấy nút Tra cứu đơn và chuyển đúng trang.
- [ ] Mã cũ + SĐT vẫn tra cứu được; đơn mới có mã 10 ký tự.
- [ ] Email có đơn: nhận email, không thấy dữ liệu đơn trên web.
- [ ] Email không có đơn: web trả cùng thông báo chung.
- [ ] Email khác/chi nhánh khác không nhận dữ liệu đơn không thuộc phạm vi.
- [ ] Gửi lại trong 5 phút không tạo thêm email đã ghi nhận.
- [ ] Xác minh SMTP gửi thật và nội dung giờ nhận/trả.
