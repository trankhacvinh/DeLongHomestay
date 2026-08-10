# Kiến trúc

## 1. Demo hiện tại

```text
HTML pages
   ↓
Page modules (assets/js/pages/*.js)
   ↓
Domain/storage API (assets/js/store.js)
   ↓
localStorage
```

`data.js` chỉ chứa seed data. `store.js` chịu trách nhiệm đọc/ghi, kiểm tra trùng lịch, tạo booking, payment, expense và housekeeping. Page module không được tự ghi localStorage trực tiếp.

## 2. Production mục tiêu

```text
Browser
   ↓ HTTPS
ASP.NET Core Razor Pages
   ├── PageModels
   ├── Application services
   ├── Domain rules
   └── EF Core / Npgsql
          ↓
      PostgreSQL
```

## 3. Nguyên tắc migration

- Giữ HTML structure, CSS tokens và UX flow càng nhiều càng tốt.
- Thay `store.js` bằng endpoint/Page Handler/Application Service.
- Validation ở browser chỉ để UX; production phải validate lại server-side.
- Conflict booking phải được bảo vệ ở transaction/database level, không chỉ JavaScript.
- Payment, housekeeping và audit log là entity riêng.

## 4. Phân quyền mục tiêu

- Admin: toàn quyền.
- Manager: booking, finance, report, settings giới hạn.
- Staff: booking/customer/housekeeping vận hành.
- Housekeeping: trạng thái dọn phòng.
- Investor/Viewer: chỉ đọc báo cáo được cấp quyền.

## 5. Multi-property

Schema có `property_id` ngay từ đầu. UI chỉ hiển thị selector cơ sở khi user quản lý nhiều hơn 1 cơ sở.
