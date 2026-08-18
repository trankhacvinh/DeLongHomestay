# Public Header & Footer Designer — UAT

Branch: `feat/header-footer-designer`

## Mục tiêu

Header có thể bật/tắt sticky và có style riêng sau khi cuộn. Footer có Builder Row / Column / Element để thiết kế linh hoạt nhưng vẫn dùng đúng dữ liệu động của Menu, cơ sở và liên hệ. Cấu hình được lưu bằng metadata `__PublicShellDesigner`, không cần migration database.

## 1. Header — Sticky bật/tắt

- Đăng nhập Admin/Manager và mở trang public có quyền chỉnh sửa.
- Bấm `Header` trên toolbar hoặc nút chỉnh trực tiếp trên Header.
- Mặc định `Sticky Header` bật.
- Checkbox phải là ô nhỏ bo góc đúng theme, không bị kéo thành ô xanh lớn theo style input chung.
- Lưu rồi cuộn trang: Header phải bám phía trên.
- Tắt `Sticky Header`, lưu và refresh.
- Cuộn xuống: Header phải cuộn khỏi màn hình cùng nội dung, không còn `position: sticky`.

## 2. Header — style bình thường và style sau khi cuộn

- Bật Sticky.
- Ở `Style Header bình thường`, đặt nền tối + chữ sáng.
- Ở `Style sau khi cuộn`, đặt nền sáng + chữ tối.
- Có thể bật/tắt `Đổ bóng` và `Blur` độc lập cho hai trạng thái.
- Các checkbox Đổ bóng / Blur phải cùng kiểu toggle checkbox nhỏ, căn đều với nhãn.
- Lưu và refresh.
- Ở đầu trang phải thấy style bình thường.
- Cuộn xuống khoảng 20px: màu nền/chữ/viền/shadow/blur phải chuyển sang style sticky.
- Cuộn về đầu trang: phải trở lại style bình thường.

## 3. Header — thương hiệu

### Trang cơ sở

- Sửa tên website / tagline.
- `Chọn từ Media` cho logo, chọn một ảnh đã có trong Media Library.
- Lưu Header và refresh: logo/tên/tagline phải đổi đúng.

### Trang chủ chung

- Admin mở Header của `/`.
- Các field là override thương hiệu global; để trống vẫn giữ fallback hiện tại.
- Lưu không được làm mất favicon / OG image / meta title / meta description đã cấu hình trước đó.

## 4. Footer Builder — bật/tắt

- Bấm `Footer` trực tiếp trên website.
- Bật `Dùng Footer Builder`.
- Checkbox `Dùng Footer Builder` phải gọn giống checkbox Header, không bị kéo full chiều cao/chiều rộng.
- Bấm `Bắt đầu từ Footer hiện tại`.
- Phải sinh mẫu 1 hàng / 4 cột gồm:
  - Thương hiệu + mô tả + nút đặt phòng.
  - Tiêu đề Khám phá + Menu chính.
  - Tiêu đề Cơ sở + danh sách cơ sở.
  - Tiêu đề Liên hệ + thông tin liên hệ.
- Lưu và refresh: Footer public phải dùng cấu trúc Builder.
- Tắt `Dùng Footer Builder`, lưu: Footer cũ phải xuất hiện lại và cấu trúc Builder không bị mất.

## 5. Row / Column

- Thêm một hàng mới.
- Chỉnh màu nền, màu chữ, Padding và Gap.
- Thêm tối đa 4 cột.
- Chỉnh span cột theo lưới 12 (`2/12` … `12/12`).
- Di chuyển hàng lên/xuống; di chuyển cột trái/phải; xóa hàng/cột.
- Không cho xóa cột cuối cùng của một hàng.
- Footer tối đa 6 hàng.

## 6. Element Footer

Kiểm tra thêm / di chuyển / xóa từng loại:

