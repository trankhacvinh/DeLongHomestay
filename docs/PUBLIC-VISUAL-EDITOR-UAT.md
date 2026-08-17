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
6. Danh sách chọn ảnh thủ công phải hiển thị **thumbnail + checkbox + caption/cơ sở**, không chỉ có tên ảnh.
7. Click toàn bộ card ảnh phải tick/bỏ tick checkbox; card được chọn phải có viền nổi rõ.
8. Control nổi của Gallery có thêm `↑` / `↓` để di chuyển **cả section Gallery** lên/xuống giữa các block trang chủ.
9. Đưa Gallery lên giữa Hero và RoomGrid, reload rồi mở lại bằng tài khoản khách: Gallery vẫn phải nằm đúng vị trí đã lưu.
10. Di chuyển Gallery qua Blog theo cả hai hướng; không được tạo vòng lặp vị trí hoặc làm mất section.

## Blog

1. Trên homepage phải có nút sửa nổi ngay trên section Blog khi edit mode đang bật.
2. Property Blog: danh sách bài, thêm bài, sửa title/slug/excerpt/body/cover, draft/published và xóa.
3. Nội dung bài có hai chế độ `Trực quan` và `HTML`; người dùng thường có thể định dạng mà không cần biết HTML.
4. Chuyển Trực quan → HTML → Trực quan không được mất nội dung; HTML vẫn được server sanitize khi lưu.
5. Khi đang ở `/h/{siteSlug}/blog/{slug}`, toolbar có `Sửa bài viết` và mở thẳng bài hiện tại.
6. Global Blog: chỉnh enabled, title, limit và nguồn `all/properties/manual` trực tiếp; nội dung bài thật vẫn thuộc cơ sở.
7. Control nổi của Blog có thêm `↑` / `↓` để di chuyển **cả section Blog** lên/xuống giữa các block trang chủ.
8. Đưa Blog lên trước Gallery hoặc lên giữa hai block CMS, reload và xác nhận cả Admin lẫn khách đều thấy cùng thứ tự.
9. Sau khi di chuyển Gallery/Blog, editor phải tự bật lại và việc tiếp tục di chuyển một block CMS bình thường vẫn phải lưu được.

## Primitive kiểu UX Builder

1. `+ Thêm khối` có: `Ảnh`, `Dòng phân cách`, `Khoảng cách`, `2 cột`, `3 cột`, `HTML tùy chỉnh`.
2. Ảnh có upload/URL, alt, caption và link.
3. 2/3 cột responsive thành một cột trên mobile.
4. Text/HTML trong RichText, HTML tùy chỉnh và HTML trong cột có toggle `Trực quan / HTML`.
5. HTML tùy chỉnh và HTML trong cột phải được server sanitize.
6. Các primitive này lưu dưới `RichText` có metadata `builderKind` để CMS cũ vẫn đọc được; không có migration/schema mới.

## Row / Column Builder lồng phần tử

1. Khi đang bật edit mode, toolbar có nút `＋ Row / cột`.
2. Khi mở `+ Thêm khối`, drawer cũng phải có shortcut `Row / Column Builder`.
3. Drawer Row phải rộng trên desktop; phần nhập liệu nằm bên trái và **preview sticky nằm bên phải**, không phải kéo xuống cuối form để xem.
4. Preview có nút `Desktop / Tablet / Mobile`; Mobile phải mô phỏng stack/reverse ngay trong vùng preview.
5. Tạo được các preset: `1 cột`, `50/50`, `33/67`, `67/33`, `3 cột đều`, `25/50/25`, `4 cột đều`.
6. Đổi preset phải giữ lại nội dung các cột còn tồn tại; cột mới được tạo rỗng.
7. Mỗi Row chỉnh được gap, căn dọc, nền `plain/soft/cream/dark`, padding và cách stack mobile.
8. Trong từng cột thêm được nhiều phần tử: `Tiêu đề`, `Văn bản`, `Ảnh`, `Nút`, `Dòng phân cách`, `Khoảng cách`, `HTML`.
9. Văn bản và HTML có toggle `Trực quan / HTML`; toolbar WYSIWYG có paragraph/H2/H3, bold, italic, underline, list, quote và link.
10. Mỗi phần tử có `↑`, `↓`, `←`, `→`, copy, nhân bản, xóa; có thể chuyển phần tử giữa các cột.
11. Copy một phần tử rồi `Dán` sang cột khác phải giữ đúng nội dung và cấu hình phần tử.
12. `Copy Row / Dán Row` hoạt động trong session; dán Row có thể hoàn tác.
13. `↶ / ↷` hoàn tác/làm lại được các thay đổi cấu trúc và nội dung gần nhất trong Row.
14. Ảnh trong Row upload được bằng pipeline asset hiện tại và preview cập nhật ngay.
15. Nút hỗ trợ text, URL, kiểu primary/outline/ghost và căn trái/giữa/phải.
16. Preview nhanh cập nhật khi đổi nội dung/layout nhưng không được thực thi `script`, event handler hoặc `javascript:` URL.
17. Lưu Row xong phải reload và tự quay lại edit mode; control ngoài trang hiển thị nhãn `Row / cột`.
18. Bấm `Sửa` trên một Row đã lưu phải mở lại đúng nested builder, không rơi về textarea RichText thô.
19. `Nhân bản`, `Ẩn`, `Xóa`, `↑`, `↓` ở control ngoài trang vẫn hoạt động với Row như block bình thường.
20. Row mới lưu dưới `RichText` với `builderKind=row`; HTML thực tế vẫn đi qua sanitizer server hiện có.
21. Một Row quá lớn phải bị chặn ở client và hướng dẫn tách thành nhiều Row thay vì vượt giới hạn ContentJson.
22. Mở tab ẩn danh/khách: Row vẫn hiển thị đúng layout, màu nền, button, ảnh và responsive mà không cần visual-editor JS có quyền quản trị.

## Thương hiệu

1. Bấm `Thương hiệu` trên trang chung: chỉnh tên/tagline/logo và lưu.
2. Khi global field để trống và chỉ có một cơ sở, inheritance hiện tại vẫn hoạt động.
3. Trên trang cơ sở, chỉnh logo/tagline rồi lưu; các trường settings khác không được mất.
4. Upload logo/favicon/OG dùng pipeline ảnh CMS hiện tại.

## Responsive

1. Desktop: toolbar không che public header; drawer nằm bên phải.
2. Desktop rộng: Row Builder sử dụng gần hết chiều ngang nhưng vẫn chừa một phần website phía sau để giữ ngữ cảnh.
3. Mobile <= 760px: toolbar cuộn ngang gọn, drawer full-width, controls block vẫn thao tác được.
4. Nút `↑` / `↓` phải thao tác được trên mobile khi drag handle bị ẩn.
5. Gallery/Blog cũng phải di chuyển được bằng `↑` / `↓` trên mobile.
6. Row 2/3/4 cột phải stack thành 1 cột trên mobile; tùy chọn `Đảo thứ tự cột` phải hoạt động.
7. Rich editor trên mobile cho phép cuộn ngang toolbar định dạng mà không làm tràn trang.
8. Tắt `Chỉnh sửa trang`: website trở về giao diện khách, không còn outline/control.
