# Operations Realtime + Availability Interval UAT

PR nền này chưa thêm giao diện Calendar V2. Mục tiêu là xác nhận dữ liệu realtime và interval engine đủ ổn định để Calendar V1, Calendar V2 và Housekeeping dùng chung.

## 1. Kiểm tra Operations SSE

Mở Admin → Lịch phòng, DevTools → Network và lọc `operations/stream`.

Kết quả mong đợi:

- request `/api/admin/properties/{propertyId}/operations/stream` giữ trạng thái streaming;
- `document.documentElement.dataset.operationsRealtime` trở thành `connected`;
- response có heartbeat định kỳ và event `operations` khi booking thay đổi.

Payload event chỉ được có metadata vận hành như `eventId`, `propertyId`, `type`, `bookingId`, `roomId`, `occurredAtUtc`. Không được có tên khách, SĐT, email hoặc dữ liệu CCCD.

## 2. Booking web → Calendar V1 không F5

1. Giữ `/Admin/Calendar` mở ở tab A.
2. Ở tab/browser B tạo một booking web hợp lệ.
3. Không refresh tab A.

Kết quả mong đợi:

- notification vẫn xuất hiện như hiện tại;
- Calendar nhận event `booking.created` sau khi Booking Core V2 hoàn tất;
- booking xuất hiện trên đúng phòng/ngày/trạng thái `Giữ phòng` mà không F5;
- sau reconcile, modal booking đọc được dữ liệu cuối cùng chứ không phải snapshot trước khi Core V2 lưu xong.

## 3. Admin edit / move / status / payment

Từ tab B lần lượt:

- sửa giờ hoặc giá booking;
- kéo/chuyển booking sang phòng/ngày khác;
- đổi trạng thái;
- ghi nhận hoặc void thanh toán.

Tab A phải tự reconcile booking. Sửa/move/status không yêu cầu notification mới vì chúng đi qua operations stream riêng.

## 4. Hold hết hạn

1. Tạo booking web để vào trạng thái Held.
2. Giữ Calendar mở.
3. Không xác nhận booking và chờ quá 3 phút.

Kết quả mong đợi: operations stream sweep hold khoảng mỗi 5 giây; booking chuyển Held → Requested và phát `booking.hold-expired`; Calendar tự nhả trạng thái khóa mà không F5.

## 5. Housekeeping realtime

Mở Admin → Dọn phòng ở tab A, đổi trạng thái phòng hoặc hoàn tất booking từ tab B.

Kết quả mong đợi:

- event `housekeeping.changed` hoặc `booking.status-changed` làm board tải lại;
- fallback poll 15 giây vẫn reconcile nếu SSE bị ngắt;
- focus/quay lại tab cũng tải lại ngay.

## 6. Availability interval Admin

Gọi:

```text
GET /api/admin/properties/{propertyId}/operations/availability/rooms/{roomId}?from=2026-08-20&days=10
```

Mỗi rate slot phải có `startUtc`, `endUtc`, `state`, `occupiedRatio`, `occupied`, `free`.

Case quan trọng: rate 12:00–15:00 nhưng booking thực tế chiếm 12:00–14:00 phải trả:

- `state = partial`;
- `occupiedRatio ≈ 0.6667`;
- free range 14:00–15:00.

Rate qua đêm 21:00–10:00 nằm trong hàng ngày bắt đầu và `endUtc` thuộc ngày hôm sau.

## 7. Availability public không lộ PII

Gọi:

```text
GET /api/public/room-availability?roomId={roomId}&from=2026-08-20&days=10&siteSlug={siteSlug}
```

Public response được phép có trạng thái `held` / `booked` và khoảng thời gian chiếm để Calendar V2 public vẽ pill partial. Response không được có booking id, tên khách, SĐT, email, ghi chú hoặc thông tin CCCD.

## 8. Reconnect/fallback

Trong DevTools bật Offline vài giây rồi Online lại.

- operations marker chuyển `reconnecting` rồi `connected`;
- khi reconnect Calendar reconcile lại ngay;
- Calendar còn poll dự phòng 15 giây và refresh khi focus/visible, nên mất một event không làm dữ liệu stale lâu dài.
