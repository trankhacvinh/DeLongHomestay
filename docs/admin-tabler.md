# Admin UI trên Tabler

Giao diện admin đang được chuyển dần sang Tabler (kế hoạch 6 PR). Quy tắc:

- Layout mới: `Pages/Shared/_AdminLayout.cshtml`. Trang đã chuyển đặt `Layout = "_AdminLayout";`, trang chưa chuyển vẫn dùng `_Layout.cshtml`. Một trang không bao giờ tải cả hai bộ CSS.
- CSS: `wwwroot/vendor/tabler/tabler.min.css` (Tabler 1.4.0 + Bootstrap 5.3.7, chỉ các thành phần cần dùng, màu chủ đạo `#174b4f`, font Be Vietnam Pro) và `wwwroot/css/admin-app.css`. Dựng lại bằng `scripts/tabler/build.sh` (cần Node chỉ khi nâng cấp; server không cần Node).
- Không dùng JS của Bootstrap: modal do Vue điều khiển; dropdown topbar dùng `js/core/admin-dropdown.js`.
- Chỉ tải script khi trang cần: `ViewData["UseVue"]`, `UseFlatpickr`, `UseUploads`, `UsePasswordStrength`.
- Icon: sprite `#i-*` trong `_AdminIcons.cshtml` (cùng bộ với `_Layout.cshtml` cho tới khi layout cũ bị gỡ).
- Khi thêm trang admin vào menu, cập nhật cả `_Layout.cshtml` và `_AdminLayout.cshtml` (test `AdminTablerLayoutSourceContractTests` kiểm tra hai bên khớp).
