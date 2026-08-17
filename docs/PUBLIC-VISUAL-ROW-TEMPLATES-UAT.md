# Public Visual Row Template Library — UAT

## Điều kiện

- Đăng nhập Admin hoặc Manager có quyền chỉnh website.
- Bật **Chỉnh sửa trang**.
- PR Responsive Controls (#28) là branch cha của tính năng này.

## 1. Mở thư viện mẫu từ toolbar

1. Ở trang chủ public, bật edit mode.
2. Bấm **Mẫu Row** trên top toolbar.
3. Xác nhận thư viện mở dạng modal rộng, không làm mất trạng thái trang.
4. Thử tìm `hero`, `ảnh`, `CTA`.
5. Thử các filter: Hero, Nội dung, CTA, Hình ảnh, Tiện ích, Của tôi.

## 2. Dùng mẫu hệ thống

1. Chọn **Ảnh trái · nội dung phải** → **Dùng mẫu**.
2. Row Builder phải mở.
3. Xác nhận thao tác thay Row khi được hỏi.
4. Preview phải có 2 cột: ảnh trái, heading/text/button phải.
5. Chỉnh text và upload ảnh rồi lưu.
6. Reload và kiểm tra output public.

Lặp lại nhanh với:

- Hero nội dung trung tâm;
- 3 điểm nổi bật;
- 3 ảnh ngang;
- CTA nền tối;
- Banner ưu đãi.

## 3. Responsive của mẫu

1. Chọn Hero hoặc CTA.
2. Chuyển preview Desktop / Tablet / Mobile.
3. Heading phải có cỡ khác nhau theo breakpoint đã cài trong mẫu.
4. Chỉnh thêm responsive controls rồi lưu.
5. Resize trang public thật và so với preview.

## 4. Lưu Row thành mẫu riêng

1. Mở một Row đang chỉnh và thay đổi nội dung/layout.
2. Bấm **Lưu mẫu** trên header Row Builder.
3. Đặt tên, ví dụ `Giới thiệu cơ sở chuẩn`.
4. Mở **Thư viện mẫu** → filter **Của tôi**.
5. Mẫu vừa lưu phải xuất hiện.
6. Bấm **Dùng mẫu**, xác nhận Row Builder nhận đúng layout, nội dung và responsive metadata.

## 5. Tái sử dụng ở trang/cơ sở khác

1. Lưu một Row vào **Mẫu của tôi**.
2. Chuyển sang một trang cơ sở khác trên cùng trình duyệt.
3. Bật edit mode → **Mẫu Row** → **Của tôi**.
4. Mẫu phải vẫn xuất hiện và dùng được.

> `Mẫu của tôi` ở vòng này lưu bằng `localStorage`, tức là theo trình duyệt/profile hiện tại. Chưa đồng bộ giữa nhiều máy hoặc nhiều tài khoản.

## 6. Xóa mẫu riêng

1. Vào **Của tôi**.
2. Bấm **Xóa** trên một mẫu riêng.
3. Xác nhận xóa.
4. Mẫu phải biến mất nhưng không ảnh hưởng Row đã được tạo từ mẫu trước đó.

## 7. Regression

Sau khi dùng một template, kiểm tra lại:

- Quill Trực quan / HTML vẫn focus và gõ được;
- upload ảnh vẫn hoạt động;
- ↑ / ↓ / ← / → element;
- copy/paste element;
- undo/redo;
- copy/paste Row;
- responsive Desktop / Tablet / Mobile;
- Gallery / Blog editor vẫn bình thường.

## 8. Mobile

- Thư viện mẫu mở full-screen trên viewport nhỏ.
- Card template xuống 1 cột.
- Search/filter vẫn dùng được.
- Không có horizontal overflow.
