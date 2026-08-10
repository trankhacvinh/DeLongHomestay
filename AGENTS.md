# Project working rules

Trước khi sửa dự án, đọc skill phù hợp trong `skills/`.

1. Nghiệp vụ booking/lịch phòng: `skills/domain-booking/SKILL.md`.
2. HTML/CSS/JavaScript demo: `skills/demo-ui/SKILL.md`.
3. Chuyển ASP.NET Core Razor Pages + PostgreSQL: `skills/razor-migration/SKILL.md`.
4. Test/nghiệm thu/release: `skills/qa-release/SKILL.md`.

## Nguyên tắc không được phá vỡ

- Không biến lịch thành 4 ô cứng/ngày. `checkIn` và `checkOut` thực tế là nguồn sự thật.
- Không nhồi thanh toán vào JSON của booking. Payment là entity riêng.
- Không dùng màu làm trạng thái nghiệp vụ. Màu chỉ là presentation metadata.
- Không lưu password plaintext trong production.
- UI phải ưu tiên thao tác nhanh, mobile usable và lịch phòng là trung tâm.
- Demo không được thêm framework/build step nếu chưa có quyết định kiến trúc mới.
- Mọi thay đổi schema phải cập nhật `docs/DATA-MODEL.md` và `docs/RAZOR-MIGRATION.md`.