- `Thương hiệu / Logo`: lấy thương hiệu hiện tại.
- `Tiêu đề`: sửa text + H2/H3/H4 + căn trái/giữa/phải.
- `Văn bản`: nhiều dòng.
- `Ảnh`: chọn lại từ Media Library, sửa alt và link khi bấm.
- `Nút`: text + URL + default/pill/outline.
- `Danh sách link`: thêm/xóa link, URL, mở tab mới.
- `Menu chính`: tự lấy Menu hiện tại, không copy cứng nội dung.
- `Danh sách cơ sở`: tự lấy các cơ sở đang hoạt động.
- `Liên hệ`: tự lấy địa chỉ / điện thoại / email hiện tại.
- `Phân cách`.
- `Khoảng cách`: nhỏ / vừa / lớn.

## 7. Inline edit Footer

- Bật `Chỉnh sửa trang` và bảo đảm Footer Builder đang bật.
- Hover từng element Footer: có viền dashed nhẹ và nút `⚙` nhỏ để mở Footer Builder nâng cao.
- Click `Tiêu đề` rồi gõ trực tiếp trên Footer → `Lưu` → refresh: nội dung mới phải còn.
- Click `Văn bản`: Enter xuống dòng bình thường; `Ctrl+Enter` / `Cmd+Enter` lưu; `Esc` hủy.
- Click `Nút`: sửa chữ trực tiếp; bấm `Liên kết` để đổi URL/token; lưu không được điều hướng khỏi trang.
- `Danh sách link` tùy chỉnh: click từng link để sửa label + URL trực tiếp; khi đang edit không được điều hướng.
- Click ảnh hoặc `Đổi ảnh`: mở Media Library; chọn ảnh cũ → lưu ngay và cập nhật preview.
- Các element động `Brand / Menu / Cơ sở / Liên hệ / Divider / Spacer` dùng nút `⚙` để vào Builder nâng cao, không sửa HTML render trực tiếp.
- Inline save phải cập nhật chính `footerRows[].columns[].elements[]` trong `__PublicShellDesigner`, không tạo section/footer schema thứ hai.

## 8. Dữ liệu động / compatibility

- Vào công cụ `Menu / Footer` cũ và đổi tên / URL một mục Menu.
- Footer Builder có element `Menu chính` phải tự hiển thị thay đổi sau refresh.
- Thêm / tắt cơ sở: element `Danh sách cơ sở` phải phản ánh dữ liệu public hiện tại.
- Sửa địa chỉ / điện thoại / email trong Footer Designer của cơ sở: element `Liên hệ` phải đổi theo.
- Header CTA và Menu hiện tại vẫn hoạt động như trước; PR này không thay schema `__PublicShell`.

## 9. Media Library safe-delete

- Trong Footer Builder thêm element `Ảnh` và chọn media đang `Chưa dùng`.
- Lưu Footer.
- Vào `Admin → Website → Media Library`.
- Ảnh vừa dùng trong Footer phải có `usage > 0` và không được xóa/dọn như file chưa dùng.
- Xóa element ảnh khỏi Footer, lưu; sau đó ảnh có thể trở về `Chưa dùng` nếu không còn nơi nào tham chiếu.

## 10. Responsive

- Desktop: lưới 12 cột theo span đã chọn.
- Tablet: các cột chuyển thành 2 cột dễ đọc.
- Mobile: mỗi cột xếp thành một hàng, không tràn ngang.
- Header sticky không được đè lên Visual Editor toolbar khi đang chỉnh sửa.
- Inline toolbar Footer không được tràn màn hình; mobile phải tự xuống dòng.

## 11. Metadata / regression

- `__PublicShellDesigner` không được xuất hiện như một section trong Admin CMS hoặc Visual Editor.
- Reorder section không được báo sai số lượng vì metadata designer.
- Không có migration DB mới cho PR này.
- Guest không có quyền chỉnh sửa chỉ thấy giao diện public, không thấy drawer/toolbar designer/inline controls.
- Room / Booking / Blog / Gallery / Media Library tiếp tục hoạt động bình thường.
