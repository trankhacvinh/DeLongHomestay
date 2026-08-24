# Ma trận tình huống thanh toán Pay2S

Tài liệu này là bản đối chiếu giữa quy trình mong muốn và code hiện tại tại ngày 23/08/2026.

Ký hiệu:

- ✅ Đã đáp ứng.
- ⚠️ Đáp ứng một phần hoặc cần kiểm thử Pay2S thực tế.
- ❌ Chưa đáp ứng hoặc cách xử lý hiện tại khác quy trình mong muốn.

## 1. Quy tắc thời gian đã thống nhất

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| Khách tự đặt và bắt đầu thanh toán | Tạo booking `Held`, khóa phòng đến `ReleaseAt`; `PaymentExpiresAt` là hạn được mở link thanh toán; sau đó chờ IPN thêm 3 phút | Có hai mốc `ExpiresAtUtc` và `ReleaseAtUtc`; booking được chuyển sang `Held` | ✅ |
| Khách tự đặt, hết hạn thanh toán nhưng chưa hết 3 phút đệm | Không cho mở/tạo lại QR; vẫn khóa phòng và nhận IPN hợp lệ | Endpoint `resume` từ chối; intent cũ ngăn tạo QR mới; booking vẫn `Held` | ✅ |
| Khách tự đặt, hết cả khoảng đệm | Hủy booking, intent `Expired`, giải phóng phòng | Worker chạy mỗi 15 giây, chuyển intent `Expired` và booking `Cancelled` | ✅ |
| Nhân viên tạo booking rồi tạo QR gửi khách | Booking đã có xác nhận trực tiếp của nhân viên nên chỉ giữ đến `PaymentExpiresAt`, **không thêm 3 phút đệm** | API quản trị đặt `ReleaseAtUtc = ExpiresAtUtc`; không dùng khoảng đệm cấu hình | ✅ |
| Nhân viên tạo booking `Held`, hết hạn QR chưa thanh toán | Hủy booking và giải phóng phòng ngay tại `PaymentExpiresAt` | Worker hết hạn intent và hủy booking `Held` tại chính `PaymentExpiresAt` | ✅ |
| Nhân viên đã chuyển booking sang `Confirmed` rồi mới tạo QR | Hết hạn QR chỉ đóng intent; không tự hủy booking vì nhân viên đã cam kết giữ phòng | `CancelBookingOnExpiry = false` với booking `Confirmed`; booking vẫn khóa phòng | ✅ |
| Sai lệch vài giây do worker | Chấp nhận độ trễ kỹ thuật ngắn, không vượt quá chu kỳ worker | Worker quét mỗi 15 giây | ✅ |

## 2. Tạo QR và số tiền

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| Booking hợp lệ, chưa thanh toán | Tạo QR theo số tiền còn thiếu | Lấy `TotalAmount - tổng Payment chưa void` | ✅ |
| Booking đã thanh toán một phần | QR chỉ thu phần còn lại | Tính đúng số dư còn lại | ✅ |
| Booking đã thanh toán đủ | Không tạo QR mới | Trả lỗi `booking_paid` | ✅ |
| Booking `Cancelled`, `Completed` hoặc `NoShow` | Không cho tạo QR | Trả lỗi `booking_not_payable` | ✅ |
| Cơ sở chưa bật hoặc thiếu cấu hình Pay2S | Không tạo QR, báo lỗi rõ ràng | Kiểm tra hồ sơ theo `property_id` và trả lỗi cấu hình | ✅ |
| Pay2S không phản hồi hoặc trả lỗi | Không tạo intent giả; booking giữ nguyên để nhân viên xử lý | Không lưu intent nếu Pay2S tạo link thất bại | ✅ |
| Bấm tạo QR nhiều lần khi intent còn hiệu lực | Trả lại intent đang hoạt động, không tạo nhiều order | Tìm intent `Pending` chưa hết hạn và trả lại | ✅ |
| Bấm tạo QR trong thời gian đệm | Không tạo QR mới | Chặn bằng `payment_settlement_pending` | ✅ |
| Tạo QR mới sau khi intent cũ đã `Expired` | Cho phép nếu booking vẫn còn trạng thái có thể thanh toán | Có thể tạo intent mới nếu booking chưa bị hủy/kết thúc | ✅ |
| Nhân viên sửa tổng tiền sau khi đã tạo QR | Phải đóng QR cũ hoặc yêu cầu tạo QR mới theo số dư mới; IPN cũ không được tự xác nhận sai số dư | Mọi intent `Pending/Failed` của booking được đóng trước khi lưu thay đổi giá | ✅ |
| Nhân viên ghi nhận tiền mặt/chuyển khoản thủ công khi QR còn hiệu lực | Đóng hoặc giảm hiệu lực QR cũ để tránh khách trả thêm | Ghi hoặc void Payment đều đóng intent `Pending/Failed`; muốn thanh toán Pay2S phải tạo QR mới theo số dư mới | ✅ |
| Số tiền có phần thập phân | Không gửi sang Pay2S; báo dữ liệu không hợp lệ | Chỉ nhận số tiền nguyên dương | ✅ |

