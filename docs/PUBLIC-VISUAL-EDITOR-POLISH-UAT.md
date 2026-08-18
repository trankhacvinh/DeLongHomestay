# Public Visual Editor — Polish & Stabilization UAT

Branch: `feat/editor-polish-stabilization`  
PR: #43

## Mục tiêu

Vòng này không thêm một editor mới. Mục tiêu là làm các editor hiện có đồng nhất hơn, tránh save chồng dữ liệu cũ và cho phép chỉnh style cơ bản/responsive ngay tại phần tử đang nhìn.

## 1. Homepage — style phần tử

Thực hiện cả `/` (Admin) và `/h/{siteSlug}` (Admin/Manager đúng cơ sở):

1. Bật **Chỉnh sửa trang**.
2. Click tiêu đề Hero → sửa nội dung → **Lưu**.
3. Click lại tiêu đề → **Kiểu**.
4. Desktop:
   - Căn = Giữa.
   - Cỡ chữ = Hero.
   - Độ rộng = Full.
   - Khoảng cách dọc = Lớn.
5. Tablet:
   - Cỡ chữ = Rất lớn.
   - Độ rộng = Rộng.
6. Mobile:
   - Cỡ chữ = Lớn.
   - Căn = Trái.
7. **Lưu kiểu**.

Kỳ vọng:
- Panel không reload trang sau khi lưu.
- Nội dung vừa sửa không bị trả lại snapshot cũ.
- Refresh vẫn giữ style.
- Mở tab ẩn danh/public vẫn thấy đúng style theo breakpoint.

## 2. Button

1. Click nút Hero/CTA → sửa label/link → Lưu.
2. Mở **Kiểu**.
3. Thử Căn, Độ rộng = Full và Cỡ nút = Nhỏ/Vừa/Lớn.
4. Đặt Mobile khác Desktop.

Kỳ vọng:
- Nền/màu chữ thật của button không bị editor phủ sáng.
- Full width không làm chữ biến mất.
- Responsive override chỉ tác động breakpoint đã chọn.

## 3. Image

1. Hero hoặc Feature → **Kiểu ảnh**.
2. Desktop: Rộng + căn Giữa + Bo lớn.
3. Tablet: Kế thừa Desktop.
4. Mobile: Full khối + Bo nhẹ.
5. Lưu rồi refresh.

Kỳ vọng:
- Ảnh không bị upload/copy lại chỉ vì chỉnh style.
- Full khối hoạt động cho Hero/Feature.
- Bo góc áp vào ảnh, không phá toolbar đổi ảnh.

## 4. Custom Page

Lặp lại các case Heading/Text/Button/Image trên một Custom Page.

Kỳ vọng:
- Cùng UI **Kiểu** với Homepage.
- Dữ liệu vẫn lưu ở `_visual` của section hiện tại.
- Draft/published không bị đổi trạng thái ngoài ý muốn.

## 5. Nội dung lặp

Trên các section có item lặp:
- Feature: từng điểm nổi bật.
- FAQ: từng câu hỏi/trả lời.
- Location: từng địa điểm gần.
- Policy: từng tiêu đề/nội dung.

Mở inline edit rồi **Kiểu**, đổi một item và refresh.

Kỳ vọng: style của đúng item được giữ, không áp nhầm sang item kế bên.

## 6. Row Builder regression

1. Thêm Row 2 cột.
2. Sửa Heading/Text/Button/Image inline.
3. Drag element sang cột khác.
4. Undo/Redo.
5. Mở Builder kiểm tra Desktop/Tablet/Mobile.

Kỳ vọng: PR #43 không làm mất responsive config của Row và không phá stale guard.

## 7. Header / Footer regression

- Header: mở Sticky designer, thử checkbox Blur/Shadow/Sticky.
- Footer: bật Builder, sửa Text/Button/Image inline rồi mở Builder nâng cao.

Kỳ vọng:
- Checkbox giữ kích thước nhỏ, không bị input CSS chung kéo thành ô lớn.
- Modal/drawer có thể đóng bằng X/Hủy/Esc.
- Không có horizontal overflow ở toolbar/inspector.

## 8. Media regression

- Từ Hero/Row/Footer chọn ảnh Website và Room Image từ Media Library.
- Đóng Media bằng X/Esc.
- Mở lại và chọn ảnh khác.

Kỳ vọng: Media Library không bị style guard che/hide sai và usage vẫn cập nhật như PR #42.

## 9. Viewport

Kiểm tra nhanh ở:
- Desktop >= 1280px.
- Tablet khoảng 768–1024px.
- Mobile 390–430px.

Kỳ vọng:
- Toolbar, inline bar, style panel và drawer không ép trang cuộn ngang.
- Style panel mobile nằm trong viewport, phần nút Lưu/Hủy luôn truy cập được.

## Pass criteria

- Không mất nội dung khi chuyển từ inline edit sang chỉnh style.
- Không reload khi chỉ lưu style phần tử.
- Style public bền sau refresh và đúng breakpoint.
- Không regression Row/Header/Footer/Media.
- Build/test, JavaScript syntax, PostgreSQL Integration và migration check đều xanh.
