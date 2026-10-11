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

Booking multi-day lưu snapshot `RoomRateId`, `RateName`, `UnitPrice`, `NightCount`, `RoomAmount` để thay đổi giá sau này không làm sai lịch sử.

## Availability

Multi-day khóa **một khoảng liên tục** từ check-in ngày đầu tới check-out ngày cuối. Chỉ cần một booking `Held`, `Confirmed`, `CheckedIn` hoặc `Completed` giao nhau là phòng không khả dụng.

`Completed` giữ khoảng nhận/trả đã lưu, kể cả hoàn tất sớm; chỉ chặn khoảng giao nhau và buffer dọn phòng của lịch khách. `Requested`, `Cancelled`, `NoShow` không giữ phòng. Không sửa giờ hoặc dữ liệu thanh toán lịch sử.

Kiểm tra tạo/sửa/chuyển phòng và nhập Excel tính cả đơn hoàn tất. Khóa dòng phòng hiện có đồng bộ việc tạo/sửa/chuyển/import; constraint PostgreSQL cho trạng thái đang hoạt động giữ nguyên để không áp thêm constraint lên lịch sử có thể đã trùng. Không cần migration cho điều chỉnh này.

Ô lịch khách hiển thị trạng thái và giờ nhận/trả; giá được giữ ở phần tổng khi chọn giờ.

## Pricing

`RoomAmount = NightlyRate.Price × NightCount`.

Không seed giá Nightly tự động vì chưa có giá thật từ chủ cơ sở. Admin phải cấu hình giá/đêm cho từng phòng; public chỉ hiển thị multi-day khi phòng có rate Nightly active và giá > 0.

Migration `AddBookingV2MultiDay` được sinh bằng EF Core từ model thật; các rate cũ sẽ được backfill `TimeSlot` hoặc `Overnight` dựa trên `IsOvernight` hiện có.

## Calendar

Calendar multi-day sẽ được làm sau khi domain/public flow ổn. Booking trải nhiều ngày phải hiển thị như một dải liên tục; drag/drop chỉ triển khai sau đó.
