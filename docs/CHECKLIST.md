# Checklist nghiệm thu

## Pricing V2

- [x] Pricing được tính lại phía server khi tạo booking.
- [x] Voucher không giảm phụ thu ngày đặc biệt.
- [x] Migration giữ booking cũ và mặc định giá cuối tuần bằng giá ngày thường.
- [ ] Nhập bảng giá 780.000–990.000 VND cho từng phòng/cơ sở trên môi trường cần test.
- [ ] Chạy UAT theo `docs/PRICING-UAT.md` sau khi migrate database test.

## Foundation

- [x] `dotnet restore` pass trên GitHub Actions.
- [x] `dotnet build -c Release` pass trên GitHub Actions.
- [x] `dotnet test -c Release` pass trên GitHub Actions.
- [x] JavaScript page modules được `node --check` trong CI.
- [ ] PostgreSQL local có database `delong_dev`.
- [ ] Connection string thật nằm trong User Secrets, không nằm trong Git.
- [x] Initial migration được EF Core sinh và commit.
- [x] Migration có `btree_gist` + exclusion constraint chống overlap booking.
- [ ] Apply InitialCreate thành công trên PostgreSQL local.
- [ ] Seed tạo De Long + 6 phòng đúng rates.
- [ ] Seed admin từ User Secrets.
- [ ] Login/logout Identity hoạt động với PostgreSQL local.
- [x] API kiểm tra role + `UserPropertyAccess`.

## Vue/API mẫu Rooms

- [ ] Trang `/Admin/Rooms` render initial data từ PostgreSQL local.
- [x] Vue mount trong page scope, không SPA.
- [x] Search/filter không reload.
- [x] Thêm phòng dùng modal + POST API.
- [x] Sửa phòng dùng modal + PUT API.
- [x] Ngừng phòng dùng confirm modal + DELETE API.
- [x] Mutation API dùng antiforgery token.
- [x] API validation dùng ProblemDetails.
- [ ] Kiểm tra trực tiếp loading state/toast trên browser với PostgreSQL local.

## Customer + Booking

- [x] Ghi chú khách dùng chung giữa hồ sơ và chi tiết booking.
- [x] Danh sách đen có lý do, trạng thái chặn độc lập và lọc/cảnh báo trong danh sách khách.
- [x] Chặn server-side đăng ký, đăng nhập và đặt phòng theo số điện thoại hoặc email.

- [x] Customer entity/service/API.
- [x] Nhận diện khách cũ theo normalized phone trong cùng cơ sở.
- [x] Booking status/rules.
- [x] Create booking qua API.
- [x] C# conflict check.
- [x] Đơn Completed giữ khoảng đã đặt trong lịch khách và guard tạo/sửa/chuyển/import; kiểm thử PostgreSQL gồm hoàn tất sớm, qua đêm và buffer dọn phòng.
- [x] Ô lịch khách bỏ giá riêng, giữ tổng tiền khi chọn giờ.
- [x] Lịch V1/V2 cho thu bổ sung khi sửa đơn và trong chi tiết đơn hoàn tất; append Payment, tải lại số dư và làm mới lịch.
- [ ] UAT thu bổ sung trên di động sau deploy; đối soát đơn BKYZEWBEVV trước khi ghi nhận tiền thật.
- [ ] UAT bản sửa Completed và lịch không hiện giá trên production sau deploy.
- [x] PostgreSQL overlap guard cho Held/Confirmed/CheckedIn.
- [x] Conflict API trả `409 ProblemDetails`.
- [x] Calendar Razor + Vue đọc dữ liệu server.
- [x] Click ô trống mở modal tạo booking.
- [x] Preset rate tự điền giờ/giá nhưng cho sửa giờ thực tế.
- [x] Booking detail/status actions không reload.
- [x] Trang Booking search/filter.
- [x] Trang Customers add/edit modal.
- [ ] Edit booking room/time/amount.
- [ ] Audit log booking.
- [ ] Áp dụng migration `AddBookingEmailAndTelegramNotifications` trước khi bật email/Telegram.
- [ ] Danh sách email nội bộ nhận đúng thông báo booking mới; lỗi SMTP được retry và không làm hỏng booking.
- [ ] Telegram bot gửi đúng nhóm đã cấu hình; bot token không hiển thị lại và nút gửi thử báo đúng kết quả.
- [ ] Booking `Confirmed` có email khách nhận hướng dẫn check-in; chi tiết booking/lịch hiển thị trạng thái và gửi lại được.
- [ ] Booking chuyển `Cancelled` xếp đúng một email báo hủy cho mỗi lần hủy và dùng snapshot mẫu tại thời điểm xếp hàng.
- [ ] Áp dụng migration `AddVoucherSystem` trước khi bật Voucher ở production.
- [ ] UAT voucher theo `docs/VOUCHER-UAT.md`, gồm tranh lượt cuối và voucher 100% không tạo Pay2S.
- [ ] Mẫu email nội bộ/check-in/hủy thay đúng các biến cho phép; tiêu đề chặn xuống dòng và nội dung quá dài.
- [ ] Editor HTML email, bảng click-copy biến và preview hoạt động trên desktop/mobile; script/event attribute nguy hiểm bị loại khi lưu.
- [ ] Integration test race condition trên `delong_test`.
- [ ] Kiểm tra calendar trực tiếp trên browser với PostgreSQL local.

