# Calendar V2 UAT

Calendar V2 giữ nguyên Calendar V1 và bổ sung một góc nhìn theo từng phòng: ngày chạy dọc, các khung giờ/qua đêm chạy ngang. Availability được tính từ interval thời gian thực tế của booking. Cột `Lưu trú theo đêm` không hiển thị riêng trong V2; booking nhiều ngày vẫn khóa các khung giờ thực tế mà nó chồng lấn.

## Admin

1. Mở `/Admin/Calendar` và chọn **Theo khung giờ** ở phần `Kiểu lịch`.
2. Xác nhận `[‹] [Tên phòng] [›]` đổi đúng từng phòng và mỗi phòng hiển thị 10 ngày.
3. Xác nhận chỉ có các cột khung giờ và qua đêm; mức giá `Lưu trú theo đêm` không tạo thêm cột riêng.
4. Tạo booking đúng toàn bộ một khung. Ô tương ứng phải chuyển sang trạng thái đã đặt/giữ phòng mà không cần F5.
5. Tạo booking linh hoạt, ví dụ khung `12:00–15:00` nhưng booking `12:00–14:00`. Pill phải bị tô đúng khoảng 2/3 và hiển thị phần còn `14:00–15:00`.
6. Bấm phần đã đặt của pill. Modal **Chi tiết lượt đặt** hiện tại phải mở đúng booking, bao gồm thông tin người đặt/CCCD nếu có.
7. Bấm một khung hoàn toàn trống. Modal tạo booking phải mở với phòng, ngày, rate, giờ và giá của khung đã điền sẵn.
8. Bấm phần trống còn lại của một khung partial. Modal tạo booking phải mở với giờ linh hoạt chính xác; `Mức giá` không bị ép và giá phòng chỉ là gợi ý có thể sửa tay.
9. Mở Calendar V2 ở tab A, tạo/sửa/đổi giờ/đổi trạng thái booking ở tab B. Tab A phải đổi tự động qua operations SSE; không F5.
10. Chuyển về **Tổng quan** và xác nhận Calendar V1 vẫn giữ thanh booking nhiều ngày như trước.

## Public room availability block

1. Mở trang chi tiết một phòng đã xuất bản, ví dụ `/h/{siteSlug}/rooms/{roomSlug}`.
2. Ngay sau phần **Giá & thời gian** phải có block **Lịch phòng · Chọn thời gian phù hợp**.
3. Xác nhận ngày chạy dọc và các khung giờ/qua đêm chạy ngang, giống logic Admin V2.
4. Bấm một khung hoàn toàn trống. Khung chuyển sang **Đang chọn**, phía dưới hiện ngày/giờ/rate/giá và nút **Đặt khung này**.
5. Bấm **Đặt khung này**. Trang booking phải nhận sẵn `date`, `room` và `rate`.
6. Với khung partial, bấm phần còn trống. Không được tự đặt online; block phải ghi rõ khoảng giờ còn trống và hướng khách liên hệ homestay để đặt giờ linh hoạt.
7. Mở public block ở tab A, tạo booking cho cùng phòng ở Admin/tab B. Block tab A phải đổi tự động qua SSE, không F5.
8. Public network payload `/api/public/room-availability` và `/api/public/room-availability/stream` không được chứa booking id, tên khách, số điện thoại, email hoặc CCCD.

## Automated coverage

- `AvailabilityIntervalProjectorTests`: empty/full/partial/overlap và free-range chính xác.
- `OperationsAvailabilityIntegrationTests`: PostgreSQL kiểm tra booking `12:00–14:00` trong slot `12:00–15:00`, slot qua đêm, hold expiry và public payload không lộ PII.
- `PublicAvailabilityRealtimeContractTests`: public SSE event chỉ được có metadata availability theo phòng, không được có booking id hoặc dữ liệu khách.
- Workflow `.NET` chạy `node --check` cho toàn bộ JS, bao gồm Admin/Public Calendar V2.

## Fallback/reconnect

- Tắt mạng vài giây rồi bật lại: EventSource phải tự reconnect và availability tự reconcile.
- Quay sang tab khác rồi quay lại: lịch tự tải lại.
- Khi SSE gián đoạn, fallback poll 15 giây vẫn phải đưa lịch về dữ liệu server hiện tại.

Không có migration database trong PR Calendar V2.