## 3. Khách mở, đóng và quay lại link thanh toán

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| Khách đóng tab hoặc chuyển ứng dụng khi QR còn hạn | Booking vẫn được giữ; IPN vẫn hoạt động; khách có thể mở lại link trước hạn | Intent lưu trong DB, endpoint trạng thái và `resume` hoạt động trước hạn | ✅ |
| Khách mở lại link sau `PaymentExpiresAt` | Không trả lại `payUrl` | Server trả `409 payment_session_closed` | ✅ |
| Khách đã lưu trực tiếp URL Pay2S cũ và mở sau hạn | DeLong không thể tin phía trình duyệt; nếu Pay2S vẫn nhận tiền thì xử lý như tiền đến muộn | DeLong không hủy collection link phía Pay2S; IPN muộn được xử lý phòng vệ | ⚠️ |
| Khách quay về trang DeLong trước khi IPN đến | Hiển thị trạng thái đang xác nhận và tiếp tục hỏi server | Trang return polling endpoint trạng thái | ✅ |
| Khách không quay về trang DeLong sau thanh toán | IPN vẫn là nguồn sự thật; booking vẫn được xác nhận | Xử lý thanh toán nằm ở IPN, không phụ thuộc redirect trình duyệt | ✅ |

## 4. IPN thành công, thất bại và đến muộn

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| IPN thành công trước `PaymentExpiresAt` | Ghi Payment Pay2S, intent `Succeeded`, booking `Confirmed` | Đã thực hiện | ✅ |
| IPN thành công trong khoảng đệm của booking công khai | Ghi Payment và xác nhận booking bình thường | Đã thực hiện | ✅ |
| IPN thành công sau `ReleaseAt` | Ghi Payment, intent `PaidAfterExpiry`, không giành lại phòng, cảnh báo quản trị | Đã thực hiện | ✅ |
| IPN thành công sau hạn của booking nhân viên | Vì không có khoảng đệm, mọi IPN sau `PaymentExpiresAt` phải là `PaidAfterExpiry` | `ReleaseAtUtc = ExpiresAtUtc`, nên IPN sau hạn được ghi `PaidAfterExpiry` | ✅ |
| IPN báo giao dịch thất bại | Ghi trạng thái thất bại, không ghi Payment, không xác nhận booking | Intent chuyển `Failed`, không tạo Payment | ✅ |
| IPN sai chữ ký | Từ chối và lưu mã lỗi phục vụ kiểm tra | Xác thực HMAC fixed-time, lưu `signature_invalid` | ✅ |
| IPN sai partner code | Từ chối | Đã kiểm tra | ✅ |
| IPN sai `orderId` hoặc `requestId` | Từ chối | Chỉ tìm intent khi cả hai khớp | ✅ |
| IPN sai `orderInfo` | Từ chối và lưu lỗi | Đã kiểm tra `order_mismatch` | ✅ |
| IPN sai số tiền | Từ chối và lưu lỗi | Đã kiểm tra `amount_mismatch` | ✅ |
| Pay2S gửi lại đúng IPN cùng `transId` | Không ghi trùng Payment | Nếu intent đã thành công/đến muộn và cùng `transId`, trả idempotent | ✅ |
| Cùng order nhưng xuất hiện IPN thành công với `transId` khác | Không được ghi thêm Payment; phải cảnh báo bất thường | Chặn bằng `transaction_conflict`; `transId` đã thuộc intent khác bị chặn bằng `transaction_reused` | ✅ |
| IPN và worker hết hạn chạy đồng thời đúng ranh giới | Chỉ có một kết quả nhất quán; không vừa xác nhận vừa hủy | IPN và worker cùng khóa hàng intent bằng PostgreSQL `FOR UPDATE`; worker dùng thêm `SKIP LOCKED` | ✅ |
| Ứng dụng tạm dừng đúng lúc hết hạn | Khi chạy lại phải tự hết hạn các intent quá hạn | Worker chạy lại; các endpoint tạo/resume/status cũng gọi expire | ✅ |

