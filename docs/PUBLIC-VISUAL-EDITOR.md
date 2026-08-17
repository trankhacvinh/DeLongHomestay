# Public Visual Editor

Public Visual Editor là lớp thao tác trực tiếp trên website khách dành cho người có quyền quản lý nội dung.

- Trang chủ chung: chỉ Admin.
- Trang cơ sở: Admin/Manager và bắt buộc có `UserPropertyAccess` tới cơ sở đó.
- Các role khác và khách chưa đăng nhập không tải UI chỉnh sửa.
- Toolbar xuất hiện trên public page khi endpoint `GET /api/admin/site/visual-context` xác nhận quyền.
- Trang chủ cho phép bật edit mode, sửa/thêm/nhân bản/ẩn/xóa block và kéo thả sắp xếp.
- Drawer dùng lại `SiteContentEndpoints` hiện có, không tạo CMS hoặc schema dữ liệu thứ hai.
- Branding drawer dùng lại global branding inheritance và property site settings hiện có.
- Các thao tác ghi vẫn đi qua antiforgery, policy và property access guard hiện tại.

Visual editor cố ý giữ mô hình block-based. Nó không cho kéo tự do từng DOM element như Webflow/Wix để tránh phá responsive và tách nguồn dữ liệu khỏi CMS.
