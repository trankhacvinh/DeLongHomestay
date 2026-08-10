# DeLongHomestay

Demo chuyển đổi quy trình quản lý De Long Homestay từ Excel sang web app.

## Mục tiêu

- Giữ cách vận hành quen thuộc của file Excel nhưng giảm thao tác thủ công.
- Lịch phòng là màn hình trung tâm.
- Booking lưu check-in/check-out thực tế; khung giờ chỉ là preset nhập nhanh.
- Tách booking, khách hàng, thanh toán, dọn phòng và chi phí thành các thực thể riêng.
- Bản demo dùng HTML/CSS/JavaScript thuần + `localStorage` để chốt UX trước khi làm production bằng ASP.NET Core Razor Pages + PostgreSQL.

## Chạy demo

Mở qua web server tĩnh (không nên mở trực tiếp bằng `file://` vì ES Modules có thể bị trình duyệt chặn):

```bash
cd demo
python -m http.server 8080
```

Sau đó truy cập `http://localhost:8080/`.

### Admin demo

- URL: `demo/admin/login.html`
- User: `admin`
- Password: `demo123`

> Tài khoản trên chỉ là giả lập UX. GitHub Pages là static hosting, không có authentication thật.

## GitHub Pages

Nếu Pages publish từ branch `main`, folder `/` thì root `index.html` sẽ chuyển vào `demo/index.html`. Demo cũng truy cập trực tiếp tại `/demo/`.

## Cấu trúc

```text
DeLongHomestay/
├── index.html                 # redirect tiện cho GitHub Pages
├── demo/                      # toàn bộ HTML demo
│   ├── index.html             # trang khách
│   ├── rooms.html
│   ├── room-detail.html
│   ├── booking.html
│   ├── booking-success.html
│   ├── admin/
│   └── assets/
├── docs/                      # tài liệu quản trị/phân tích/chuyển production
├── skills/                    # project skills cho các lần phát triển tiếp theo
├── AGENTS.md                  # quy tắc làm việc chung
└── README.md
```

## Dữ liệu Excel đã chuẩn hóa

Demo lấy cấu trúc từ 2 file quản lý hiện tại và chuẩn hóa 6 phòng. Hai lỗi nhập liệu rõ ràng đã được sửa trong dữ liệu seed:

- Moon Stone #4: `11:30-14:03` → `11:30-14:30`.
- La Roman #6: `00:00-15:00` → `12:00-15:00`.

Xem chi tiết: [`docs/EXCEL-MAPPING.md`](docs/EXCEL-MAPPING.md).

## Tài liệu chính

- [`docs/ADMIN-GUIDE.md`](docs/ADMIN-GUIDE.md) – hướng dẫn dùng demo.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) – kiến trúc demo và production.
- [`docs/DATA-MODEL.md`](docs/DATA-MODEL.md) – mô hình dữ liệu mục tiêu.
- [`docs/ROADMAP.md`](docs/ROADMAP.md) – lộ trình Razor Pages/PostgreSQL.
- [`docs/CHECKLIST.md`](docs/CHECKLIST.md) – checklist nghiệm thu.
- [`docs/RAZOR-MIGRATION.md`](docs/RAZOR-MIGRATION.md) – mapping demo → Razor Pages.
