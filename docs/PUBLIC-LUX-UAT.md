# UAT giao diện trang khách 2026-11 ("lux")

Phạm vi: đổi giao diện Trang chủ, các trang Chính sách, modal FAQ và Tra cứu đơn theo mockup 2026-11 (xanh rêu đậm, nền kem, nhấn vàng, tiêu đề Playfair Display). Không đổi schema, API, quy tắc đặt phòng, lịch phòng hay modal đặt phòng.

## Thành phần

- `wwwroot/css/public-lux.css`: lớp giao diện mới, nạp **sau cùng** (cuối bundle trang chủ và sau `public-redesign.css` ở các trang khác), trước `custom.css` của cơ sở nên màu admin chỉnh riêng vẫn thắng. Áp dụng cho mọi cơ sở.
- `wwwroot/fonts/playfair/`: Playfair Display 400 thường + nghiêng, tự host, subset Latin + tiếng Việt, giấy phép OFL. Không gọi Google Fonts.
- `Features/Site/PublicTitleAccent.cs`: in nghiêng màu nhấn 2 chữ cuối của tiêu đề CMS (chỉ bọc `<em>`, chữ lưu trong CMS không đổi; inline editor đọc `innerText` nên vẫn lưu đúng).
- `Pages/Shared/_LuxHeroDeco.cshtml`, `_LuxBookingSteps.cshtml`: trăng/vòm trang trí khi hero không có ảnh, huy hiệu xoay "Đặt phòng trực tiếp", khối "Bốn bước" (chữ cố định) hiện ngay sau khối Phòng.
- `Pages/CustomPage.cshtml` + `wwwroot/js/pages/public-policy-page.js`: 5 trang chính sách (`PublicPolicyLinks`) dùng chung bố cục hero tối, tab 5 chính sách, thẻ hotline, thẻ nội dung. JS tự chia nội dung soạn tay thành mục đánh số: tiêu đề h1–h3 hoặc dòng chỉ in đậm; "I., II." là mục lớn, "1., 2." là tiêu đề con. Không lưu gì; khi người có quyền sửa trang đang đăng nhập (inline editor) thì để nguyên HTML.
- `_PublicFaqModal.cshtml` + `public-faq-modal.js`: modal FAQ có ô tìm kiếm (không dấu), câu hỏi đánh số, khối "Chưa có câu trả lời?" (hotline, Facebook nếu có).
- `Pages/Booking/Lookup.cshtml` + `booking-lookup.js`: hero "Đơn của bạn đang ở đâu?", dòng tiến trình Tạo đơn → Đã thanh toán → Đã xác nhận → Nhận phòng theo trạng thái thật (ẩn khi Hủy/Không đến), 3 thẻ hướng dẫn. Giữ tra cứu qua email, số tiền, hướng dẫn phòng + PDF.

## Hiệu năng

- Không có màn "rèm" khi mở trang, không parallax bằng JavaScript. Hiệu ứng chỉ bằng CSS: chữ hero hiện dần và hiện khi cuộn chỉ trên màn hình ≥ 900px; thanh tiến độ cuộn dùng `animation-timeline` (trình duyệt không hỗ trợ thì không hiện).
- `prefers-reduced-motion: reduce` tắt mọi chuyển động.

## Kiểm thử

1. Trang chủ desktop và 390px: hero tối, tiêu đề có chữ nghiêng vàng, nút vàng; không có dải trắng giữa header và hero.
2. Lịch phòng: chọn khung trống → thanh tạm tính → "Đặt phòng" mở đúng modal đặt phòng như trước.
3. Khối Phòng nền tối, thẻ ảnh dọc; nút "Đặt ngay" vẫn về lịch phòng; phòng khóa vẫn hiện "Tạm ngừng nhận đặt phòng".
4. Chính sách: tab đánh dấu đúng trang hiện tại; nội dung soạn bằng dòng in đậm "I. …/1. …" được chia mục 01, 02…; câu mở đầu trùng với mô tả không lặp lại. Đăng nhập admin: HTML để nguyên để sửa.
5. FAQ: gõ "gia phong" lọc ra câu về giá; gõ chuỗi không có → hiện thông báo không tìm thấy.
6. Tra cứu: đơn Đã xác nhận có thanh toán hiện 3 bước xong; đơn Hủy không hiện tiến trình.
