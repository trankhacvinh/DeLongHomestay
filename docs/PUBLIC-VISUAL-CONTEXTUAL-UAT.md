# Public Visual Contextual Editing — UAT

PR này mở rộng Visual Editor ra ngoài homepage. Mục tiêu là Admin/Manager đứng đúng trang public và chỉnh đúng nội dung đang nhìn thấy.

## 1. Thanh chỉnh sửa ngoài homepage

1. Đăng nhập Admin rồi mở `/rooms` hoặc một trang phòng `/h/{siteSlug}/rooms/{slug}`.
2. Xác nhận có thanh **Chỉnh website** ở trên cùng.
3. Guest / Staff / Housekeeping / Viewer không được thấy thanh này.
4. Manager chỉ thấy công cụ ở cơ sở có quyền truy cập.

## 2. Header / Footer

### Trang cơ sở

1. Mở một route thuộc `/h/{siteSlug}/...`.
2. Bấm **Header**.
3. Đổi tên website/tagline hoặc tải logo rồi lưu.
4. Reload các trang khác cùng cơ sở: Header phải dùng dữ liệu mới.
5. Bấm **Footer**, đổi địa chỉ/SĐT/email/Facebook/Zalo/Google Maps.
6. Reload và kiểm tra Footer public.
7. Các link hệ thống Đặt phòng / Tra cứu không bị mất.

### Trang chung

1. Mở route global bằng Admin.
2. Bấm Header/Footer.
3. Chỉnh global branding override.
4. Để trống override vẫn giữ inheritance của cơ sở duy nhất như PR #25.

## 3. Contextual room detail

Mở `/h/{siteSlug}/rooms/{slug}`.

### Nội dung phòng

1. Hover phần intro, bấm **Nội dung phòng**.
2. Đổi tên/mô tả ngắn/sức chứa rồi lưu.
3. Reload: public detail phải thay đổi đúng.
4. Slug và code vẫn qua validation server; thử trùng phải báo lỗi thay vì ghi đè.

### Mô tả

1. Bấm badge **Mô tả** hoặc tab Mô tả.
2. Soạn bằng Quill Trực quan.
3. Chuyển Trực quan ↔ HTML.
4. Lưu và xác nhận HTML public vẫn được sanitize như pipeline Room Content hiện tại.

### Điểm nổi bật / tiện nghi

1. Mỗi dòng nhập một mục.
2. Lưu highlights rồi amenities.
3. Reload public và kiểm tra danh sách đúng thứ tự.
4. Tags hiện có của phòng phải được giữ nguyên sau khi lưu các tab khác.

## 4. Ảnh phòng

1. Bấm **Quản lý ảnh** ngay trên gallery.
2. Upload nhiều ảnh.
3. Đổi alt text.
4. Đặt một ảnh khác làm ảnh bìa.
5. Dùng ↑/↓ đổi thứ tự.
6. Xóa một ảnh không cần thiết.
7. Reload public gallery: cover/thứ tự/alt phải đúng.
8. Không được vượt giới hạn upload hiện có của Room Content.

## 5. Regression

- Homepage Visual Editor, Row Builder, responsive controls và Template Library vẫn hoạt động.
- Booking, gallery PhotoSwipe và CTA phòng vẫn hoạt động khi không chỉnh sửa.
- Không có migration DB mới.
