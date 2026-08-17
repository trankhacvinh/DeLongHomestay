# Public Visual Smart Links + Gallery Studio UAT

Branch: `feat/smart-links-gallery`

PR này stack trên `feat/visual-content-cards` / PR #32.

## 1. Smart Link Picker

1. Đăng nhập Admin/Manager có quyền và mở `/` hoặc `/h/{siteSlug}`.
2. Mở `Menu / Footer`.
3. Mỗi ô Link chỉ hiển thị một thanh compact gồm trạng thái link + `Chọn liên kết` + `Mặc định` (nếu có) + `Mở thử`; không được tự bung danh sách hay hiện `Đang tải…` trước khi bấm.
4. Bấm `Chọn liên kết`: mở dialog riêng ở giữa màn hình, không kéo dài từng row của Menu.
5. Ngay khi dialog mở, các link hệ thống phải dùng được lập tức; phòng/bài viết được tải bổ sung lazy và chỉ hiện một dòng trạng thái nhỏ `Đang tải thêm phòng và bài viết…`.
6. Dialog có search và filter `Tất cả / Hệ thống / Phòng / Bài viết / Trong trang`.
7. Danh sách trang hệ thống phải có: Trang chủ, Danh sách phòng, Đặt phòng, Tra cứu, Cơ sở, Blog.
8. Nút `×` trên dialog phải đóng được; phím `Esc` và click vùng nền tối bên ngoài dialog cũng phải đóng được.
9. Chọn `Đặt phòng`: URL hiển thị đúng scope hiện tại nhưng khi lưu vẫn giữ token hệ thống nếu không sửa tay.
10. Với menu hệ thống đã bị đổi sai URL, bấm `Mặc định` phải trả về đúng route.
11. Danh sách `Phòng` phải lấy phòng thật có quyền truy cập; chọn một phòng rồi lưu, guest phải mở đúng room detail.
12. Danh sách `Bài viết` chỉ đề xuất bài đã publish; chọn một bài rồi lưu, guest phải mở đúng blog detail.
13. Gõ từ khóa để lọc theo tên phòng, mã phòng, tên bài hoặc URL; chuyển filter không được làm mất từ khóa tìm kiếm.
14. Mục đang dùng phải có dấu chọn trong dialog và footer dialog phải hiển thị loại link + URL hiện tại.
15. `Gallery trên trang chủ` phải trỏ đến `#gallery`; gallery public đầu tiên phải có anchor `gallery`.
16. `Mở thử` phải mở URL hợp lệ ở tab mới và bị disable với URL không hợp lệ/nguy hiểm.
17. Bấm `Nhập URL thủ công` phải đóng dialog và focus trở lại ô Link để người dùng gõ URL ngoài hệ thống.
18. CTA Header và link đặt phòng Footer cũng dùng cùng picker.
19. Đóng rồi mở lại picker lần hai phải dùng dữ liệu đã cache, không tải lại toàn bộ danh sách từ đầu.

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