## 5. Nhân viên hủy hoặc thay đổi booking

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| Nhân viên hủy booking trước khi khách thanh toán | Đóng quyền resume ngay; phòng được giải phóng; intent nên được đánh dấu đóng/hủy nội bộ | Thay đổi trạng thái sang `Cancelled` đóng ngay intent `Pending/Failed` thành `Expired` trong cùng lần lưu | ✅ |
| Khách dùng URL Pay2S đã lưu và trả tiền sau khi nhân viên hủy booking | Ghi `PaidAfterExpiry`, không phục hồi booking, cảnh báo quản trị | Vì booking đã `Cancelled`, IPN thành công được coi là thanh toán muộn và tạo cảnh báo | ✅ |
| Nhân viên hủy booking đúng lúc IPN đang đến | Cần transaction/concurrency guard; hoặc xác nhận booking hoặc ghi tiền muộn, không được trạng thái mâu thuẫn | IPN khóa intent trong transaction; thao tác hủy đóng intent cùng lần lưu booking. Cần tiếp tục UAT cạnh tranh trên PostgreSQL thật | ⚠️ |
| Nhân viên đổi phòng/thời gian khi QR còn sống | Phải kiểm tra lại xung đột và quyết định giữ/đóng intent; link gửi khách phải phản ánh đơn mới | Sau conflict check, thay đổi phòng hoặc thời gian đóng QR cũ; nhân viên phải tạo QR mới | ✅ |
| Nhân viên hủy rồi khôi phục booking trong khi intent cũ còn hạn | Không được vô tình làm sống lại QR cũ; phải tạo intent mới | Intent đã thành `Expired` khi hủy và không được phục hồi cùng booking | ✅ |
| Booking `Confirmed` bị nhân viên hủy trong lúc QR còn hạn | Resume phải bị chặn; tiền đến sau đó là tiền muộn | Resume bị chặn và IPN xem booking `Cancelled` là thanh toán muộn | ✅ |
| Nhân viên hủy booking đã nhận tiền | Không xóa hoặc tự hoàn tiền; giữ Payment để đối soát và yêu cầu nhân viên ghi giao dịch hoàn tiền riêng | Booking chuyển `Cancelled`, Payment cũ vẫn giữ nguyên; chỉ intent chưa hoàn thành bị đóng | ✅ |

## 6. Xử lý tiền đến muộn

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| Tiền đến muộn, chọn hoàn tiền | Lưu quyết định và giao dịch hoàn tiền; không tự xác nhận booking | Tạo Payment loại `Refund`, lưu người và thời điểm xử lý | ✅ |
| Tiền đến muộn, chuyển phòng/thời gian khác | Kiểm tra phòng còn trống rồi cập nhật và xác nhận | Có conflict check server-side trước khi chuyển | ✅ |
| Tiền đến muộn, xác nhận lại lịch cũ | Chỉ xác nhận nếu phòng vẫn trống | Có conflict check server-side | ✅ |
| Hoàn tiền thực tế qua ngân hàng/Pay2S | Phải thực hiện hoàn tiền thật hoặc ghi rõ thao tác ngoài hệ thống | Hiện chỉ ghi sổ Payment hoàn tiền, chưa gọi API hoàn tiền Pay2S | ⚠️ |
| Khoản tiền muộn chưa được xử lý | Phải hiện cảnh báo bắt buộc cho quản trị | Có notification `pay2s-paid-after-expiry` | ✅ |
| Xử lý cùng một khoản tiền muộn hai lần | Chặn thao tác lặp | Kiểm tra `LatePaymentResolution` trước khi xử lý | ✅ |

