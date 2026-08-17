# Public Visual Editor — Room Cards & Blog UAT

## Điều kiện

- PR này stack trên PR #31 (`feat/visual-shell-rates`).
- Dùng tài khoản Admin hoặc Manager có quyền đúng cơ sở.
- Kiểm tra lại bằng tab ẩn danh sau khi lưu.

## Drawer close icon

1. Mở Header / Footer / Menu / Giá phòng / Card phòng / Blog.
2. Nút `×` ở góc phải phải nằm chính giữa cả ngang lẫn dọc.
3. Không còn cảm giác icon bị đẩy lên trên như trước.
4. Mobile cũng phải nằm giữa nút.

## Danh sách phòng

1. Mở `/rooms` bằng Admin.
2. Mỗi card phòng phải có `✎ Sửa card phòng` khi hover.
3. Nếu nhiều cơ sở có cùng mã phòng, card phải resolve đúng cơ sở theo URL card.
4. Mở một card rồi sửa tên / mô tả ngắn / sức chứa.
5. Lưu và kiểm tra card public cập nhật sau reload.
6. Đổi slug, lưu và xác nhận link card chuyển sang slug mới.
7. Sửa Tags; dữ liệu mô tả dài / tiện nghi / highlights hiện có không được mất.
8. Bỏ `Hiển thị phòng trên website`, lưu và xác nhận public catalog không còn phòng đó theo rule hiện tại.
9. Nút `Mở trang chi tiết ↗` phải mở đúng phòng ở tab mới.

## Ảnh bìa card phòng

1. Từ drawer card chọn tab `Ảnh bìa`.
2. Ảnh đang dùng phải có nhãn `★ Đang dùng`.
3. Chọn ảnh khác → ảnh đó trở thành cover.
4. Reload trang danh sách phòng → card dùng ảnh cover mới.
5. Tải ảnh mới → ảnh được upload bằng Room Content pipeline hiện có và tự đặt làm cover.
6. Upload lỗi / file không hợp lệ phải hiện message từ server, không làm mất ảnh cũ.

## Blog list

1. Mở `/h/{siteSlug}/blog`.
2. Mỗi card có `✎ Sửa bài` khi hover.
3. Header Blog có `✎ Bài viết mới`.
4. Toolbar có `＋ Bài viết`.
5. Tạo bài mới: title, excerpt, cover, nội dung Quill, publish.
6. Sau save, bài xuất hiện đúng cơ sở.
7. Trên `/blog` global, tạo bài phải yêu cầu chọn cơ sở khi có nhiều cơ sở.
8. Admin có thể sửa card bài thuộc nhiều cơ sở trên global Blog; Manager chỉ thấy/sửa scope được phép theo `visual-context`.

## Blog detail

1. Mở `/h/{siteSlug}/blog/{slug}`.
2. Header bài có target `Sửa bài viết`.
3. Cover có target `Đổi ảnh bìa`.
4. Body có target `Sửa nội dung`.
5. Toolbar có `Sửa bài`.
6. Nội dung dùng Quill `Trực quan / HTML`, không dùng editor tự chế.
7. Chuyển Trực quan → HTML → Trực quan không mất nội dung.
8. Upload cover dùng Site asset pipeline hiện có.
9. Đổi slug rồi save → bài phải mở bằng URL mới.
10. Unpublish bài rồi kiểm tra public không còn truy cập như bài published theo behavior hiện tại.
11. Xóa bài phải hỏi confirm và quay về Blog của đúng cơ sở.

## Quyền / regression

1. Guest không thấy target / toolbar editor.
2. Staff / Housekeeping / Viewer không có write UI.
3. Manager không thể sửa property ngoài quyền.
4. Homepage Row Builder / Responsive / Template Library vẫn hoạt động.
5. Header / Footer PR #30 vẫn hoạt động.
6. Menu / Footer / Room Rates PR #31 vẫn hoạt động.
7. Booking flow không đổi.
8. Không có migration mới.
