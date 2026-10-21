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
- [x] Tự động check-in/check-out theo giờ booking thật, có hai công tắc theo cơ sở và mặc định bật.
- [x] Vai trò Media giới hạn theo cơ sở, chỉ quản lý nội dung/ảnh phòng, Gallery, Media Library và Blog.

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
- [x] First-run setup tạo Admin đầu tiên, tùy chọn seed trọn bộ De Long và cấu hình an toàn không chứa API key/credential.
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

## SePay thay Pay2s

- [x] Cấu hình cơ sở, QR VietQR, webhook xác thực, chống trùng, vòng đời booking/voucher và sổ đối soát.
- [x] Đối soát thủ công qua API SePay v1 theo ID giao dịch; giữ tương thích Pay2s cũ.
- [ ] Nghiệm thu SePay Test Mode qua domain staging và giao dịch ngân hàng thật trước khi bật production.
- Chi tiết vận hành: [SEPAY-PAYMENTS.md](SEPAY-PAYMENTS.md).

## Khóa phòng tạm thời (2026-10-06)

- Trạng thái khóa nhận đơn riêng, lý do và audit; quyền Admin/Manager theo chi nhánh.
- Chặn đơn mới/chuyển phòng/import; giữ nguyên đơn, QR và lịch sử có trước khi khóa.
- Row lock PostgreSQL bảo vệ thứ tự khóa/tạo đơn; migration `AddRoomBookingLock` và nghiệm thu tại `docs/ROOM-BOOKING-LOCK.md`.

## Lịch khóa phòng theo thời gian (2026-10-09)

- Menu quản lý riêng; nhiều phòng, khoảng liên tục hoặc khung giờ lặp theo ngày, qua đêm và kết thúc sớm.
- Chặn overlap khi tạo/chuyển/import, row lock chung với nhận đơn; báo các đơn cũ bị ảnh hưởng và giữ nguyên thanh toán.
- Lịch khách không lộ lý do; lịch admin thể hiện vùng khóa; migration `AddRoomBookingSchedules`.
- Nghiệm thu triển khai Ubuntu và điện thoại thật sau khi áp dụng migration mới.

- [x] AI quản trị đề xuất khóa/mở khóa và tạo/sửa/kết thúc lịch khóa phòng; preview server, xác nhận đơn trùng, giữ đơn cũ và transaction/audit.
- [ ] Nghiệm thu các câu lệnh khóa phòng với provider AI thật sau deploy.

### Kiểm tra dữ liệu và AI tạo booking

- [x] Source: danh sách đối soát, dedup, ghi chú/audit, cảnh báo nội bộ và quét định kỳ.
- [x] Source: AI giải thích phát hiện; tạo booking qua preview/xác nhận và kiểm tra lại khóa/overlap.
- [ ] Production: áp dụng AddDataQualityIssues bằng bundle mới; kiểm tra quyền chi nhánh và giao diện điện thoại.
- Xem [DATA-QUALITY.md](DATA-QUALITY.md).

### 2026-10-11 — Calendar presentation / housekeeping
- [x] Fix historical successor detection (includes completed stays); retain confirmed 4-hour merge rule.
- [x] Persist optional manual booking color independently of payment/status; migration AddBookingDisplayColor.
- [x] Readable scrolling mobile slot layout with optional fit overview.
- [ ] Apply migration/deploy and perform authenticated device UAT. See BOOKING-COLOR-HOUSEKEEPING.md.

- [x] Admin manual booking: default 2 guests (bounded by room capacity), full initial receipt for new bookings; preserve staff-entered deposit and zero additional receipt on edits. Local RobinHerbots Inputmask 5.0.9 for VND fields; nightly extension reuses the common receipt flow. Source verification completed separately from deployment.

## Setup website mới / demo thương hiệu (2026-10-12)

- Xem `docs/WINDOWS-DEMO-SETUP.md`. Production giữ AutoMigrate/SeedOnStartup=false; migrate riêng trước khi chạy.
- GET `/` khi chưa có Admin chuyển tới `/setup`; setup khóa sau commit, Admin được cấp quyền cơ sở mới.
- Không thay đổi schema. Cần nghiệm thu setup, booking/payment/report và mobile trên Windows trước khi bàn giao.

## Giãn lịch dọn phòng (2026-10-13)