## 7. Cấu hình, bảo mật và vận hành

| Trường hợp | Cách xử lý mong muốn | Cách xử lý của hệ thống hiện tại | Đã đáp ứng |
|---|---|---|---|
| Hai cơ sở dùng hai hồ sơ Pay2S khác nhau | Chọn hồ sơ theo `property_id` | Đã tách cấu hình theo cơ sở | ✅ |
| Hai cơ sở chủ động dùng chung tài khoản Pay2S | Cho nhập cùng bộ thông tin ở cả hai cơ sở | Đã cho phép | ✅ |
| Access key/secret key lưu trong DB | Phải mã hóa, không trả khóa rõ về UI | Dùng Data Protection và chỉ trả cờ đã cấu hình | ✅ |
| Quản trị đổi hoặc tắt hồ sơ Pay2S khi còn intent chờ | Intent đang chờ vẫn phải xác thực được IPN bằng khóa đã dùng lúc tạo, hoặc phải chặn thay đổi | Chặn tắt, xóa hoặc đổi partner/endpoint/khóa xác thực khi còn intent trong thời gian chờ | ✅ |
| Callback dùng localhost | Không cho dùng; callback phải là HTTPS công khai | Service yêu cầu callback HTTPS; UI hướng dẫn domain công khai | ✅ |
| Endpoint IPN bị gọi hàng loạt | Rate limit và xác thực chữ ký | Có rate limiting và chữ ký HMAC | ✅ |
| Nhân viên không có quyền quản lý | Không được tạo QR hoặc xem cấu hình | API admin yêu cầu policy và kiểm tra quyền theo cơ sở | ✅ |
| Mất IPN vĩnh viễn nhưng tiền đã chuyển | Cần đối soát chủ động hoặc màn hình nhập `transId`/kiểm tra trạng thái nhà cung cấp | Chưa có job đối soát với Pay2S hoặc thao tác truy vấn giao dịch | ❌ |
| Theo dõi giao dịch và lỗi callback | Phải lưu `transId`, thời gian callback, mã lỗi gần nhất | Đã lưu trên payment intent | ✅ |

## 8. Việc cần sửa theo ưu tiên

Các mục 1–7 dưới đây đã được triển khai. Mục 8 còn phụ thuộc API đối soát Pay2S cung cấp:

1. **Tách chính sách hết hạn theo nguồn booking**:
   - Booking công khai: `ReleaseAt = PaymentExpiresAt + SettlementGraceMinutes`.
   - Booking nhân viên tạo QR: `ReleaseAt = PaymentExpiresAt`.
2. Khi booking bị nhân viên hủy, đóng intent nội bộ ngay; URL Pay2S cũ nếu vẫn trả tiền phải đi vào `PaidAfterExpiry`.
3. Dùng transaction/concurrency guard cho cuộc đua giữa IPN, worker hết hạn và thao tác hủy booking.
4. Vô hiệu hóa intent khi tổng tiền, phòng hoặc thời gian booking thay đổi; buộc tạo QR mới.
5. Đóng intent Pay2S đang chờ khi nhân viên ghi nhận đủ tiền bằng phương thức khác.
6. Chặn ghi Payment lần hai nếu cùng intent nhận `transId` khác bất thường; tạo cảnh báo thay vì tự cộng tiền.
7. Không cho đổi/tắt khóa Pay2S khi còn intent chờ, hoặc lưu snapshot khóa xác thực riêng cho intent.
8. Bổ sung đối soát giao dịch khi IPN không đến. **Chưa đáp ứng** vì hiện chưa có API truy vấn trạng thái giao dịch được xác nhận trong tích hợp đang dùng.

## 9. Kết luận hiện trạng

Luồng công khai giữ hai mốc thời gian; luồng QR do nhân viên tạo dùng một mốc và không có khoảng đệm. Hủy/sửa booking, ghi nhận tiền khác, IPN trùng và cuộc đua với worker đã có guard phía server. Phần còn phải nghiệm thu thực tế là IPN Pay2S qua domain công khai, cạnh tranh đúng ranh giới thời gian trên PostgreSQL và đối soát chủ động khi nhà cung cấp không gửi IPN.
