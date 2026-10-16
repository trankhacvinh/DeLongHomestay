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