- Cấu hình theo cơ sở: bật/tắt giãn lịch, thời lượng (mặc định 30 phút), số lao công (1), dọn sớm tối đa (30 phút), giờ bắt đầu/kết thúc làm việc theo múi giờ cơ sở. Mặc định tắt; cần nhập giờ làm việc trước khi bật.
- Chỉ việc chuẩn bị đón khách được chuyển sớm. Giữ quy tắc gộp 4 giờ và việc sau trả phòng. Không xếp dọn trong booking thật hoặc khoảng khóa đang hiệu lực; khóa đã hủy không chặn dọn.
- Ưu tiên việc cùng giờ ít linh hoạt hơn, tránh trùng người và cùng phòng, đảm bảo đủ thời lượng trước nhận khách. Nếu không xếp được: giữ giờ gốc, báo cần phân công thủ công trên lịch và văn bản sao chép. Các việc ngoài giờ làm vẫn giữ và báo; không tự xóa việc ban đêm.
- Migration `20261013013412_AddHousekeepingStaggerSettings` chỉ thêm cấu hình vào `properties`; không cập nhật booking/payment/khoảng khóa. Áp dụng migration trước deploy; bật tính năng sau khi xác nhận giờ làm việc.
- Nghiệm thu: phòng khóa kết thúc 10:00 có thể dọn 10:00; kết thúc 10:30 không được chuyển sớm; khách còn ở, hết giờ làm, deadline ngắn, hai lao công, qua ngày, cấu hình tắt, không mất/trùng công việc và sao chép cảnh báo.

## Feedback vận hành 17/10/2026 — 1.8.5

- [x] Source: lịch công khai mobile nhiều phòng có cấu hình quay lại; Telegram theo sự kiện; Tom Select tìm khách; khóa nhanh vô thời hạn; lịch sử thao tác; link CalendarV2.
- [x] Kiểm chứng cục bộ: migration trên PostgreSQL test, 47 .NET tests, 30 JS tests và Chrome mobile/desktop fixtures.
- [ ] Publish bundle mới, migration/deploy production và UAT điện thoại/Telegram thật. Xem [bàn giao](OPERATIONS-FEEDBACK-2026-10-17.md).

## Dọn phòng và kiểm kê ngày — 1.8.6 (17/10/2026)

- [x] Source: Văn bản mặc định, bỏ tab tình trạng, filter đèn; giải thích màu và ưu tiên ghi chú; thống kê toàn cơ sở theo ngày có chi tiết.
- [x] Ngưỡng khung bị lấn còn tối đa 120 phút áp dụng API/public, cấu hình DB 0–720; migration AddPublicMinimumRemainingMinutes.
- [x] 61 .NET tests, 102 JavaScript tests và Chrome fixture mobile/desktop đạt.
- [ ] Publish/migrate/deploy và UAT tài khoản/điện thoại thật. Xem [bàn giao](HOUSEKEEPING-CALENDAR-FEEDBACK-2026-10-17.md).

## Báo cáo theo quyền, danh sách nợ và màu — 1.8.7

- [x] Source: Admin tổng hợp tất cả/một chi nhánh trên Tổng quan/Báo cáo/Excel; non-Admin chỉ ngữ cảnh chi nhánh được cấp, chặn scope mở rộng server-side.
- [x] Nợ ưu tiên hơn manual/ghi chú; bỏ màu riêng hoàn tất, giữ màu cấu hình. Danh sách Còn nợ có ngày nhận phòng/tìm kiếm/sắp xếp/tổng theo lọc.
- [x] Build Web, 29 .NET tests, 107 JavaScript tests, Chrome fixtures mobile/desktop đạt. Không thêm schema/migration.
- [ ] Publish/deploy và UAT tài khoản/iPhone thật. Xem [bàn giao](REPORTING-SCOPE-DEBT-COLORS-2026-10-17.md).

## Lịch public mobile — 1.8.8 (19/10/2026)

- [x] Sửa header/body lệch cột do width:max-content; giữ cuộn ngang và ngày sticky. Browser regression 5 trường hợp và 107 JS tests đạt.
- [ ] Deploy và kiểm tra Safari/iPhone thật. Xem [chi tiết](PUBLIC-CALENDAR-MOBILE-ALIGNMENT-2026-10-19.md).

## Nhãn giờ lịch public mobile — 1.8.9 (19/10/2026)

- [x] Mobile: cột giờ tối thiểu 128px, font 12px, padding 10px; không cắt/chồng nhãn, header/body thẳng hàng và cuộn ngang.
- [x] 107 JS tests và 7 browser cases (375/390/414px, desktop và mode cũ) đạt; ảnh trước/sau 390px lưu trong docs/qa.
- [ ] Deploy và kiểm tra iPhone/Safari thật. Xem [chi tiết](PUBLIC-CALENDAR-TIME-LABELS-2026-10-19.md).
