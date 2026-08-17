# Public Visual Inline Editing UAT

Branch: `feat/inline-visual-editing`

## Mục tiêu

Cho phép sửa các nội dung đơn giản ngay trên chính website khi Visual Editor đang bật, không cần mở drawer cho mọi thay đổi nhỏ.

## 1. Bật / tắt

1. Đăng nhập Admin hoặc Manager có quyền và mở `/` hoặc `/h/{siteSlug}`.
2. Bấm `Chỉnh sửa trang`.
3. Heading/text/button hỗ trợ inline phải có outline nhẹ khi hover.
4. Hero/Feature có ảnh phải hiện nút `Đổi ảnh` khi hover.
5. Bấm `Kết thúc chỉnh sửa`: toàn bộ affordance inline phải biến mất; guest không bao giờ thấy chúng.

## 2. Text inline

1. Click Hero title → gõ trực tiếp trên heading.
2. Với field một dòng, `Enter` lưu; với field nhiều dòng, `Ctrl+Enter`/`Cmd+Enter` lưu.
3. `Esc` phải hoàn nguyên nội dung chưa lưu.
4. Thanh action nổi có `Nâng cao / Hủy / Lưu` và bám gần field đang sửa.
5. Lưu thành công không reload toàn trang và text trên màn hình đổi ngay.
6. Refresh trang → text vừa sửa vẫn giữ đúng.

Các field cần test:
- Hero: eyebrow, title, body, CTA chính/phụ.
- BranchGrid / RoomGrid: eyebrow + title.
- AvailabilitySearch: title.
- FeatureGrid: eyebrow, title, body, từng benefit.
- FAQ: eyebrow, title, từng question/answer.
- Location: eyebrow, title, body, address, nearby.
- PolicyGrid: eyebrow, title, từng policy title/body.
- CTA: title, body, button text.
- RichText thường: nội dung HTML trực tiếp; Row/Builder RichText không được inline-edit theo cách làm mất metadata builder.

## 3. Link của button

1. Click CTA Hero hoặc CTA block.
2. Bấm `Liên kết` trên thanh chỉnh nhanh.
3. Nhập URL tùy chỉnh hoặc chọn nhanh Trang chủ / Phòng / Đặt phòng / Tra cứu / Blog / Gallery.
4. `Mở thử` phải mở đúng URL đã resolve theo scope cơ sở.
5. Bấm `Xong` rồi `Lưu` → text + URL lưu cùng một lần.
6. Refresh và guest click button phải đi đúng URL.

## 4. Đổi ảnh trực tiếp

1. Hover Hero image hoặc Feature image → `Đổi ảnh`.
2. Chọn PNG/JPG/WebP.
3. Upload phải dùng Site asset pipeline hiện có.
4. Ảnh đổi ngay trên trang sau khi lưu API, không reload.
5. Refresh → ảnh mới vẫn đúng.

## 5. Tương thích drawer nâng cao

1. Sửa inline và lưu một field.
2. Ngay sau đó bấm `Sửa` của khối hoặc `Nâng cao` trên thanh inline.
3. Hệ thống được phép reload một lần để lấy section mới nhất, sau đó phải tự mở đúng drawer.
4. Drawer không được chứa dữ liệu cũ rồi ghi đè thay đổi inline vừa lưu.
5. Nếu chưa có thay đổi inline đã lưu, `Nâng cao` phải mở drawer ngay không reload.

## 6. Regression

- Kéo/drop, ↑/↓, duplicate, hide, delete của block vẫn hoạt động.
- Row Builder v4 / Responsive / Template Library không đổi hành vi.
- Gallery Studio, Global Gallery manual source, Smart Link Picker, Menu/Footer, Room Cards, Blog, Rates vẫn hoạt động.
- Không migration DB mới.
- Write vẫn đi qua Site Content API, property access, antiforgery và sanitizer hiện có.
- Mobile: action bar nằm sát đáy màn hình, không tràn viewport.
