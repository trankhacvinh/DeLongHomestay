# Public Visual Smart Links + Gallery Studio UAT

Branch: `feat/smart-links-gallery`

PR này stack trên `feat/visual-content-cards` / PR #32.

## 1. Smart Link Picker

1. Đăng nhập Admin/Manager có quyền và mở `/` hoặc `/h/{siteSlug}`.
2. Mở `Menu / Footer`.
3. Dưới mỗi ô Link phải có nút `Chọn link có sẵn`.
4. Bấm picker: danh sách được chia nhóm `Trang hệ thống`, `Phòng`, `Bài viết`, `Trong trang`.
5. Các trang hệ thống phải có: Trang chủ, Danh sách phòng, Đặt phòng, Tra cứu, Cơ sở, Blog.
6. Chọn `Đặt phòng`: URL hiển thị đúng scope hiện tại nhưng khi lưu vẫn giữ token hệ thống nếu không sửa tay.
7. Với menu hệ thống đã bị đổi sai URL, bấm `Khôi phục mặc định` phải trả về đúng route.
8. Danh sách `Phòng` phải lấy phòng thật có quyền truy cập; chọn một phòng rồi lưu, guest phải mở đúng room detail.
9. Danh sách `Bài viết` chỉ đề xuất bài đã publish; chọn một bài rồi lưu, guest phải mở đúng blog detail.
10. Gõ từ khóa trong picker để lọc theo tên phòng, mã phòng, tên bài, loại link.
11. `Gallery trên trang chủ` phải trỏ đến `#gallery`; gallery public đầu tiên phải có anchor `gallery`.
12. `Mở thử` phải mở URL hợp lệ ở tab mới và bị disable với URL không hợp lệ/nguy hiểm.
13. Link tùy chỉnh vẫn được phép nhập trực tiếp như trước.
14. CTA Header và link đặt phòng Footer cũng dùng cùng picker.
15. Picker không tải danh sách phòng/blog cho đến khi người dùng mở picker lần đầu.

## 2. Gallery Studio

1. Ở trang cơ sở, bấm `Gallery` trên Visual Editor.
2. Drawer Gallery phải rộng hơn và chia hai vùng: dữ liệu bên trái, preview bên phải trên desktop.
3. Danh sách ảnh hiển thị dạng card thumbnail thay vì danh sách dài.
4. Preview thay đổi ngay khi sửa URL ảnh, caption, publish hoặc layout.
5. Chuyển layout `Mosaic / Grid / Slider`: preview bên phải phải đổi tương ứng.
6. Kéo một card ảnh lên/xuống: thứ tự preview phải đổi ngay.
7. Các nút ↑ / ↓ cũ vẫn hoạt động và tương thích với drag/drop.
8. Bấm `Tải nhiều ảnh`, chọn nhiều PNG/JPG/WebP: hệ thống upload lần lượt và tự tạo card cho từng ảnh.
9. Alt text của ảnh upload nhiều tự gợi ý từ tên file nhưng vẫn sửa được.
10. Dùng filter `Tất cả / Đang hiện / Đang ẩn` để lọc danh sách card.
11. Bỏ check `Đang hiển thị`: preview vẫn cho thấy item mờ với nhãn `Đang ẩn`, guest không được thấy sau khi lưu.
12. Xóa một ảnh rồi lưu: item bị xóa qua API hiện tại.
13. Lưu Gallery: create/update/delete/reorder/layout đều phải dùng endpoint Editorial hiện có.
14. Refresh trang và kiểm tra thứ tự/layout/caption/publish được giữ đúng.
15. Trên mobile drawer phải về một cột; preview nằm dưới danh sách và không tràn viewport.

## 3. Regression

- Menu tự do PR #32 vẫn thêm/xóa/ẩn/reorder được.
- Room Card + Blog editor PR #32 vẫn hoạt động.
- Header/Footer/Rates PR #31 không đổi hành vi.
- Homepage Row Builder/Responsive/Template Library vẫn hoạt động.
- Guest không thấy UI chỉnh sửa.
- Không có migration DB mới.
