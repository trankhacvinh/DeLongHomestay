# Public Custom Pages / Page Builder — UAT

Branch: `feat/custom-pages-builder`  
PR: #41

## Mục tiêu

Cho phép tạo các trang nội dung riêng như Giới thiệu, Liên hệ, Chính sách, Dịch vụ, Ưu đãi… và thiết kế bằng chính Visual Editor / Row Builder hiện tại. Không tạo editor thứ hai và không cần migration database mới.

## 1. Admin → Website → Trang nội dung

- Mở Admin và vào `Website → Trang nội dung`.
- Sidebar phải có mục `Trang nội dung` và được highlight khi đang ở trang này.
- Admin có thể chuyển giữa `Trang chung` và từng cơ sở.
- Manager chỉ thấy / quản lý các cơ sở mình có quyền.
- Thống kê hiển thị tổng trang, đã xuất bản và bản nháp.

## 2. Tạo trang

Tạo lần lượt hoặc chọn một vài mẫu:

- `Trang trống`.
- `Giới thiệu`.
- `Liên hệ`.
- `Landing ưu đãi`.

Kiểm tra:

- Để slug trống: hệ thống tự sinh slug từ tên trang, kể cả tên tiếng Việt.
- Slug chỉ dùng chữ thường, số, dấu gạch ngang.
- Thử slug `rooms`, `booking`, `blog`, `admin`: phải bị chặn vì là đường dẫn hệ thống.
- Tạo hai trang cùng slug trong cùng scope: trang thứ hai phải bị chặn.
- Trang global có URL `/{slug}`.
- Trang cơ sở có URL `/h/{siteSlug}/{slug}`.

## 3. Draft / Published

- Tạo một trang ở trạng thái `Bản nháp`.
- Khi đang đăng nhập Admin/Manager có quyền, nút `Xem` / `Thiết kế` vẫn mở được trang và phải thấy nhãn `Bản nháp`.
- Mở cùng URL ở tab ẩn danh: phải 404 khi chưa xuất bản.
- Bản nháp phải có robots `noindex,nofollow` khi người có quyền preview.
- Bật `Xuất bản`, lưu lại rồi mở tab ẩn danh: trang phải public.

## 4. Visual Editor trên Custom Page

Từ Admin bấm `Thiết kế`:

- URL mở với `?edit=1` và tự bật chế độ `Chỉnh sửa trang`.
- Toolbar phải hiển thị tên Custom Page thay vì `Trang chủ chung` / tên cơ sở.
- Có nút `Quản lý trang` quay lại `Admin → Website → Trang nội dung`.
- Có thể thêm / sửa / nhân bản / ẩn / xóa / kéo sắp xếp các block chuẩn:
  - Hero
  - Availability Search
  - Room Grid
  - Feature Grid
  - FAQ
  - Location
  - Policy Grid
  - Rich Text
  - CTA
  - Branch Grid ở trang global.
- Refresh trang: thứ tự và nội dung phải giữ nguyên.
- Thay đổi Custom Page không được làm thay đổi Homepage.

## 5. Inline Editing

Bật `Chỉnh sửa trang` trên Custom Page:

- Click Hero heading / body → sửa trực tiếp → lưu.
- Click CTA button → sửa text và URL.
- Click FAQ / Policy / Feature text → sửa trực tiếp.
- Đổi ảnh Hero / Feature bằng inline image flow.
- Sau inline save, bấm `Nâng cao` ngay: dữ liệu mới không bị ghi đè bởi snapshot cũ.

## 6. Row / Column Builder

Trên Custom Page:

- `＋ Row / cột` phải xuất hiện khi bật chỉnh sửa.
- Tạo Row 1–4 cột.
- Thêm Heading, Text, Image, Button, Divider, Spacer, HTML.
- Copy/Paste element và Copy/Paste Row.
- Undo / Redo.
- Desktop / Tablet / Mobile settings vẫn hoạt động độc lập.
- Save rồi refresh: Row phải còn đúng layout và responsive config.
- Row Template Library hiện tại vẫn dùng được.

## 7. Advanced Elements

Trong Row trên Custom Page, thử các element nâng cao đã có:

- Icon + nội dung.
- Video.
- Bản đồ.
- Accordion.
- Đánh giá khách.
- Ưu đãi / Giá.

Kiểm tra inline edit, duplicate, drag/move và responsive giống Homepage.

## 8. Header / Footer kế thừa

- Custom Page phải dùng Header hiện tại của đúng scope.
- Sticky Header và style sau khi cuộn phải giống các trang khác.
- Footer Builder hiện tại phải xuất hiện đúng.
- Inline editing của Footer vẫn hoạt động trên Custom Page.
- Sửa Header/Footer từ Custom Page phải thay đổi shell của scope, không tạo Header/Footer riêng cho từng Custom Page.

## 9. Media Library

- Chọn / upload ảnh Hero, Row, OG image trên Custom Page.
- Vào `Admin → Website → Media Library`.
- Ảnh đang được tham chiếu trong Custom Page phải được tính là đang sử dụng và safe-delete không được xóa nhầm.
- Xóa reference khỏi Custom Page rồi lưu; nếu không còn nơi dùng, media mới có thể trở về trạng thái `Chưa dùng`.

## 10. Smart Link Picker

Sau khi đã có ít nhất một Custom Page đã xuất bản và không bật `Ẩn khỏi danh sách điều hướng`:

- Mở Menu / Header CTA / Footer Button hoặc link picker hiện tại.
- Phải có nhóm `Trang` / `Trang nội dung`.
- Tìm theo tên Custom Page.
- Chọn trang → URL phải tự điền đúng global/property scope.
- Trang bật `Ẩn khỏi danh sách điều hướng` không xuất hiện trong picker nhưng URL public vẫn dùng được nếu trang đã xuất bản.

## 11. Duplicate Page

- Bấm `Nhân bản` một trang có nhiều section / Row.
- Bản sao phải:
  - có slug mới;
  - là `Bản nháp`;
  - mặc định ẩn khỏi điều hướng;
  - giữ nội dung nhưng dùng ID page/section mới.
- Sửa bản sao không được ảnh hưởng trang gốc.

## 12. SEO / sitemap

- Điền SEO title, meta description, OG image.
- Public page phải render title/meta/OG/canonical tương ứng.
- Trang Published phải xuất hiện trong sitemap đúng scope.
- Trang Draft không được xuất hiện trong sitemap.
- `Ẩn khỏi điều hướng` không đồng nghĩa noindex: nếu Published, trang vẫn có thể nằm trong sitemap.

## 13. Regression Homepage

- Homepage global và homepage cơ sở vẫn hiển thị các section cũ đúng thứ tự.
- `__CustomPage` không được xuất hiện như một Home Section.
- Reorder Homepage không được báo sai số lượng vì Custom Page metadata.
- Gallery / Blog / Room / Booking / Header / Footer / Media Library vẫn hoạt động bình thường.

## 14. Database

- PR #41 không thêm table / column và không có EF migration mới.
- Không cần chạy `dotnet ef database update` chỉ vì PR này.