## Trước Payments milestone

- [ ] CurrentProperty resolver/selector thay seed property ID trong các PageModel.
- [ ] Chốt logic thanh toán/cọc/hoàn tiền.
- [ ] Thêm Payment ledger thay vì số tiền thanh toán nằm trong Booking.
- [x] Check-out tự động chuyển booking hoàn tất và phòng sang trạng thái Bẩn; có thể tắt theo cơ sở.
- [ ] Áp dụng migration `AddRoomConditionReports`, sau đó migration bổ sung rating/video trước khi mở chức năng báo cáo phòng.
- [ ] Kiểm tra nhân viên chỉ thấy/tạo báo cáo tại cơ sở được cấp quyền.
- [x] Media chỉ sửa nội dung/ảnh phòng, Gallery, Media Library và Blog tại các cơ sở được gán; không truy cập booking, giá, tài chính hoặc cấu hình hệ thống.
- [ ] Chụp/quay trực tiếp và chọn nhiều ảnh/video trên iPhone/Android; xác nhận ảnh xoay đúng, WebP tải nhanh và video phát được.
- [ ] Xác nhận báo cáo cần ít nhất 1 file, không giới hạn số file nghiệp vụ, video tối đa 250 MB/file và không mất nội dung khi server trả lỗi validation.
- [ ] Xác nhận tag mẫu tự điền nội dung, điểm 1–5 sao và trạng thái Mới báo/Đang xử lý/Đã hoàn thành hiển thị đúng trong bảng.

## Lịch khóa phòng theo thời gian

- [ ] Production áp dụng migration `20261009142839_AddRoomBookingSchedules` bằng bundle mới.
- [ ] UAT: nhiều phòng, qua đêm, nhiều khung/ngày, sửa/kết thúc sớm và khóa không thời hạn cũ.
- [ ] UAT: vùng khóa trên mobile, khách đặt ngoài vùng khóa, QR/đơn cũ vẫn hoạt động.

- [ ] AI khóa phòng: deploy cùng migration lịch khóa, thử provider thật, preview/checkbox đơn trùng và giờ chi nhánh trên mobile; xác nhận đơn/payment cũ giữ nguyên.

### Kiểm tra dữ liệu / AI tạo booking (2026-10-11)

- [x] Migration chỉ thêm bảng phát hiện, không sửa tiền/đơn cũ.
- [x] Dedup quét, reopen khi đổi bằng chứng, audit và ghi nhận xử lý có lý do.
- [x] Phân quyền Admin/Manager + chi nhánh và antiforgery; AI quản trị duyệt trước tạo booking.
- [ ] Production migration/deploy + UAT browser/mobile, provider AI thật. Xem DATA-QUALITY.md.

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

## Kho chung và tiền mặt (1.8.15 — 27/10/2026)

- [x] Tồn liên tháng, decimal, chống âm theo ngày lùi, xuất đồng thời, request key retry và hủy có lý do trên PostgreSQL test.
- [x] Cash payment thu/hoàn, thu ngoài, expense có phòng/ghi chú; void loại khỏi tổng; không cộng chuyển khoản; Admin all/one và non-Admin scope.
- [x] HTTP role/property/warehouse authorization và antiforgery; migration rehearsal bảo toàn khoản chi cũ; 20 .NET tests tập trung đạt.
- [x] 4 JavaScript tests và 4 Chrome fixtures mobile/desktop đạt; Excel numeric và chống công thức từ nội dung nhập.
- [ ] Publish/efbundle mới, backup và migration production trước khi chạy app; khởi tạo kho/gán quyền và UAT Home/iPhone thật. Không chạy Down xóa dữ liệu đã phát sinh. Xem [bàn giao](INVENTORY-CASH-2026-10-27.md).

