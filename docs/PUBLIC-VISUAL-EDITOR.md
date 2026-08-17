# Public Visual Editor

Public Visual Editor là lớp thao tác trực tiếp trên website khách dành cho người có quyền quản lý nội dung.

- Trang chủ chung: chỉ Admin.
- Trang cơ sở: Admin/Manager và bắt buộc có `UserPropertyAccess` tới cơ sở đó.
- Các role khác và khách chưa đăng nhập không tải UI chỉnh sửa.
- Toolbar xuất hiện trên public page khi endpoint `GET /api/admin/site/visual-context` xác nhận quyền.
- Context trả thêm catalog cơ sở/phòng để visual editor có thể chỉnh trực tiếp nguồn của `BranchGrid` và `RoomGrid`.
- Trang chủ cho phép bật edit mode, sửa/thêm/nhân bản/ẩn/xóa block, kéo thả và dùng nút `↑` / `↓` để sắp xếp.
- Edit mode và vị trí cuộn được giữ qua reload sau khi lưu để không phải bật lại liên tục.
- Drawer dùng lại `SiteContentEndpoints` hiện có, không tạo CMS hoặc schema dữ liệu thứ hai.
- Branding drawer dùng lại global branding inheritance và property site settings hiện có.
- Gallery/Blog dùng lại `PropertyEditorialContentEndpoints` và `GlobalEditorialShowcaseService`; property content được chỉnh trực tiếp, global homepage chỉnh selection/showcase trực tiếp.
- Các thao tác ghi vẫn đi qua antiforgery, policy và property access guard hiện tại.

## Primitive kiểu UX Builder

Visual editor bổ sung các primitive dễ bố cục: `Ảnh`, `Dòng phân cách`, `Khoảng cách`, `2 cột`, `3 cột`, `HTML tùy chỉnh`.

Chúng cố ý **không tạo entity/block schema mới** ở giai đoạn này. Mỗi primitive được lưu dưới `RichText` với metadata `builderKind` và HTML đã render sẵn. `SiteContentService` tiếp tục sanitize trường `html`, vì vậy CMS cũ vẫn đọc/sửa được và không cần migration. CSS của các primitive nằm trong `homepage-cms.css`, áp dụng cho cả khách không đăng nhập.

Đây là bước trung gian hợp lý trước khi quyết định có nâng lên mô hình nested Row/Column/Element đầy đủ như Flatsome UX Builder hay không. Nested builder thực sự sẽ cần schema cây, undo/redo, breakpoint settings và cơ chế draft/publish; không nên nhét vào DOM drag tự do.

Visual editor vẫn giữ mô hình block-based và responsive-safe. Nó không cho kéo tự do từng DOM element như Webflow/Wix để tránh phá responsive và tách nguồn dữ liệu khỏi CMS.
