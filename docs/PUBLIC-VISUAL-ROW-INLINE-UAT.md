# Public Visual Row Inline Editing UAT

Branch: `feat/inline-row-builder`

Mục tiêu: sửa trực tiếp phần tử bên trong Row Builder trên website mà vẫn giữ cấu trúc `rowVersion`, responsive metadata và API/sanitizer hiện có.

## 1. Bật / tắt chế độ chỉnh sửa

1. Đăng nhập Admin hoặc Manager có quyền và mở `/` hoặc `/h/{siteSlug}`.
2. Bấm `Chỉnh sửa trang`.
3. Row Builder phải có nhãn nhỏ `Row · sửa trực tiếp` cùng `↶`, `↷`, `Builder`.
4. Hover từng phần tử trong Row phải hiện toolbar nhỏ; guest hoặc khi `Kết thúc chỉnh sửa` không được thấy toolbar này.
5. Bấm `Kết thúc chỉnh sửa`: toàn bộ toolbar, nút `＋ Thêm phần tử`, outline và trạng thái inline phải biến mất.

## 2. Heading / Text / Button

1. Click trực tiếp Heading trong Row, sửa chữ, bấm `Enter` hoặc `Lưu`.
2. Không reload trang; chữ mới phải hiện ngay và refresh vẫn giữ đúng.
3. Click Text, sửa nội dung; `Ctrl+Enter` hoặc `Cmd+Enter` để lưu.
4. `Esc` khi đang sửa phải hủy và trả lại nội dung trước đó.
5. Click Button, sửa text rồi bấm `Liên kết`.
6. Chọn nhanh `Trang chủ / Phòng / Đặt phòng / Tra cứu / Blog / Gallery` hoặc nhập URL tùy chỉnh.
7. `Mở thử` chỉ mở URL an toàn; `javascript:`, `data:`, `vbscript:` không được áp dụng.
8. Sau khi lưu Button, refresh và mở link phải đúng.

## 3. Image

1. Click ảnh trong Row hoặc bấm `Sửa` trên toolbar.
2. Inspector ảnh phải cho sửa URL ảnh, alt, caption, link.
3. Bấm `＋ Tải ảnh`: chọn PNG/JPG/WebP, upload bằng Site asset pipeline hiện có.
4. Nếu alt đang trống, upload phải gợi ý alt từ tên file; vẫn sửa được trước khi lưu.
5. Chọn link nhanh hoặc nhập link tùy chỉnh cho ảnh.
6. Bấm `Lưu`: ảnh/caption/link đổi ngay, refresh vẫn đúng.
7. Bấm `Nâng cao` phải mở Row Builder hiện tại, không mở editor dữ liệu thứ hai.

## 4. Thêm phần tử ngay trong cột

1. Hover cột → `＋ Thêm phần tử`.
2. Thử thêm lần lượt: Heading, Text, Image, Button, Divider, Spacer, HTML.
3. Heading/Text/Button mới phải đi thẳng vào sửa nội dung; Image mới mở inspector.
4. HTML mới chuyển sang Row Builder nâng cao để tránh sửa HTML thô thiếu kiểm soát.
5. Không được thêm quá 10 phần tử trong một cột; phải có thông báo rõ khi đầy.
6. Cột trống sau khi thêm phần tử phải bỏ placeholder; xóa hết phần tử phải có placeholder trở lại.

## 5. Thao tác cấu trúc phần tử

1. Toolbar phần tử: `↑`, `↓`, `←`, `→`, `Sửa`, `⧉`, `×` hoạt động đúng.
2. `⧉` nhân bản đúng dữ liệu và responsive metadata của phần tử.
3. `×` hỏi xác nhận rồi xóa đúng phần tử.
4. `← / →` chuyển phần tử sang cột bên cạnh và không cho chuyển vào cột đã đủ 10 phần tử.
5. Kéo handle `⋮⋮` để đổi vị trí trong cùng cột.
6. Kéo phần tử sang cột khác; thứ tự mới phải giữ sau refresh.
7. Kéo chỉ được trong cùng một Row, không làm lẫn dữ liệu giữa hai Row khác nhau.

## 6. Undo / Redo

1. Sửa text → lưu → bấm `↶`: nội dung trở về trạng thái trước.
2. Bấm `↷`: nội dung quay lại trạng thái vừa sửa.
3. Thử Undo/Redo sau add, duplicate, delete hoặc move.
4. Sau Undo/Redo, refresh phải giữ đúng trạng thái cuối đang thấy.
5. Nút Undo/Redo phải disable đúng ở đầu/cuối history.

## 7. Responsive & metadata regression

1. Mở Builder và đặt riêng Desktop/Tablet/Mobile cho Row và một Heading/Button.
2. Lưu, sau đó chỉ sửa text của Heading bằng inline editor.
3. Mở Builder lại: visibility, size, align, gap, padding, width, mobile stack/reverse phải còn nguyên.
4. Duplicate hoặc move element bằng inline editor cũng phải giữ responsive metadata.
5. `content.builderKind = row`, `rowVersion`, layout/theme và columns không bị đổi ngoài thao tác người dùng thực hiện.
6. HTML lưu ra không được chứa `.pve-row-inline-*`, `data-row-inline-*`, `contenteditable`, toolbar hoặc nút editor.

## 8. Tương tác với Row Builder / stale guard

1. Sửa inline và lưu một phần tử.
2. Ngay sau đó bấm `Builder` hoặc `Sửa` của Row.
3. Nếu stale guard cần reload, trang phải tự reload và mở đúng Row Builder với dữ liệu vừa lưu; không được quay lại dữ liệu cũ.
4. Chỉnh tiếp trong Row Builder và lưu; quay lại public phải thấy cả thay đổi builder lẫn nội dung inline trước đó.
5. Core section actions `Nhân bản / Ẩn` sau một inline save không được ghi đè Row bằng ContentJson cũ.

## 9. An toàn / regression chung

- Không migration DB mới.
- Vẫn dùng `PUT /sections/{id}` và `assets/section` hiện có.
- Policy, property access, antiforgery và server-side RichText sanitizer vẫn là boundary cuối.
- Row thường không có `builderKind=row` không bị editor Row can thiệp.
- Gallery, Blog, Menu/Footer, Room Card, Rates và inline editor của block thường vẫn hoạt động.
- Admin/Manager sai property không được có quyền sửa Row của property khác.
- Guest/ẩn danh không thấy UI chỉnh sửa và render Row bình thường.
- Desktop và mobile không bị toolbar tràn viewport; mobile vẫn thao tác được `Sửa / ⧉ / × / Thêm phần tử`.
