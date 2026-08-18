# Public Visual Editor — Media Library UAT

Branch: `feat/media-library`

## Mục tiêu

Media đã tải lên phải có thể dùng lại ở nhiều section/Row/Gallery/Blog thay vì upload một bản mới cho từng nơi. Media Library quản lý metadata, dung lượng, file chưa dùng, chống upload trùng và chặn xóa file đang được dùng.

## 1. Mở Media Library

- Bật `Chỉnh sửa trang`.
- Trên toolbar phải có nút `Media`.
- Bấm `Media`: dialog lớn mở ra, không làm dịch website phía sau.
- Có search, filter scope, `Chưa dùng`, `Tải ảnh mới`, thống kê tổng media / dung lượng / chưa dùng.
- Esc hoặc nút ×/Đóng đóng dialog.

## 2. Import ảnh section cũ

- Mở Media Library lần đầu trên một cơ sở đã từng upload ảnh section.
- Các file `section-*` cũ trong storage phải xuất hiện tự động trong thư viện, không cần upload lại.
- Ảnh có thumbnail, kích thước và dung lượng.

## 3. Upload và chống trùng

- Upload một JPG/PNG/WebP mới từ Media Library.
- Ảnh xuất hiện ngay, được chọn và có title/alt gợi ý từ tên file.
- Upload lại **đúng cùng file** lần nữa trong cùng scope.
- Không được tạo thêm file/asset trùng; Media Library trả lại asset hiện có.
- Upload nhiều file cùng lúc hoạt động.

## 4. Dùng lại ảnh trong Visual Editor

Kiểm tra các vị trí sau có nút `Thư viện` bên cạnh upload/URL:

- Hero / Feature image trong drawer.
- Builder Image trong Row Builder.
- Image inspector của Row inline.
- Poster Video / avatar Testimonial của Advanced Elements.
- Cover ảnh Blog nếu form dùng image picker hiện tại.

Chọn một ảnh có sẵn, lưu section, refresh và xác nhận URL vẫn đúng.

## 5. Click trực tiếp ảnh Hero / Feature

- Hover ảnh trong chế độ chỉnh sửa rồi bấm `Đổi ảnh`.
- Phải mở Media Library thay vì bật file picker ngay.
- Chọn ảnh cũ → ảnh trên website đổi và section được lưu.
- Không được đồng thời mở file picker cũ phía sau.

## 6. Gallery

- Mở Gallery Studio.
- Có nút `Chọn từ Media`.
- Chọn ảnh có sẵn → tạo Gallery row mới, thumbnail và alt được điền.
- Có thể tiếp tục sắp xếp, caption, publish và lưu Gallery như trước.

## 7. Tìm kiếm và scope

Ở trang cơ sở:

- `Cơ sở này`: chỉ media của cơ sở hiện tại.
- `Dùng chung`: chỉ media global.
- `Tất cả`: gồm cả hai.
- Search theo title, tên file hoặc tên cơ sở.
- Manager có thể **dùng** media global nhưng không được sửa metadata/xóa media global.

Ở trang global với Admin:

- Media Library hiển thị media của toàn hệ thống.
- Admin có thể quản lý tất cả scope.

## 8. Metadata

- Chọn media thuộc scope mình quản lý.
- Đổi `Tiêu đề nội bộ` và `Alt text` → Lưu thông tin.
- Đóng/mở lại: dữ liệu còn nguyên.
- `Sao chép URL` copy đúng URL public.

## 9. Usage và safe-delete

- Chọn ảnh đang được Hero/Row/Gallery/Blog dùng.
- Detail phải báo số vị trí sử dụng > 0.
- Bấm xóa phải bị chặn cả client và server.
- Chọn một media `Chưa dùng` → có thể xóa.
- Sau xóa file không còn trong thư viện/storage.
- Filter `Chưa dùng` chỉ hiện media usage = 0.

## 10. Storage dashboard

- Tổng số media đúng với danh sách hiện tại.
- Tổng dung lượng hiển thị B/KB/MB hợp lý.
- Số file chưa dùng và dung lượng có thể dọn được cập nhật sau upload/xóa.

## 11. Regression

- Upload ảnh trực tiếp cũ (nếu vẫn dùng ở một form chưa chuyển) không làm hỏng form.
- Khi mở Media Library lần sau, `section-*` legacy mới phải được import vào thư viện.
- Room images hiện tại vẫn chạy flow Room Media riêng, không bị thay đổi.
- Branding cover/logo/favicon/OG vẫn hoạt động.
- Không có Media toolbar khi guest/staff/viewer không có quyền chỉnh website.

## 12. Migration

- Migration `AddMediaLibrary` tạo bảng `media_assets` và index cần thiết.
- Database cũ migrate lên không làm đổi URL/file hiện tại.
- Việc import legacy chỉ đăng ký metadata cho file đang tồn tại; không copy lại file.
