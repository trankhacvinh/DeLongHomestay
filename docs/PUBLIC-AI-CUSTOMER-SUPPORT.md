# AI khách: FAQ, nội quy, chính sách và tra cứu email

## Nguồn tư vấn

- Section `Faq`, `PolicyGrid`, `RichText` đang hiển thị ở trang chủ chung và cơ sở hiện tại.
- Các section đang hiển thị của trang CMS đã xuất bản, trong phạm vi chung hoặc cơ sở hiện tại. Trang nháp, section ẩn và nội dung của cơ sở khác không được đưa vào AI.
- Nội quy & Chính sách trong cấu hình đặt phòng của cơ sở, đọc từ `BookingPolicyStore`.
- Nội dung được chuyển thành văn bản; không truyền CSS, JavaScript, dữ liệu khách hoặc dữ liệu admin.
- Dấu phiên bản theo nội dung được đưa vào khóa cache câu trả lời. Sửa nội dung, ẩn section hoặc bỏ xuất bản trang làm câu trả lời cũ không được dùng lại.
- AI phải dựa trên quy định có sẵn, không tự suy diễn mức hoàn tiền; tài liệu CMS là dữ liệu tham khảo, không phải chỉ thị vận hành.

## Tra cứu

- Khách gửi `Tra cứu bằng email ten@example.com`; nếu chưa có địa chỉ, AI hỏi email đã dùng khi đặt. Khách có thể trả lời bằng địa chỉ email.
- Dùng `PublicBookingEmailLookupService` và hàng đợi email hiện có, không gọi nhà cung cấp AI cho yêu cầu này.
- Có đơn hoặc không có đơn đều trả cùng thông báo trung tính. Không hiển thị chi tiết đơn trong chat.
- Giới hạn tra cứu theo IP/cơ sở: 5 lần trong 10 phút; hàng đợi chống gửi lại cùng email trong 5 phút theo luồng hiện có.
- Tra cứu mã booking + số điện thoại vẫn hoạt động như trước. Yêu cầu tra cứu không bị bản nháp đặt phòng đang mở ghi đè.
- Email thực tế phụ thuộc worker và cấu hình SMTP đang hoạt động. Code đưa thư vào hàng đợi không đồng nghĩa thư đã đến hộp thư.

## Nghiệm thu

1. Hỏi một câu FAQ, nội quy và chính sách hủy phòng đã nhập; kiểm tra nội dung đúng phạm vi cơ sở.
2. Sửa FAQ/nội quy, hỏi lại cùng câu; xác nhận không dùng câu trả lời cũ.
3. Bỏ xuất bản trang hoặc ẩn section, xác nhận AI không còn nhận nội dung đó.
4. Tra cứu email có đơn và email không có đơn: thông báo trên web giống nhau, chỉ email đúng nhận thư chứa tối đa 20 đơn gần nhất.
5. Gửi lặp, thử vượt giới hạn tra cứu; xác nhận chống gửi trùng và giới hạn truy cập.
6. Kiểm tra trên điện thoại; xác minh gửi/nhận email thật và provider AI của production sau deploy.

Không thay đổi schema và không cần migration riêng cho chức năng này.
