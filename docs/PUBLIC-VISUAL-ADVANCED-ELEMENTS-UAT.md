# Public Visual Editor — Advanced Row Elements UAT

Branch: `feat/advanced-row-elements`

## Mục tiêu

Kiểm tra 6 element nâng cao dùng chung Row Builder hiện tại, không tạo schema/CMS thứ hai. Các element được lưu dưới `kind: html` với markup có cấu trúc, vì vậy Row cũ, responsive, template, duplicate/move và sanitizer vẫn tương thích.

## 1. Thêm element

Trong Row Builder và nút `＋ Thêm phần tử` inline, xác nhận có:

- Icon + nội dung
- Video
- Bản đồ
- Accordion
- Đánh giá khách
- Ưu đãi / Giá

Thêm từng loại, lưu, refresh và mở lại Builder. Element phải còn đúng nội dung và không biến thành HTML thô trong UI cấu trúc.

## 2. Icon + nội dung

- Đổi icon/ký hiệu.
- Sửa tiêu đề và mô tả.
- Thêm link nội bộ `/booking` và text link.
- Refresh: giao diện và link giữ nguyên.

## 3. Video

- URL YouTube/Vimeo: hiển thị card video có poster/play và link hoạt động.
- URL `.mp4/.webm/.ogg`: hiển thị video player native nếu sanitizer/browser hỗ trợ.
- Upload poster bằng nút `＋ Tải poster`.
- Không cho `javascript:`, `data:`, `vbscript:` trở thành link chạy được.

## 4. Bản đồ

- Sửa tiêu đề, địa chỉ, link Google Maps, text nút.
- Link mở đúng vị trí đã cấu hình.
- Mobile phải xếp canvas + nội dung một cột.

## 5. Accordion

- Mỗi dòng editor dùng cú pháp `Câu hỏi | Câu trả lời`.
- Tối đa 8 mục.
- Bật/tắt `Mở sẵn mục đầu tiên`.
- Sau lưu/refresh, accordion đóng/mở bình thường và không mất nội dung.

## 6. Đánh giá khách

- Sửa quote, tên, dòng phụ, 1–5 sao.
- Upload ảnh đại diện.
- Không có ảnh vẫn hiển thị avatar fallback.

## 7. Ưu đãi / Giá

- Sửa badge, title, mô tả, giá, hậu tố, nút và link.
- Thử 4 tone: Nhẹ / Kem / Tối / Viền.
- CTA dùng link nội bộ và URL ngoài bình thường.

## 8. Inline editing

Với mỗi element nâng cao:

- Hover toolbar phải hiển thị đúng tên element thay vì `HTML`.
- `Sửa` mở inspector cấu trúc tại chỗ.
- Save cập nhật ngay và vẫn giữ responsive metadata.
- Duplicate, ↑/↓, ←/→, drag qua cột khác hoạt động.
- Undo/Redo Row khôi phục được element trước/sau thao tác.

## 9. Row Builder

- Dropdown `＋ Thêm` có 6 lựa chọn nâng cao.
- Sau khi thêm, card hiển thị form cấu trúc, không bắt người dùng sửa HTML.
- Preview cập nhật khi gõ.
- `Lưu Row` rồi mở lại vẫn nhận diện đúng element.
- Copy/Paste Row và Template Library giữ nguyên advanced elements.

## 10. Responsive và public

- Ẩn advanced element theo Desktop/Tablet/Mobile vẫn hoạt động.
- Test tab ẩn danh: không có toolbar/editor UI.
- Dark row theme vẫn đọc được Icon/Map/Accordion/Testimonial/Promo.
- Không có migration DB mới.

## 11. Regression — Phân cách / Khoảng cách

- Từ `＋ Thêm phần tử` inline, thêm **Phân cách**. Kết quả phải là đường phân cách thật, không được biến thành `Nhập nội dung của bạn`.
- Click trực tiếp đường phân cách hoặc nút `Sửa`: inspector nhỏ phải mở ngay trên trang.
- Đổi kiểu `Liền / Nét đứt / Nhẹ`, thêm/bỏ nhãn ở giữa và lưu. Refresh phải giữ đúng kết quả.
- Thêm **Khoảng cách**. Trong chế độ edit phải thấy vùng trống có viền dashed nhẹ và nhãn `Khoảng cách · ...`; ngoài chế độ edit phần này vẫn chỉ là khoảng trống.
- Click vùng Khoảng cách hoặc `Sửa`, chọn `20 / 42 / 70 / 108px`, lưu và refresh.
- Các element tiêu chuẩn khác thêm từ inline (`Tiêu đề`, `Văn bản`, `Ảnh`, `Nút`) phải giữ đúng loại, không biến thành Text.
- Nếu Row đã từng lưu element lỗi từ bản cũ với trường `k` thay vì `kind`, bật editor phải tự sửa chính xác loại element và lưu lại schema đúng khi có quyền chỉnh sửa.
