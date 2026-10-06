# UAT giao diện public mới (2026-10)

Phạm vi: chỉ đổi giao diện trang khách (header, trang chủ, lịch phòng, footer). Không đổi schema, API, quy tắc đặt phòng hay dữ liệu CMS.

## Thành phần

- `wwwroot/css/public-redesign.css`: lớp giao diện mới. Nạp sau `hospitality-shell.css` và trước `custom.css` của từng cơ sở, nên CSS tùy biến của cơ sở vẫn ghi đè được.
- `wwwroot/fonts/brand/`: Be Vietnam Pro (400/600/700/900) và Playfair Display Italic. Cả hai tự host, subset Latin + tiếng Việt, giấy phép OFL đi kèm.
- `SiteContentService.GetPublicContactsAsync`: danh sách liên hệ của các cơ sở đang hoạt động, lấy từ `PropertySiteSettings` (logo, địa chỉ, hotline, Zalo, Facebook, Google Maps). Cache dưới tag `public-content`, nên tự làm mới khi lưu cài đặt website.
- `public-availability-calendar.js`: lịch nhiều phòng và danh sách tick chọn cơ sở. Logic chọn khung liên tiếp giữ nguyên.

## Kiểm thử

1. **Trang chủ, desktop ≥ 1080px.**
   - Hero có tiêu đề in hoa cỡ lớn, ảnh dạng vòm và nhãn tròn "đặt nhanh từ …K" khi có giá.
   - Thứ tự các khối vẫn theo CMS.
2. **Lịch phòng khi có nhiều cơ sở, desktop.**
   - Danh sách tick hiện tên cơ sở kèm số phòng.
   - Bỏ tick một cơ sở thì cột phòng của cơ sở đó biến mất và lịch tải lại.
   - Lựa chọn tick được nhớ cho lần sau (localStorage).
   - Không tick cơ sở nào thì hiện thông báo yêu cầu chọn.
3. **Lịch nhiều phòng.**
   - Hàng là ngày, cuộn xuống tự tải thêm 14 ngày.
   - Cột là các khung giờ của từng phòng, nhóm theo phòng và theo cơ sở.
   - Cột ngày và hàng tiêu đề đứng yên khi cuộn ngang/dọc.
   - Hôm nay và cuối tuần có màu nền riêng.
4. **Quy tắc chọn khung** (không đổi, xem `PUBLIC-CONSECUTIVE-SLOTS-UAT.md`).
   - Bấm một khung trống: các khung của phòng khác mờ đi, khung kế tiếp hợp lệ có dấu `+`.
   - Bấm khung của phòng khác: không thêm vào lựa chọn, hiện thông báo phải Xóa trước.
   - Bấm khung cuối đã chọn để bớt.
   - Thanh tạm tính hiện tên phòng, số khung, giờ nhận/trả và tổng tiền.
   - Bấm "Đặt phòng" mở đúng modal booking hiện tại.
5. **Mobile ≤ 760px.**
   - Lịch về chế độ một phòng: thanh chuyển phòng ‹ › duyệt các phòng của những cơ sở đang tick.
   - Không có thanh cuộn ngang ngoài trang ở 375px.
6. **Footer.**
   - Website tổng: mỗi cơ sở đang hoạt động có một thẻ gồm logo (hoặc chữ viết tắt), tagline, địa chỉ, hotline (`tel:`), Zalo, Facebook và ô "Chỉ đường" mở Google Maps.
   - Website của từng cơ sở: chỉ hiện thẻ của cơ sở đó.
   - Trường nào trống trong Cài đặt website thì phần tương ứng tự ẩn.
7. **Cơ sở có `custom.css`:** CSS của cơ sở vẫn ghi đè được giao diện mới.
8. **Trợ năng.**
   - Mọi nút và link có focus-visible và vùng chạm tối thiểu 40–44px.
   - Dải chữ chạy và hiệu ứng tắt khi bật `prefers-reduced-motion`.

## Ghi chú vận hành

- Lịch nhiều phòng gọi `/api/public/room-availability` cho từng phòng đang hiển thị, tối đa 4 request song song mỗi lượt tải.
- Nếu số phòng tăng lớn (vài chục phòng), nên cân nhắc một endpoint gộp nhiều phòng. Mặc định chỉ tick tất cả cơ sở khi tổng số phòng ≤ 12; nếu nhiều hơn, chỉ tick cơ sở đầu tiên.
