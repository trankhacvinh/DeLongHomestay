# Mapping dữ liệu Excel → Web App

## Nguồn

### `Lịch Book phòng DeLong tháng 8_2026`

Vai trò: lịch vận hành trực quan theo phòng/ngày/khung giờ. File cho thấy booking thực tế thường linh hoạt hơn preset, ví dụ check-in 15:30, 22:30, nhận từ 16:30, out 15:30.

### `Bản sao của Long Thành Home`

Vai trò: mô hình dữ liệu sơ khai với các sheet Đặt phòng, Khách hàng, Phòng, Nhân viên, Chi phí, Cài đặt.

## Mapping

| Excel | Demo | Production target |
|---|---|---|
| Tên cơ sở | `settings.property` | `properties` |
| Mã phòng JSON | `rooms[]` | `rooms` + `room_rates` |
| Tên khách / SĐT | `customers[]` | `customers` |
| Đặt phòng | `bookings[]` | `bookings` |
| Thanh toán Json | `payments[]` | `payments` |
| Dọn phòng Json | `housekeeping[]` | `housekeeping_tasks` |
| Chi phí | `expenses[]` | `expenses` |
| Cài đặt | `settings` | `settings`/lookup tables |

## Dữ liệu phòng chuẩn hóa

| Phòng | Khung 1 | Khung 2 | Khung 3 | Qua đêm |
|---|---:|---:|---:|---:|
| CoCo Blue #1 | 10:30–13:30 / 250k | 14:00–17:00 / 250k | 17:30–20:30 / 250k | 21:00–09:30 / 360k |
| Abaus #2 | 11:00–14:00 / 210k | 14:30–17:30 / 210k | 18:00–21:00 / 210k | 21:30–10:00 / 330k |
| Hongkong #3 | 11:00–14:00 / 250k | 14:30–17:30 / 250k | 18:00–21:00 / 250k | 21:30–10:00 / 360k |
| Moon Stone #4 | 11:30–14:30 / 270k | 15:00–18:00 / 270k | 18:30–21:30 / 270k | 22:00–10:30 / 390k |
| Amber Stay #5 | 12:00–15:00 / 300k | 15:30–18:30 / 300k | 19:00–22:00 / 300k | 22:30–11:00 / 439k |
| La Roman #6 | 12:00–15:00 / 270k | 15:30–18:30 / 270k | 19:00–22:00 / 270k | 22:30–11:00 / 390k |

## Các điểm đã làm sạch

- `Moonstone #4` / `Moon Stone #4` → dùng `Moon Stone #4`.
- `HongKong #3` / `Hongkong #3` → dùng `Hongkong #3`.
- Moon Stone khung đầu `14:03` được xem là lỗi nhập và chuẩn hóa thành `14:30` theo lịch vận hành.
- La Roman khung đầu `00:00` được xem là lỗi nhập và chuẩn hóa thành `12:00` theo lịch vận hành.
- Không di chuyển password plaintext từ Excel vào seed data.
