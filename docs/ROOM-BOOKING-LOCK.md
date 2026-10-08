# Khóa phòng tạm thời

## Sử dụng

Admin/Manager chọn **Khóa phòng** tại quản lý phòng hoặc lịch V1/V2, nhập lý do (1–500 ký tự) và xác nhận. Phòng chặn tất cả đơn mới, bao gồm ngày tương lai, đến khi được mở khóa thủ công. Staff/Media không có quyền khóa/mở khóa; API kiểm tra quyền chi nhánh và antiforgery.

Phòng vẫn xuất hiện trên website với nhãn “Tạm ngừng nhận đặt phòng”; khách không chọn giờ hoặc đặt được. Lý do/người khóa/thời điểm chỉ xuất hiện trong admin. Lịch vẫn giữ các đơn hiện có và lịch sử.

Đơn trước khi khóa vẫn thanh toán, tra cứu, nhận/trả phòng và sửa thông tin trên cùng phòng bình thường. Không thể chuyển đơn khác vào phòng khóa. Muốn nhập đơn mới phải mở khóa trước. Import Excel cũng không thể tạo đơn vào phòng khóa. Không gửi thông báo khóa/mở khóa cho khách.

## Dữ liệu và API

Migration `20261006150837_AddRoomBookingLock` thêm `is_booking_locked` (mặc định false), `booking_lock_reason`, `booking_locked_at_utc`, `booking_locked_by_user_id`; không sửa booking/payment cũ. Audit Room lưu `BookingLocked`/`BookingUnlocked`, người thao tác và trạng thái trước/sau.

`PUT /api/admin/properties/{propertyId}/rooms/{roomId}/booking-lock`

Body: `{ "isLocked": true, "reason": "Bảo trì điều hòa" }`; mở khóa dùng `{ "isLocked": false }`. Response là RoomDto cập nhật. Public DTO chỉ bổ sung `isBookingLocked`, không chứa lý do nội bộ. Tạo/chuyển đơn bị chặn trả lỗi nghiệp vụ `room_booking_locked` qua cơ chế lỗi hiện có.

Khóa/mở khóa và nhận đơn mới dùng `SELECT ... FOR UPDATE` trên cùng dòng phòng trong transaction PostgreSQL. Đơn được nhận trước thì commit trước khi khóa; khóa nhận trước thì chặn đơn mới. Transaction của voucher/import được giữ nguyên. Cache public bị xóa sau commit; lịch admin cập nhật qua sự kiện `room.booking-lock-changed` và poll. Public stream chỉ phát metadata thay đổi, không phát lý do; khách đang mở lịch được kiểm tra lại qua stream và poll.

## Deploy và nghiệm thu

- Backup database, chạy migration/bundle mới rồi triển khai application và static assets cùng phiên bản. Không dùng bundle cũ.
- Phòng hiện có phải mặc định mở; thử khóa một phòng, tạo đơn từ trang khách, admin booking, V1/V2 và import để xác nhận bị chặn.
- Kiểm tra một QR/đơn tạo trước khi khóa vẫn thanh toán và tra cứu bình thường; sau mở khóa có thể đặt lại.
- Kiểm tra role Admin/Manager, Staff/Media và quyền chi nhánh; ghi nhận lịch sử audit.
- Kiểm tra modal và nhãn khóa ở 320/390px; lịch vẫn hiển thị đơn cũ.
- PostgreSQL tests dùng `DELONG_TEST_CONNECTION` trỏ đến database thử nghiệm riêng; không dùng database production.

## Kết quả kiểm tra source (2026-10-06)

- Build thành công; 31 test PostgreSQL liên quan khóa phòng, phân quyền API, import, lịch và SePay đạt trên database thử nghiệm.
- Test tự động nhận/trả phòng đạt khi chạy trên database sạch riêng (test đếm toàn bộ đơn đến hạn, không dùng chung dữ liệu từ các test thanh toán).
- 6 test JavaScript đạt; node syntax checks và `git diff --check` sạch.
- Preview từ template quản lý phòng, modal và script Vue thật: khóa/mở khóa được ở 320px và 390px, không tràn ngang. Preview dùng API giả để kiểm tra UI; API thật được kiểm tra riêng bằng PostgreSQL/HTTP tests.
- Chưa triển khai hoặc nghiệm thu thiết bị thật trên production. Cần rebuild bundle migration và publish trước khi deploy.
