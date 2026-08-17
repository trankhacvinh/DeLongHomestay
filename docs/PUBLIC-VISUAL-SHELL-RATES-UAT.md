# Public Visual Shell + Room Rates UAT

Branch: `feat/visual-shell-rates`

PR này stack trên `feat/visual-contextual-editing` / PR #30.

## 1. Menu public

1. Đăng nhập Admin, mở `/` hoặc `/h/{siteSlug}`.
2. Bấm `Menu / Footer` trên toolbar hoặc hover menu và bấm `✎ Menu`.
3. Đổi tên `Trang chủ` / `Phòng` / `Cơ sở` / `Đặt phòng` / `Tra cứu`.
4. Dùng ↑ / ↓ đổi thứ tự menu.
5. Tắt `Hiện Trang chủ`, `Hiện Phòng` hoặc `Hiện Cơ sở`.
6. Đổi chữ CTA Header.
7. Lưu: trang reload và Header phải phản ánh đúng cấu hình.
8. Mở tab ẩn danh: cấu hình menu/CTA phải giống tab Admin.
9. Link `Đặt phòng` và `Tra cứu` vẫn phải dẫn đúng route hệ thống dù đã đổi nhãn/thứ tự.

## 2. Footer

1. Bấm `Menu / Footer` → tab `Footer`, hoặc hover dòng cuối Footer → `✎ Footer`.
2. Đổi mô tả dưới thương hiệu.
3. Đổi chữ link đặt phòng.
4. Đổi tiêu đề `Khám phá`, `Cơ sở`, `Liên hệ`.
5. Đổi dòng cuối Footer.
6. Tắt/bật cột Liên hệ.
7. Lưu và kiểm tra ở desktop + mobile.
8. Với hệ thống chỉ có một cơ sở, cột Cơ sở vẫn không tự xuất hiện chỉ vì cấu hình đang bật.

## 3. Phân quyền shell

1. Admin có thể sửa shell global `/`.
2. Manager chỉ sửa được shell của property mình có quyền.
3. Manager truy cập property không có quyền phải bị 403/không thấy editor.
4. Staff / Housekeeping / Viewer / guest không thấy `Menu / Footer` và không gọi được write API.
5. Guest vẫn gọi anonymous `/api/public/site-shell` để render cấu hình public nhưng không có endpoint ghi anonymous.

## 4. Giá phòng trực tiếp

1. Mở `/h/{siteSlug}/rooms/{slug}` bằng Admin/Manager có quyền.
2. Hover khối `Giá & thời gian` hoặc booking card → `✎ Giá & thời gian` / `✎ Giá đặt phòng`.
3. Drawer phải hiển thị toàn bộ khung giá, kể cả khung đã ngừng.
4. Sửa tên, loại, giờ bắt đầu, giờ kết thúc, giá, thứ tự; bấm `Lưu tất cả`.
5. Trang reload và giá public phải cập nhật.
6. Thử bỏ check `Đang hoạt động` rồi lưu; rate phải biến khỏi public pricing/booking theo logic hiện có.
7. Check lại để kích hoạt rate cũ.
8. Bấm `Ngừng`: rate chuyển inactive nhưng booking lịch sử không mất.
9. Thêm một rate `Khung giờ` mới và kiểm tra public.
10. Thêm `Qua đêm` mới và kiểm tra public.
11. Thêm `Theo đêm` mới khi đã có một Nightly active: server phải chặn với lỗi hiện có.
12. Tắt Nightly cũ rồi tạo/kích hoạt Nightly mới: phải thành công.

## 5. Regression

- Homepage Visual Editor, Row Builder v4, responsive preview và Template Library vẫn hoạt động.
- Header/Footer editor PR #30 vẫn hoạt động.
- Room content / mô tả Quill / highlight / amenities / gallery vẫn hoạt động.
- Booking availability và booking request không đổi logic.
- Public guest không thấy bất kỳ badge/tool chỉnh sửa nào.
- Không có migration DB mới.
