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

## 2. Gallery Studio của cơ sở

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

## 3. Gallery trang chủ chung

1. Mở `/` bằng Admin → `Gallery`.
2. Checkbox trên thumbnail phải là badge vuông gọn ở góc phải trên; không còn native checkbox/khung trắng lệch kích thước.
3. Click vào card phải bật/tắt lựa chọn; card được chọn có viền teal và dấu `✓` rõ ràng.
4. Phía trên phần chọn nguồn có khối `Thêm ảnh mới`.
5. Nếu có nhiều cơ sở, chọn cơ sở đích trước khi upload; nếu chỉ có một cơ sở thì cơ sở đó được chọn sẵn.
6. `＋ Tải ảnh` cho phép chọn nhiều PNG/JPG/WebP.
7. Mỗi file upload qua Site asset pipeline của cơ sở đích rồi tạo Gallery item bằng Editorial API hiện có.
8. Ảnh mới phải xuất hiện ngay trong lưới thumbnail và được tick sẵn để dùng thuận tiện với mode `Chọn thủ công`.
9. Alt text tự gợi ý từ tên file nhưng vẫn chỉnh được sau đó tại Gallery của cơ sở.
10. Nếu nguồn hiện tại là `Tất cả` hoặc `Theo cơ sở`, upload vẫn tạo ảnh bình thường và UI giải thích rằng có thể chuyển sang `Chọn thủ công` nếu muốn chọn chính xác.
11. Đóng rồi mở lại Gallery trang chung: ảnh mới vẫn xuất hiện, không bị cache dữ liệu cũ.
12. Ảnh thật vẫn thuộc một cơ sở; không tạo global media store hoặc migration mới.

## 4. Regression

- Menu tự do PR #32 vẫn thêm/xóa/ẩn/reorder được.
- Room Card + Blog editor PR #32 vẫn hoạt động.
- Header/Footer/Rates PR #31 không đổi hành vi.
- Homepage Row Builder/Responsive/Template Library vẫn hoạt động.
- Guest không thấy UI chỉnh sửa.
- Không có migration DB mới.
