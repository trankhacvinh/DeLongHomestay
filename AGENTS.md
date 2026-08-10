# Project working rules

Trước khi sửa dự án, đọc skill phù hợp trong `skills/`.

1. Nghiệp vụ booking/lịch phòng: `skills/domain-booking/SKILL.md`.
2. HTML/localStorage demo: `skills/demo-ui/SKILL.md`.
3. Production .NET/Razor/Vue/PostgreSQL: `skills/razor-migration/SKILL.md`.
4. Test/nghiệm thu/release: `skills/qa-release/SKILL.md`.

## Baseline production không được phá vỡ nếu chưa cập nhật ADR/docs

- 1 production project `src/DeLong.Web` + 1 test project `tests/DeLong.Tests`.
- Razor Pages render initial page; Vue 3 chỉ progressive-enhance từng page scope.
- Trong `.cshtml` ưu tiên `v-on`, `v-bind`, `v-model`; không dùng Alpine.
- CRUD/mutation mượt qua Minimal APIs + fetch; không reload toàn trang cho thao tác nhỏ.
- API cookie-auth mutation phải có antiforgery + authorization server-side.
- Không Repository Pattern chỉ để bọc EF Core.
- PostgreSQL, UUID, decimal money, UTC/timestamptz, `property_id` từ đầu.
- Không biến lịch thành 4 ô cứng/ngày; check-in/check-out thực tế là nguồn sự thật.
- Payment là entity riêng; không nhồi payment JSON vào booking.
- Booking/payment/expense đã phát sinh không hard-delete.
- Không plaintext password/connection string trong Git.
- Không Docker theo workflow hiện tại của chủ dự án.
- Mọi thay đổi schema/architecture cập nhật docs + roadmap/checklist.
