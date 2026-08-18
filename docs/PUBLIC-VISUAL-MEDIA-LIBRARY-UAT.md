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
- Confirm xóa phải nói rõ sẽ xóa file vật lý khỏi storage và metadata khỏi database.
- Sau xóa file không còn trong thư viện/storage.
- Filter `Chưa dùng` chỉ hiện media usage = 0.

## 10. Storage dashboard và dọn media chưa dùng

- Tổng số media đúng với danh sách hiện tại.
- Tổng dung lượng hiển thị B/KB/MB hợp lý.
- Số file chưa dùng và dung lượng có thể dọn được cập nhật sau upload/xóa.
- Nút cleanup phải hiện rõ số lượng, ví dụ `Dọn 7 media chưa dùng`.
- Nếu scope hiện tại không có file có quyền xóa, nút phải disabled và ghi `Không có media cần dọn`.
- Bấm cleanup → hiện confirm có **số file, dung lượng, scope** và nói rõ xóa là vĩnh viễn.
- Xác nhận → nút phải chạy tiến độ `Đang dọn x/y…`.
- Cleanup chỉ lấy media `usage = 0`, `canDelete = true` và đúng filter cơ sở/scope đang chọn; search text không làm thay đổi phạm vi cleanup.
- Server kiểm tra usage lại từng file. File vừa phát sinh usage phải bị giữ lại thay vì xóa nhầm.
- Sau cleanup, Media Library reload; số file và dung lượng phải giảm.

## 11. Trang quản trị Media trong Admin

- Sidebar `Website` có mục `Media Library`.
- Mở `/Admin/Site/Media` không cần vào Visual Editor.
- Có 3 thẻ: tổng media, dung lượng đang lưu, media/dung lượng chưa dùng.
- Có search, filter cơ sở, filter chưa dùng, refresh và drag/drop upload nhiều file.
- Admin xem toàn hệ thống và chọn nơi upload: `Dùng chung` hoặc một cơ sở mình có quyền.
- Manager chỉ thấy cơ sở hiện tại + media dùng chung; không sửa/xóa media dùng chung.
- Click thumbnail mở detail: preview, scope, kích thước, dung lượng, ngày, title, alt, usage.
- Xóa media chưa dùng phải xóa file vật lý và sau reload số dung lượng giảm.

## 12. Admin sidebar — Xem website nhanh

- Link `Xem website` cũ không còn nằm cuối nhóm `Website`.
- Có một link `Xem website` riêng **trên nhóm Vận hành**.
- Bên dưới `Hệ thống đang hoạt động` có thêm card `Xem website · Mở trang khách trong tab mới`.
- Cả hai mở `/` ở tab mới, không làm mất trang Admin đang thao tác.

## 13. Toolbar Visual Editor gọn hơn

- Toolbar không còn trải toàn bộ Gallery / Blog / Thương hiệu / Header / Footer / Menu / Quản trị thành một dãy dài.
- `Chỉnh sửa trang` và `Media` vẫn là action trực tiếp.
- `Nội dung` chứa Gallery, Blog và action nội dung theo trang hiện tại.
- `Thiết kế` chứa Thương hiệu, Header, Footer, Menu/Footer.
- `Quản trị` chứa `Quản lý Media Library`, `Cấu hình đầy đủ`, `Quản trị`.
- Click ra ngoài hoặc Esc đóng menu.
- Header/Footer/Menu enhancements tải chậm hơn vẫn phải xuất hiện đúng group, không gây lỗi `insertBefore`.
- Màn hình hẹp toolbar vẫn dùng được và không tạo thanh kéo ngang quá dài.

## 14. Regression

- Upload ảnh trực tiếp cũ (nếu vẫn dùng ở một form chưa chuyển) không làm hỏng form.
- Khi mở Media Library lần sau, `section-*` legacy mới phải được import vào thư viện.
- Room images hiện tại vẫn chạy flow Room Media riêng, không bị thay đổi.
- Branding cover/logo/favicon/OG vẫn hoạt động.
- Không có Media toolbar khi guest/staff/viewer không có quyền chỉnh website.

## 15. Migration

- Migration `AddMediaLibrary` tạo bảng `media_assets` và index cần thiết.
- Database cũ migrate lên không làm đổi URL/file hiện tại.
- Việc import legacy chỉ đăng ký metadata cho file đang tồn tại; không copy lại file.
