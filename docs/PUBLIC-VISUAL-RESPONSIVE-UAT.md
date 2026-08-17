# Public Visual Editor — Responsive Controls UAT

## Mục tiêu

Kiểm tra Row Builder v4 sau khi bổ sung điều khiển riêng cho Desktop / Tablet / Mobile. Không có migration database.

## 1. Nâng cấp Row cũ

1. Mở một Row đã tạo từ PR #27.
2. Bấm **Sửa**.
3. Xác nhận nội dung, ảnh, button và HTML cũ còn nguyên.
4. Lưu lại mà không thay nội dung.
5. Reload trang khách và xác nhận Row vẫn hiển thị đúng.

## 2. Thiết kế theo thiết bị

Trong Row Builder, kiểm tra ba tab **Desktop / Tablet / Mobile**:

- bật/tắt hiển thị Row theo từng thiết bị;
- đổi độ rộng: Toàn chiều rộng / Rộng / Gọn / Hẹp;
- đổi khoảng giữa cột;
- đổi padding ngang;
- đổi padding dọc;
- đổi căn dọc nội dung;
- ở Mobile thử **Theo thứ tự** và **Đảo thứ tự**.

Tab thiết kế và tab Preview phải chuyển cùng nhau.

## 3. Responsive của từng element

Mở phần **Responsive / thiết bị** trong element:

- ẩn một element chỉ trên Mobile, giữ Desktop/Tablet;
- với Heading: đặt cỡ Desktop = XL, Tablet = LG, Mobile = MD;
- đổi căn Heading riêng theo thiết bị;
- với Button: đổi căn trái / giữa / phải theo từng thiết bị.

Preview phải cập nhật ngay.

## 4. Preview

1. Chuyển Desktop → Tablet → Mobile.
2. Mobile phải stack cột thành một cột.
3. 4 cột trên Tablet phải hiển thị thành 2 cột.
4. Element bị ẩn theo thiết bị không được xuất hiện ở preview tương ứng.
5. Padding / gap / heading size phải đổi theo đúng tab.

## 5. Public output

Sau khi lưu:

1. Mở tab ẩn danh desktop và kiểm tra Row.
2. Resize về khoảng tablet và kiểm tra layout/gap/padding/visibility.
3. Resize xuống mobile và kiểm tra stack/reverse cùng element visibility.
4. Xác nhận khách chưa đăng nhập nhìn đúng output responsive đã lưu.

## 6. Regression

- Quill vẫn focus/gõ được trong Text/HTML.
- Trực quan ↔ HTML không mất nội dung.
- Copy/paste element giữ responsive settings.
- Copy/paste Row giữ responsive settings.
- Undo/redo khôi phục thay đổi responsive.
- Upload ảnh vẫn hoạt động.
- ↑ / ↓ / ← / → / duplicate / delete element vẫn hoạt động.
- Lưu xong Visual Editor vẫn tự bật lại như PR #27.
