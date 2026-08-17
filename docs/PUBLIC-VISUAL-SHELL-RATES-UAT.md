# Public Visual Shell + Room Rates UAT

Branch gốc: `feat/visual-shell-rates`

Các cải tiến menu tự do tiếp tục được bổ sung trên `feat/visual-content-cards` / PR #32.

## 1. Menu public

1. Đăng nhập Admin, mở `/` hoặc `/h/{siteSlug}`.
2. Bấm `Menu / Footer` trên toolbar hoặc hover menu và bấm `✎ Menu`.
3. Mỗi mục menu phải có:
   - tên hiển thị;
   - link;
   - checkbox `Hiện`;
   - checkbox `Mở tab mới`;
   - nút ↑ / ↓;
   - nút xóa.
4. Sửa link của một mục hệ thống, ví dụ đổi `Phòng` sang `/h/{siteSlug}/blog` hoặc một URL https hợp lệ; lưu và xác nhận click menu đi đúng link mới.
5. Các mục hệ thống chưa sửa link vẫn dùng route động đúng scope:
   - `Trang chủ` → home của scope hiện tại;
   - `Phòng` → rooms của scope hiện tại;
   - `Cơ sở` → `/#co-so`;
   - `Đặt phòng` → booking của scope hiện tại;
   - `Tra cứu` → booking lookup của scope hiện tại.
6. Bấm `＋ Thêm mục menu`, tạo `Giới thiệu` → `/gioi-thieu`; lưu và kiểm tra mục mới xuất hiện.
7. Tạo một link ngoài `https://...`, bật `Mở tab mới`; sau lưu anchor phải có `_blank` và `noopener noreferrer`.
8. Dùng ↑ / ↓ đổi vị trí cả mục hệ thống lẫn mục tự tạo.
9. Bỏ check `Hiện` để ẩn mục nhưng vẫn giữ cấu hình trong editor.
10. Xóa một mục khỏi menu rồi lưu; mục đó không được tự sinh trở lại.
11. Có thể xóa hết menu; Header khi đó không render link điều hướng nhưng CTA vẫn hoạt động.
12. Menu tối đa 20 mục; nút thêm phải disabled khi chạm giới hạn.
13. Thử nhập `javascript:alert(1)` hoặc scheme nguy hiểm: server phải từ chối lưu.
14. Link tương đối `/abc`, `#anchor`, `?q=...`, URL `http/https`, `mailto:` và `tel:` hợp lệ phải lưu được.
15. Đổi chữ và link CTA Header; lưu rồi xác nhận cả text và href thay đổi.
16. Mở tab ẩn danh: menu/CTA phải giống tab Admin.

## 2. Footer

1. Bấm `Menu / Footer` → tab `Footer`, hoặc hover dòng cuối Footer → `✎ Footer`.
2. Đổi mô tả dưới thương hiệu.
3. Đổi cả chữ và link đặt phòng ở Footer.
4. Đổi tiêu đề `Khám phá`, `Cơ sở`, `Liên hệ`.
5. Đổi dòng cuối Footer.
6. Tắt/bật cột Liên hệ.
7. Cột `Khám phá` phải dùng cùng danh sách menu chính, bao gồm mục tự tạo và thứ tự mới.
8. Lưu và kiểm tra ở desktop + mobile.
9. Với hệ thống chỉ có một cơ sở, cột Cơ sở vẫn không tự xuất hiện chỉ vì cấu hình đang bật.

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

## 5. Backward compatibility

- Shell config cũ chỉ có `NavigationOrder` + `HomeLabel/...` phải tự nâng thành danh sách 5 mục hệ thống khi đọc.
- Sau lần lưu bằng editor mới, payload `navigationItems` trở thành nguồn chính.
- Không migration DB; dữ liệu vẫn nằm trong metadata `__PublicShell`.
- Các route token hệ thống được giữ nội bộ khi người dùng không đổi link, để site slug thay đổi vẫn không làm hỏng link mặc định.

## 6. Regression

- Homepage Visual Editor, Row Builder v4, responsive preview và Template Library vẫn hoạt động.
- Header/Footer editor PR #30 vẫn hoạt động.
- Room content / mô tả Quill / highlight / amenities / gallery vẫn hoạt động.
- Room card + Blog editor PR #32 vẫn hoạt động.
- Booking availability và booking request không đổi logic.
- Public guest không thấy bất kỳ badge/tool chỉnh sửa nào.
- Không có migration DB mới.
