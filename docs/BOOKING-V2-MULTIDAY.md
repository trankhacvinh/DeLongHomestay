# Booking V2 — Lưu trú nhiều ngày

## Quyết định sản phẩm

De Long có hai kiểu đặt phòng độc lập:

1. **Theo khung giờ** — giữ nguyên các preset hiện tại.
2. **Lưu trú nhiều ngày** — có giá/đêm và giờ nhận/trả riêng theo từng phòng.

`Qua đêm` không được dùng làm giá lưu trú nhiều ngày. Đây là hai sản phẩm khác nhau.

## Domain

- `RoomRateType.TimeSlot`: khung giờ trong ngày.
- `RoomRateType.Overnight`: khung qua đêm ngắn hiện tại.
- `RoomRateType.Nightly`: giá/đêm cho lưu trú nhiều ngày.
- `BookingType.TimeSlot`: booking theo khung giờ.
- `BookingType.MultiDay`: booking liên tục từ ngày nhận tới ngày trả.

Booking multi-day lưu snapshot `RateName`, `UnitPrice`, `NightCount`, `RoomAmount` để thay đổi giá sau này không làm sai lịch sử.

## Availability

Multi-day khóa **một khoảng liên tục** từ check-in ngày đầu tới check-out ngày cuối. Chỉ cần một booking `Held`, `Confirmed` hoặc `CheckedIn` giao nhau là phòng không khả dụng.

`Requested` vẫn không khóa phòng.

## Pricing

`RoomAmount = NightlyRate.Price × NightCount`.

Không seed giá Nightly tự động vì chưa có giá thật từ chủ cơ sở. Admin phải cấu hình giá/đêm cho từng phòng; public chỉ hiển thị multi-day khi phòng có rate Nightly active và giá > 0.

## Calendar

Calendar multi-day sẽ được làm sau khi domain/public flow ổn. Booking trải nhiều ngày phải hiển thị như một dải liên tục; drag/drop chỉ triển khai sau đó.
