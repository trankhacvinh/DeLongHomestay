# Roadmap

## Đã triển khai — Pricing V2

- [x] Giá ngày thường/cuối tuần theo phòng và ca.
- [x] Combo đủ ngày, combo đúng số ca liên tục và phụ thu ngày đặc biệt.
- [x] Chế độ ngày chỉ cho đặt combo cả ngày.
- [x] Snapshot phụ thu tách khỏi phần tiền được voucher giảm.

## Đã triển khai — tài khoản khách và nền tảng tích điểm

- [x] Đăng ký/đăng nhập khách bằng số điện thoại và mật khẩu.
- [x] Hồ sơ, lịch sử booking, đổi mật khẩu và TOTP Authenticator.
- [x] Kho CCCD mã hóa dùng cho booking sau, không cho khách đọc lại ảnh cũ.
- [x] Nhân viên đăng nhập bằng username/email và tự cấu hình TOTP.
- [x] Sổ điểm thật có cấu hình, mặc định tắt, cộng đúng một lần khi booking hoàn tất.
- [ ] Quy tắc đổi điểm và màn hình điều chỉnh điểm có audit.

## Phase 0 — Demo UX/localStorage

- [x] Chuẩn hóa dữ liệu Excel và 6 phòng.
- [x] Public/admin demo bằng localStorage.

## Phase 1 — Architecture freeze

- [x] 1 production project + 1 test project.
- [x] .NET 10 Razor Pages.
- [x] Vue 3 in-DOM progressive enhancement.
- [x] Minimal APIs cho interaction CRUD.
- [x] PostgreSQL + EF Core/Npgsql.
- [x] ASP.NET Core Identity.
- [x] Không Docker.
- [x] Soft-delete/archive/audit direction.
- [x] UTC + property timezone strategy.

## Phase 2 — Foundation

- [x] Solution/project skeleton.
- [x] EF Core/Npgsql/Identity.
- [x] Property, Room, RoomRate, UserPropertyAccess.
- [x] Seed De Long + 6 phòng + rates chuẩn theo Excel.
- [x] Vue + api.js + antiforgery pattern.
- [x] Rooms Razor + modal + Minimal API CRUD/archive.
- [x] CI restore/build/test + JavaScript syntax.
- [x] EF migrations + PostgreSQL local đã chạy được.
- [x] Seed admin bằng User Secrets và đăng nhập local.
- [x] PostgreSQL integration workflow không dùng Docker.

## Phase 3 — Customer + Booking core

- [x] Customer entity/service/API + normalized phone.
- [x] Booking entity/status/rules.
- [x] Create/edit booking + state transitions/cancel/no-show.
- [x] C# conflict validation.
- [x] PostgreSQL exclusion constraint chống booking overlap.
- [x] Database race conflict (`23P01`) → API `409 ProblemDetails`.
- [x] Calendar server-backed + Vue modal create/edit.
- [x] Booking list + search/filter/state actions.
- [x] Customers page + Vue add/edit.
- [x] Multi-property resolver + selector UI.
- [x] Booking audit timeline.
- [x] PostgreSQL constraint/integration tests.
- [x] Booking V2 multi-day + giá theo đêm + Calendar multi-day.
- [x] Calendar drag/drop desktop có confirm + conflict guard.

## Phase 4 — Payments + Operations

- [x] Payment ledger Receipt/Refund + void giữ lịch sử.
- [x] Booking PaidAmount/BalanceAmount tính từ ledger.
- [x] Payment API + Vue modal không reload.
- [x] Check-out tự chuyển phòng sang Bẩn.
- [x] Housekeeping Bẩn → Đang dọn → Sạch.
- [x] Expenses + void.
- [x] Finance + Reports.
- [x] Settings/rates.
- [x] Dashboard vận hành.
- [x] UI/UX redesign desktop + mobile admin.
- [x] Housekeeping Schedule V2 sinh việc từ giờ booking thật + chế độ văn bản sao chép + offset phút cấu hình theo cơ sở.

## Phase 5 — Public booking

