# Room Images + Media Library — UAT

Branch: `feat/room-media-library`  
PR: #42

## Mục tiêu

Đưa ảnh phòng hiện có vào cùng Media Library để Admin nhìn được tổng dung lượng thật và Visual Editor có thể dùng lại ảnh phòng cho Hero / Row / Gallery / Custom Page mà không upload thêm bản sao.

Thiết kế cố ý **không copy RoomImage thành MediaAsset**. `RoomImage` vẫn là nguồn dữ liệu duy nhất cho gallery phòng và vẫn giữ pipeline chuyên dụng `original + large + card + thumb`; Media Library chỉ hợp nhất chúng ở lớp đọc/quản trị.

## 1. Admin Media nhìn thấy ảnh phòng

- Vào `Admin → Website → Media Library`.
- Dashboard có 4 ô:
  - Tổng media.
  - Dung lượng tổng.
  - Ảnh phòng.
  - Media website có thể dọn.
- `Ảnh phòng` phải có số lượng đúng với tổng RoomImage trong scope và dung lượng gồm:
  - original;
  - large.webp;
  - card.webp;
  - thumb.webp.
- Chọn filter `Ảnh phòng`: chỉ còn ảnh gallery phòng.
- Search được theo tên phòng, mã phòng, cơ sở, filename và alt text.

## 2. Detail ảnh phòng

Chọn một ảnh phòng:

- Preview dùng bản `large`.
- Hiển thị cơ sở + tên/mã phòng + kích thước + dung lượng.
- Nếu là cover phải có badge `Ảnh bìa`.
- Có link mở riêng:
  - Large.
  - Card.
  - Thumb.
- Có link `Mở nội dung phòng`.
- Sửa `Alt text` rồi lưu; reload trang Nội dung phòng phải thấy alt mới.

## 3. Dùng lại ảnh phòng trong Visual Editor

Từ một website/cơ sở có quyền chỉnh sửa:

- Bật `Chỉnh sửa trang` → Media.
- Chọn filter `Ảnh phòng`.
- Chọn một ảnh phòng → `Dùng ảnh này` cho:
  - Hero.
  - Feature image.
  - Row Image.
  - Advanced Element có ảnh.
  - Gallery Studio.
  - Custom Page.
- URL được gắn phải là bản `large` đã tối ưu, không tạo file section mới.
- Quay lại Admin Media: tổng số file không tăng chỉ vì dùng lại ảnh phòng.

## 4. Usage ngoài gallery phòng

- Chọn một ảnh phòng và dùng nó ở Hero hoặc Row.
- Reload Admin Media.
- Ảnh phải báo có `tham chiếu thêm` ngoài gallery phòng.
- Thử xóa ảnh đó trong Admin Media: server phải chặn (`409 in_use`).
- Thay Hero/Row bằng ảnh khác và lưu.
- Reload Media: tham chiếu thêm phải về 0 nếu không còn nơi nào khác dùng.

## 5. Xóa ảnh phòng an toàn

Dùng một ảnh phòng không còn tham chiếu ngoài gallery:

- Bấm `Xóa khỏi phòng`.
- Confirm phải nói rõ sẽ gỡ khỏi gallery và xóa:
  - original;
  - large.webp;
  - card.webp;
  - thumb.webp.
- Sau khi xóa:
  - RoomImage biến mất khỏi Media Library.
  - Biến mất khỏi `Phòng → Nội dung`.
  - 4 file vật lý tương ứng không còn.
- Nếu ảnh vừa xóa là cover và phòng còn ảnh khác, ảnh kế tiếp phải tự thành cover.

## 6. Cleanup không đụng ảnh phòng

- Chọn filter `Tất cả loại`.
- Nút `Dọn N media chưa dùng` chỉ đếm MediaAsset website `usage=0`.
- Một ảnh phòng dù không có tham chiếu ngoài gallery **không được** tính là `chưa dùng` và không được cleanup tự động xóa.
- Filter `Ảnh phòng` thì nút cleanup phải disabled / báo không có media cần dọn.

## 7. Quyền

- Admin global thấy room images của tất cả cơ sở.
- Manager chỉ thấy room images thuộc cơ sở đang có quyền + media dùng chung theo behavior hiện tại.
- Manager không được sửa/xóa media của cơ sở khác.
- Guest/Staff không nhận API quản trị Media Library.

## 8. Regression Room Content

- Upload ảnh mới từ `Phòng → Nội dung` vẫn tạo đủ original + large/card/thumb.
- Focal point vẫn regenerate card/thumb bình thường.
- Kéo sắp xếp ảnh vẫn hoạt động.
- Đổi cover vẫn hoạt động.
- Public `/rooms` và `/rooms/{slug}` vẫn dùng card/large đúng như trước.

## 9. Database

- PR này không thêm table/column và không có EF migration mới.
- Không cần `dotnet ef database update` chỉ vì PR #42.