## Kho compact/import — 1.8.16 (28/10/2026)

- [x] Tự sinh mã và giữ mã khi sửa; case/space dedup tên/ĐVT, import preview/commit và retry đồng thời không trùng tồn.
- [x] Không import đè tồn đã phát sinh, không đổi quy cách/phiếu cũ; lỗi dòng chặn batch, stale/tampered token và quyền/CSRF được kiểm thử.
- [x] 26 .NET tests, 5 JS tests, 8 Chrome cases ở 375/390/414/1280px; ảnh trước/sau và modal trong docs/qa.
- [ ] Deploy cùng migration `20261028032715_AddInventoryImportReceipts`, UAT Files/Safari/iPhone và file thực. Xem [chi tiết](INVENTORY-IMPORT-COMPACT-2026-10-28.md).

## Upload CCCD / khóa khung — 1.8.17

- [x] Ảnh camera lớn resize ngay khi decode, EXIF, queue/encoder timeout và cleanup; preview không decode lại ảnh gốc. Upload tiến độ/idle timeout và retry không tạo booking mới.
- [x] Hai scope khóa, qua đêm, xác nhận đơn trùng; mở một interval, giữ khóa khác, quyền chi nhánh/CSRF, request key/concurrency/audit.
- [ ] Deploy/UAT thiết bị/mạng thật. Source contract QR cũ lỗi đối chiếu chuỗi được tách rõ, không thay đổi QR trong bản sửa này. Xem [bàn giao](MOBILE-IDENTITY-AND-ROOM-LOCKS-2026-10-28.md).

## Giờ gợi ý booking Admin — 1.8.18

- [x] Dropdown và form dùng giờ trả thực tế + thời lượng dọn của cơ sở; giữ giá/giờ kết thúc, xét khóa/qua đêm và không ghi đè giờ nhập tay.
- [x] 22 .NET tests trên PostgreSQL test, 28 JS tests và Chrome fixtures 390/1280px đạt; không thêm migration.
- [ ] Deploy và UAT điện thoại/dữ liệu thực. Xem [bàn giao](BOOKING-TIME-SUGGESTIONS-2026-10-28.md).

## Lượt tạo hôm nay — 1.8.19

- [x] Đếm theo ngày tạo/timezone chi nhánh, gồm mọi nguồn, độc lập bộ lọc; 9 JS tests và 2 Chrome fixtures đạt, build/test version đạt.
- [ ] Deploy/UAT website và nhân viên tạo booking. Xem [bàn giao](BOOKING-CREATED-TODAY-2026-10-28.md).

## Sửa khóa khung / click booking — 1.8.20

- [x] Báo/chặn khung đã hết giờ trước khi gửi; đổi scope xóa lỗi. Bấm booking khi khóa toàn phòng vẫn xem chi tiết.
- [x] 16 .NET, 22 JS tests và Chrome fixture 390/1280px đạt; không thêm migration.
- [ ] Deploy/UAT. Xem [bàn giao](ROOM-LOCK-CLICK-EXPIRY-2026-10-28.md).

## Bố cục thu bổ sung / lịch sử — 1.8.21

- [x] Grid thanh toán compact, lịch sử có lề/gap và metadata riêng; build, 9 JS tests và 4 Chrome fixtures đạt.
- [ ] Deploy/UAT. Xem [bàn giao](BOOKING-MODAL-SPACING-2026-10-28.md).

## Public performance — 1.8.22

- [x] Bundle CSS qua .NET SDK, WOFF2/fingerprint, core form DOM ready; Release publish đã kiểm tra đủ tài nguyên.
- [x] 19 .NET tests tập trung, 149 JS unit tests, 4 replay cases và test browser ảnh treo; mobile CLS replay về 0, layout hero giữ nguyên.
- [ ] Deploy, Lighthouse lại trên production cùng điều kiện, UAT thiết bị/phiên editor thật. Xem [bàn giao](PUBLIC-PERFORMANCE-OPTIMIZATION-2026-10-28.md).


## Kho và tiền mặt — 1.8.23

- [x] Schema cộng thêm và kiểm thử nơi nhận, retry, số dư, phân quyền; 12 PostgreSQL, 152 JS, 8 màn hình Chrome qua.
- [ ] Backup, xác minh database đích, publish/bundle mới và migrate production (InventoryImportReceipts + InventoryRecipientNames).
- [ ] UAT home1 xuyên tháng, hết hàng/mobile và tiền mặt tháng 09/2026 trên VPS. Xem [bàn giao](INVENTORY-RECIPIENTS-CASH-2026-10-29.md).