- [x] Public boutique landing page.
- [x] Public room catalog.
- [x] Room detail + rates.
- [x] Availability theo ngày/khung giờ từ PostgreSQL.
- [x] Booking request tạo trạng thái `Requested`.
- [x] Server-side price/time derivation từ RoomRate.
- [x] Conflict check trước khi nhận request.
- [x] Antiforgery + honeypot + rate limit 5 request/IP/10 phút.
- [x] Success page.
- [x] Dashboard Admin inbox cho yêu cầu website mới.
- [x] Public multi-day booking.
- [x] Public chọn nhiều khung liền nhau, sticky tổng tiền, giá cả ngày/giảm theo số khung và khóa cạnh tranh trước khi tạo Pay2S.
- [x] CCCD người thứ hai cho booking từ 3 khách trở lên.
- [x] Room Content V2: gallery, cover/focal, optimized images, rich editor, amenities/tags/highlights.
- [x] Hướng dẫn phòng soạn bằng editor, hiển thị sau đặt/tra cứu, tải PDF và khóa tra cứu sau checkout hoặc trạng thái terminal.
- [x] Visual UAT public desktop/mobile vòng chính.
- [ ] End-to-end UAT cuối: public request → Pay2S Sandbox/IPN → Confirmed → checkout.
- [x] Nền tảng Pay2S theo từng cơ sở: cấu hình mã hóa, payment intent, tách hạn thanh toán và hạn giải phóng phòng, đệm chờ IPN, ghi Payment và quy trình xử lý thanh toán muộn.
- [ ] UAT Pay2S thực tế trên `delong.pmedia.vn`: tạo QR Sandbox, webhook công khai, đóng/mở lại trang, hết hạn và IPN đến muộn.
- [x] Notification booking ngoài hệ thống qua danh sách email và Telegram theo từng cơ sở, có outbox/retry và gửi thử.
- [x] Email hướng dẫn check-in cho khách khi booking xác nhận, có trạng thái gửi trong chi tiết lịch/booking và gửi lại thủ công.
- [x] Email báo hủy cho khách và bộ mẫu email booking/check-in/hủy tùy chỉnh theo từng cơ sở.
- [x] Voucher theo cơ sở: phạm vi khung/qua đêm/cả ngày, giới hạn tổng/mỗi khách, giữ lượt đồng thời, vòng đời Pay2S, voucher 100%, hoàn lượt thủ công và email tùy chỉnh.
- [ ] Kênh Zalo/SMS — chỉ làm khi cần.

## Phase 6 — Migration & go-live (đang thực hiện)

- [x] Import booking/khách Excel theo preview → validate → transaction.
- [x] Converter lịch màu cũ → mẫu booking cần bổ sung tên/SĐT.
- [ ] UAT tổng thể với dữ liệu gần thực tế.
- [ ] Backup/restore rehearsal.
- [ ] Production deployment.
- [ ] Logging/monitoring + health checks.
- [ ] Persistent media + Data Protection keys trên production storage.
- [ ] Tài khoản/role nhân viên thật.
- [ ] Hướng dẫn nhân viên.
- [ ] Go-live.
- [x] Chức năng độc lập báo cáo tình trạng phòng: ảnh/video nhiều file, ảnh tối ưu, tag tự điền nội dung, đánh giá 5 sao, bảng lọc, trạng thái xử lý và lịch sử theo cơ sở.
- [ ] UAT thực tế camera trên iOS Safari và Android Chrome; kiểm tra retry khi mạng di động yếu.
- [ ] Màn hình quản lý đầy đủ cho thêm, sắp xếp và ngừng dùng tag mẫu.
# Admin AI assistant (2026-09)

