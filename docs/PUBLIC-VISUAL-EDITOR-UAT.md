# Public Visual Editor UAT

## Quyền hiển thị

1. Đăng nhập Admin rồi mở `/`: phải thấy thanh `Chỉnh website` phía trên website public.
2. Đăng nhập Manager rồi mở `/`: không được thấy visual editor của trang chủ chung.
3. Manager mở `/h/{siteSlug}` của cơ sở được cấp quyền: phải thấy thanh chỉnh website.
4. Manager mở cơ sở không được cấp quyền: không được thấy visual editor.
5. Staff / Housekeeping / Viewer và khách chưa đăng nhập: website public phải giữ nguyên, không có toolbar chỉnh sửa.

## Trang chủ

1. Bấm `Chỉnh sửa trang`: các CMS block hiển thị viền và nhóm thao tác.
2. Kéo một block sang vị trí khác, reload trang và xác nhận thứ tự đã lưu.
3. Bấm `Sửa` Hero, đổi tiêu đề/layout, lưu và xác nhận public cập nhật.
4. Thêm một block mới bằng `+ Thêm khối` trên toolbar và bằng nút chèn giữa hai block.
5. `Nhân bản`, `Ẩn`, `Xóa` phải dùng API CMS hiện có và cập nhật sau reload.
6. FAQ/Policy cho phép thêm/xóa mục lặp mà không cần nhập JSON.
7. Hero/FeatureGrid cho phép tải ảnh ngay trong drawer.

## Thương hiệu

1. Bấm `Thương hiệu` trên trang chung: chỉnh tên/tagline/logo và lưu.
2. Khi global field để trống và chỉ có một cơ sở, inheritance hiện tại vẫn hoạt động.
3. Trên trang cơ sở, chỉnh logo/tagline rồi lưu; các trường settings khác không được mất.
4. Upload logo/favicon/OG dùng pipeline ảnh CMS hiện tại.

## Responsive

1. Desktop: toolbar không che public header; drawer nằm bên phải.
2. Mobile <= 760px: toolbar thu gọn, drawer full-width, controls block vẫn thao tác được.
3. Tắt `Chỉnh sửa trang`: website trở về giao diện khách, không còn outline/control.
