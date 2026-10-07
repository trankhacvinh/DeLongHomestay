# UAT giao diện public mới (2026-10)

Phạm vi: chỉ đổi giao diện trang khách (header, trang chủ, lịch phòng, footer). Không đổi schema, API, quy tắc đặt phòng hay dữ liệu CMS.

## Thành phần

- `wwwroot/css/public-redesign.css`: lớp giao diện mới. Nạp sau `hospitality-shell.css`; ở chế độ Tùy chỉnh tự do, `custom.css` của cơ sở nạp sau nên vẫn ghi đè được.
- `wwwroot/fonts/brand/`: Be Vietnam Pro (400/600/700/900), tự host, subset Latin + tiếng Việt, giấy phép OFL đi kèm. Một font duy nhất, thang chữ cố định (`--dl-h1`, `--dl-h2`, `--dl-h3`), tiêu đề không viết hoa toàn bộ và line-height ≥ 1.15 để dấu tiếng Việt không đè lên dòng khác.
- **Chế độ giao diện** (Quản trị › Trang chủ chung): lưu trong HomeSection ẩn `__PublicTheme`, không đổi schema. Mặc định là **Giao diện chuẩn**.
- `SiteContentService.GetPublicContactsAsync`: danh sách liên hệ của các cơ sở đang hoạt động, lấy từ `PropertySiteSettings` (logo, địa chỉ, hotline, Zalo, Facebook, Google Maps). Cache dưới tag `public-content`, nên tự làm mới khi lưu cài đặt website.
- `public-availability-calendar.js`: lịch nhiều phòng và danh sách tick chọn cơ sở. Logic chọn khung liên tiếp giữ nguyên.

## Kiểm thử

1. **Trang chủ, desktop ≥ 1080px.**
   - Hero có tiêu đề rõ ràng theo thang chữ chuẩn, ảnh dạng vòm và nhãn tròn "đặt nhanh từ …K" khi có giá.
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
7. **Cơ sở có `custom.css`:** chỉ áp dụng ở chế độ Tùy chỉnh tự do.
8. **Trợ năng.**
   - Mọi nút và link có focus-visible và vùng chạm tối thiểu 40–44px.
   - Dải chữ chạy và hiệu ứng tắt khi bật `prefers-reduced-motion`.

## Chế độ giao diện

| | Giao diện chuẩn (mặc định) | Tùy chỉnh tự do |
|---|---|---|
| Nội dung: chữ, ảnh, khối, thứ tự, menu | Sửa được | Sửa được |
| Cỡ chữ / độ rộng / khoảng cách / căn lề chỉnh tay (`cp-*`) | Bỏ qua (server không render class) | Áp dụng |
| Layout biến thể của khối (`variant-*`) | Luôn dùng bố cục chuẩn | Áp dụng |
| Màu và hàng footer từ trình thiết kế header/footer | Bỏ qua | Áp dụng |
| `custom.css` của cơ sở | Không nạp | Nạp |
| Tiêu đề và chữ trong row builder / rich text | Đưa về thang chữ chuẩn | Giữ nguyên |

9. Bật Giao diện chuẩn: trang public không còn chữ quá to/quá nhỏ do chỉnh tay; người quản trị đang sửa trang thấy dải thông báo ở đầu trang.
10. Chuyển sang Tùy chỉnh tự do: mọi tùy chỉnh cũ hiện lại như trước, không mất dữ liệu.

## Khôi phục giao diện mặc định

Nút **Khôi phục mặc định…** trong Quản trị › Trang chủ chung (có bước xác nhận ngay trên trang):

11. Bấm và xác nhận: trang chủ chung có đúng bố cục chuẩn theo bản thiết kế đã duyệt — Hero, Lịch phòng, Các phòng (kèm dải chữ chạy), Các cơ sở (tự ẩn khi chỉ có 1 cơ sở), Cách đặt phòng (4 thẻ); Blog và footer liên hệ hiển thị như cũ. Chế độ giao diện chuyển về Giao diện chuẩn.
12. Các khối cũ vẫn còn trong danh sách, ở trạng thái ẩn, tên bắt đầu bằng `[Bản cũ]`; bật lại hoặc xóa tùy ý.
13. Trang tự tạo (custom pages), trang riêng từng cơ sở, menu, thông tin liên hệ và dữ liệu đặt phòng không thay đổi.
14. Trong Giao diện chuẩn, khối "Nội dung + điểm nổi bật" hiển thị thành dải thẻ: mỗi dòng điểm nổi bật viết dạng `Tiêu đề — mô tả` thành một thẻ.

## Bổ sung 2026-10-07

15. **Màu nền beige**: nền trang `#f8f1e6`, nền phụ `#f1e6d4`, thẻ `#fffcf6`.
16. **Màu thẻ phòng**: mặc định lấy bảng màu 6 tông theo thứ tự thẻ (`PublicThemeStore.DefaultRoomPalette`). Quản trị › Trang chủ chung › **Màu thẻ phòng**: chọn màu riêng cho từng phòng, bấm "Mặc định" để bỏ. Lưu trong `__PublicTheme` (chỉ nhận mã `#rrggbb`).
17. **Ảnh phòng**: thẻ phòng có ảnh lớn hơn; bấm vào ảnh mở lightbox xem toàn bộ ảnh phòng (bản lớn `LargePath`), chuyển ảnh bằng nút, phím ← → hoặc vuốt; vuốt ngang trên thẻ vẫn đổi ảnh như cũ.
18. **Footer liên hệ**: có địa chỉ thì hiện bản đồ Google nhúng thật kèm nút "Chỉ đường". Cơ sở chưa nhập gì: khách thấy liên kết tới phòng của cơ sở, quản trị viên thấy liên kết "Nhập thông tin liên hệ".
19. **Nhập liên hệ dễ hơn**: Zalo nhận số điện thoại (tự đổi thành `https://zalo.me/…`), Facebook/Maps nhận link không có `https://`, Google Maps nhận cả mã nhúng `<iframe …>`. Trước đây các giá trị này làm cả form lưu thất bại.
20. **Lịch nhiều cơ sở**: tick 2+ cơ sở, lịch tải trong vài giây, cuộn mượt; đổi lựa chọn cơ sở liên tục không bị treo; tải lại trang hoạt động bình thường.
21. Đã bỏ dải thông báo "Đang dùng Giao diện chuẩn" ở đầu trang.

## Ghi chú vận hành

- Lịch nhiều phòng gọi `/api/public/rooms-availability?siteSlug=…&roomIds=…` **một lần cho mỗi cơ sở** mỗi lượt tải (tối đa 24 phòng/lần); việc giải phóng giữ chỗ hết hạn chạy một lần cho mỗi cơ sở thay vì mỗi phòng. Xem một phòng (mobile) vẫn dùng `/api/public/room-availability`.
- Mọi request lịch bị hủy khi đổi phòng/cơ sở và tự hết hạn sau 20 giây; hàng ngày đã dựng được giữ lại khi cuộn; formatter giờ/tiền tạo một lần.
- Mặc định tick tất cả cơ sở khi tổng số phòng ≤ 12; nếu nhiều hơn, chỉ tick cơ sở đầu tiên.