- [x] Hồ sơ OpenAI/Gemini/DeepSeek theo cơ sở, mã hóa API key.
- [x] Chat Admin trên navbar, tra cứu snapshot booking/phòng/doanh thu/housekeeping.
- [x] Thống kê lượt gọi và input/output token theo tháng.
- [x] Lịch sử hội thoại theo Admin/cơ sở, tạo cuộc trò chuyện mới và mở lại nội dung cũ.
- [x] Đính kèm DOCX/PDF/ảnh để AI xử lý nội dung thay vì nhập lại bằng tay.
- [x] Ngân sách USD, đơn giá token tùy model, cảnh báo và khóa theo chi phí ước tính tháng.
- [x] Preview và duyệt proposal cho phòng/khung giá/voucher/ngày đặc biệt/cấu hình giá.
- [x] Preview nội dung phòng, hướng dẫn check-in, thông tin website và SEO; loại trừ custom code và cấu hình bí mật.
- [ ] UAT với API key thật của OpenAI, Gemini và DeepSeek.
- [ ] Bổ sung báo cáo phân tích nâng cao; RAG chưa nằm trong phạm vi hiện tại.
- [x] Giai đoạn AI 0–2 (nền dữ liệu): audience, public kill switch/quota, Admin budget reserve, persistent response cache, knowledge snapshot, conversation summary và tool execution audit.
- [x] Giai đoạn AI 2 (runtime): toàn bộ provider call đi qua gateway/quota chung; token và chi phí được giữ trước bằng PostgreSQL để chống vượt hạn mức đồng thời, rồi quyết toán theo usage thật; cache invalidation dùng data version và usage dashboard tách audience.
- [x] Giai đoạn AI 3–4 nền tảng: Customer AI read-only, booking draft và lookup bằng booking code + số điện thoại.
- [x] Customer AI read-only v1: feature flag, public knowledge snapshot, quota/budget, IP rate limit, response cache và chat responsive.
- [x] Customer AI typed availability/quote theo ngày và booking draft TTL chuyển vào form hiện có.
- [x] Customer AI lưu lịch sử cục bộ tối đa 30 ngày/10 cuộc trò chuyện trên đúng trình duyệt, có mở lại, tạo mới và xóa.
- [x] Booking draft giữ mã voucher và chuyển vào form; voucher chỉ được xác thực/quote sau khi khách nhập số điện thoại để bảo đảm giới hạn theo khách.
- [ ] Đồng bộ lịch sử Customer AI qua tài khoản/nhiều thiết bị (chưa lưu nội dung chat công khai vào server để giảm dữ liệu cá nhân và chi phí).
- [x] Giai đoạn AI 5–9 code: Staff AI, báo cáo Owner, proposal allowlist, prompt-injection guard, booking/quota concurrency và persistent cache đã hoàn thành; UAT provider thật, browser/load test và vận hành production vẫn là checklist release riêng.
- [x] Owner/Admin typed report v1: ngày/tuần/tháng/quý/năm, thực thu/hoàn tiền/chi phí/dòng tiền/công suất/hủy và so sánh kỳ trước; bảng render trực tiếp không nhờ model tính số.
- [x] Owner/Admin report breakdown: phòng, nguồn, loại booking, khung giờ, khách mới/quay lại, thời lượng và thời gian đặt trước.
- [x] Owner/Admin report cache theo data version và link mở báo cáo GUI đúng cơ sở/tháng.
- [x] Admin AI action Giai đoạn 7: typed proposal cho phòng/giá/voucher/ngày đặc biệt/nội dung/SEO, allowlist, preview trước/sau, stale check, apply một lần, rollback batch và audit người duyệt.
- [x] Calendar V2 cho cấu hình màu booking theo cơ sở: công nợ, ghi chú đặc biệt, giờ linh động, nhiều khung và trạng thái vận hành; cấu hình lưu trong persistent DataRoot.
- [x] Staff AI giữ context kỳ hội thoại cho câu hỏi nối tiếp như “còn phòng cần dọn?” hoặc “thực thu thì sao?”.
- [x] Phân tích Owner v1 tách facts/nhận định/đề xuất, có ngưỡng dữ liệu và chỉ chuyển đề xuất sang luồng preview Admin.
- [x] Giai đoạn AI 8: facts/nhận định/khuyến nghị có regression test, khuyến nghị chỉ là tham khảo và không tự mutation.
- [x] AI hardening: prompt injection từ chat, file đính kèm và dữ liệu database không thể vượt server allowlist.
- [x] AI booking draft concurrency: draft không khóa phòng; khi hai draft gửi đồng thời, PostgreSQL conflict guard chỉ tạo đúng một booking `Held`.
- [x] AI production hardening: PostgreSQL quota/budget reservation, provider cached-token, dashboard lỗi/cache/latency/cost/blocked, persistent cache restart/invalidation, global kill switch và rollback checklist.
