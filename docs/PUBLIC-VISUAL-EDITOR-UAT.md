# Public Visual Editor UAT

## Quyền hiển thị

1. Đăng nhập Admin rồi mở `/`: phải thấy thanh `Chỉnh website` phía trên website public.
2. Đăng nhập Manager rồi mở `/`: không được thấy visual editor của trang chủ chung.
3. Manager mở `/h/{siteSlug}` của cơ sở được cấp quyền: phải thấy thanh chỉnh website.
4. Manager mở cơ sở không được cấp quyền: không được thấy visual editor.
5. Staff / Housekeeping / Viewer và khách chưa đăng nhập: website public phải giữ nguyên, không có toolbar chỉnh sửa.

## Trang chủ & trạng thái editor

1. Bấm `Chỉnh sửa trang`: các CMS block hiển thị viền và nhóm thao tác.
2. Bấm `Sửa` Hero, đổi tiêu đề/layout rồi lưu: trang reload nhưng **edit mode phải tự bật lại** và giữ gần vị trí cuộn trước đó.
3. Hover nút `Chỉnh sửa trang`: chữ và nền phải vẫn đủ tương phản, không bị chìm như bản đầu.
4. Mỗi block có cả kéo thả và nút `↑` / `↓`; nút mũi tên phải đổi thứ tự được ngay cả khi block rất cao.
5. Thêm một block mới bằng `+ Thêm khối`, sau đó dùng mũi tên hoặc kéo để đặt vị trí.
6. `Nhân bản`, `Ẩn`, `Xóa` phải dùng API CMS hiện có và sau reload editor vẫn ở chế độ chỉnh sửa.
7. FAQ/Policy cho phép thêm/xóa mục lặp mà không cần nhập JSON.
8. Hero/FeatureGrid/Ảnh cho phép tải ảnh ngay trong drawer.
9. Khi trang chung chỉ có một cơ sở active và `BranchGrid` bị public tự ẩn, các control vẫn phải bám đúng block còn lại.

## Nguồn cơ sở / phòng

1. Global `BranchGrid`: visual editor hiển thị danh sách cơ sở để chọn trực tiếp; không chọn gì = tự lấy tất cả cơ sở active.
2. Global `RoomGrid`: có `Tự lấy tất cả`, `Chia theo cơ sở`, `Chọn thủ công`.
3. `Chia theo cơ sở`: hiện quota riêng cho từng cơ sở.
4. `Chọn thủ công`: hiện danh sách phòng kèm tên cơ sở để tick trực tiếp.
5. Chuyển mode và lưu không được làm mất cấu hình nguồn chưa liên quan.
6. Property `RoomGrid`: hiển thị rõ rằng nguồn phòng luôn là phòng published của cơ sở hiện tại.

## Gallery

1. Trên homepage phải có nút sửa nổi ngay trên section Gallery khi edit mode đang bật.
2. Toolbar cũng có nút Gallery để quản lý khi section đang trống/ẩn.
3. Property Gallery: thêm ảnh, upload ảnh, sửa alt/caption, bật/tắt published, `↑`/`↓`, xóa, đổi layout rồi lưu.
4. Global Gallery: chỉnh enabled, title, layout, limit và mode `all/properties/manual` trực tiếp.
5. Global manual mode cho phép chọn ảnh published cụ thể.

## Blog

1. Trên homepage phải có nút sửa nổi ngay trên section Blog khi edit mode đang bật.
2. Property Blog: danh sách bài, thêm bài, sửa title/slug/excerpt/body HTML/cover, draft/published và xóa.
3. Khi đang ở `/h/{siteSlug}/blog/{slug}`, toolbar có `Sửa bài viết` và mở thẳng bài hiện tại.
4. Global Blog: chỉnh enabled, title, limit và nguồn `all/properties/manual` trực tiếp; nội dung bài thật vẫn thuộc cơ sở.

## Block kiểu UX Builder

1. `+ Thêm khối` có thêm: `Ảnh`, `Dòng phân cách`, `Khoảng cách`, `2 cột`, `3 cột`, `HTML tùy chỉnh`.
2. Ảnh có upload/URL, alt, caption và link.
3. 2/3 cột responsive thành một cột trên mobile.
4. HTML tùy chỉnh và HTML trong cột phải được server sanitize.
5. Các primitive này lưu dưới `RichText` có metadata `builderKind` để CMS cũ vẫn đọc được; không có migration/schema mới.

## Thương hiệu

1. Bấm `Thương hiệu` trên trang chung: chỉnh tên/tagline/logo và lưu.
2. Khi global field để trống và chỉ có một cơ sở, inheritance hiện tại vẫn hoạt động.
3. Trên trang cơ sở, chỉnh logo/tagline rồi lưu; các trường settings khác không được mất.
4. Upload logo/favicon/OG dùng pipeline ảnh CMS hiện tại.

## Responsive

1. Desktop: toolbar không che public header; drawer nằm bên phải.
2. Mobile <= 760px: toolbar cuộn ngang gọn, drawer full-width, controls block vẫn thao tác được.
3. Nút `↑` / `↓` phải thao tác được trên mobile khi drag handle bị ẩn.
4. Tắt `Chỉnh sửa trang`: website trở về giao diện khách, không còn outline/control.
