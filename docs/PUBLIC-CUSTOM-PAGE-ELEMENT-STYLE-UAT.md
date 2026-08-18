# Custom Page Element Style — UAT

Branch: `feat/custom-pages-builder`  
PR: #41

## Mục tiêu

Cho phép chỉnh trực tiếp **căn lề, cỡ chữ, độ rộng của text/button** và **căn + kích thước ảnh** trên Custom Page mà không phải chuyển mọi nội dung sang Row Builder.

Các setting được lưu trong `_visual` của chính `ContentJson` section; không có migration DB mới.

## 1. Text / Heading

- Mở một Custom Page bằng `Thiết kế` và bật `Chỉnh sửa trang`.
- Click tiêu đề Hero.
- Thanh inline phải có nút `↔ Kiểu`.
- Bấm `Kiểu`: nội dung đang sửa được lưu trước, sau đó mở panel kiểu phần tử.
- Thử `Căn`: Tự động / Trái / Giữa / Phải.
- Thử `Cỡ chữ`: Mặc định / Rất nhỏ / Nhỏ / Vừa / Lớn / Rất lớn / Hero.
- Thử `Độ rộng`: Mặc định / Gọn / Vừa / Rộng / Full.
- `Lưu kiểu` → trang reload nhưng vẫn giữ chế độ chỉnh sửa và vị trí cuộn.
- Refresh thêm một lần: style phải còn nguyên.

## 2. Full width Hero

- Chọn Hero title → `Độ rộng = Full`.
- Hero phải chuyển sang hàng rộng để title có thể dùng toàn bộ chiều ngang nội dung thay vì bị khóa trong cột hẹp.
- Chọn `Căn = Giữa` + `Cỡ = Hero` để kiểm tra layout lớn.
- Đưa về `↺ Mặc định` và lưu: layout phải trở lại theo theme gốc.

## 3. Button

- Click CTA hoặc nút Hero.
- `Kiểu` → thử căn trái / giữa / phải.
- Thử `Độ rộng = Full`: nút phải kéo rộng trong vùng khả dụng, text vẫn căn hợp lý.
- Link của nút không được thay đổi khi chỉ chỉnh kiểu.

## 4. Ảnh

- Hover ảnh Hero / Feature.
- Bên cạnh `Đổi ảnh` phải có `↔ Kiểu ảnh`.
- Thử `Căn`: trái / giữa / phải.
- Thử kích thước: Gọn / Vừa / Rộng / Full khối.
- `Full khối` ở Hero phải cho ảnh chiếm toàn bộ hàng; ở Feature ảnh phải có thể mở ra toàn chiều rộng section.
- `Lưu kiểu` rồi refresh và mở tab ẩn danh: style public phải giống editor.

## 5. Các block chuẩn khác

Thử ít nhất một field ở mỗi nhóm:

- RoomGrid / BranchGrid heading.
- AvailabilitySearch heading.
- Feature title/body/benefit.
- FAQ title/question/answer.
- Location title/body/address/nearby.
- Policy title/item title/item body.
- RichText.
- CTA title/body/button.

Style phải lưu đúng field, không lan sang field khác.

## 6. Không ghi đè nội dung

- Click text, đổi nội dung nhưng chưa bấm Lưu.
- Bấm `Kiểu`.
- Hệ thống phải lưu nội dung text trước rồi mới mở Style panel.
- Sau khi lưu style, text mới vẫn còn.
- Mở `Nâng cao` ngay sau đó: text + style vẫn còn, không bị snapshot cũ ghi đè.

## 7. Row Builder regression

- Row Builder vẫn dùng responsive controls riêng như trước.
- Custom Page Element Style không được gắn nhầm lên Row element hoặc làm mất cấu hình Desktop / Tablet / Mobile của Row.

## 8. Public / permission

- Guest không thấy nút `Kiểu`, `Kiểu ảnh` hay panel editor.
- Style đã lưu vẫn render cho guest.
- Manager chỉ chỉnh Custom Page trong cơ sở được cấp quyền; Admin chỉnh được global/property theo flow hiện tại.
