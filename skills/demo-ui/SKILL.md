---
name: demo-ui
description: Quy ước phát triển HTML/CSS/JS localStorage demo De Long Homestay.
---

# Demo UI Skill

## Scope

`demo/` là prototype UX có khả năng chạy trên GitHub Pages, không phải production security model.

## Quy tắc

- HTML/CSS/JavaScript thuần, ES Modules, không build step.
- Shared CSS ở `demo/assets/css/styles.css`.
- Seed data ở `data.js`; mọi mutation đi qua `store.js`.
- Page module ở `assets/js/pages/` không gọi `localStorage.setItem` trực tiếp.
- Link dùng relative path để chạy đúng trong GitHub project Pages.
- Responsive trước khi thêm hiệu ứng trang trí.
- Admin desktop ưu tiên lịch rộng; mobile ưu tiên card/table scroll hợp lý.
- Không gom toàn bộ app vào một `index.html`.
- Không dùng dependency CDN nếu chức năng có thể làm bằng native browser API.

## Definition of done

- Không lỗi JS console khi load page chính.
- Link nội bộ không 404.
- localStorage survive refresh.
- Booking conflict test pass.
- Demo data reset/export/import vẫn hoạt động.
