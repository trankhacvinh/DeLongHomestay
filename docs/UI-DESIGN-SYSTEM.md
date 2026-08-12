# De Long Homestay — UI Design System

## Product direction

Production admin dùng phong cách **Boutique Hospitality Admin**: rõ nghiệp vụ, ấm hơn dashboard SaaS thông thường, ít trang trí nhưng có hierarchy mạnh.

## Nguyên tắc

- Razor Pages render shell/initial state; Vue chỉ progressive-enhance interaction.
- Không đổi business rules/API/database chỉ để phục vụ visual redesign.
- Calendar là màn hình vận hành quan trọng nhất; desktop ưu tiên mật độ thông tin và chiều ngang.
- Mobile ưu tiên navigation drawer, card/list và horizontal scroll có chủ đích.
- Mọi control có focus-visible state, disabled/loading state và target chạm tối thiểu khoảng 40px.
- Không dùng browser `confirm()` cho thao tác chính.
- Không trộn nhiều icon set; production shell dùng SVG icon sprite nội bộ.

## Design tokens

### Color

- `--brand-950 #0d2b2e`: navigation shell.
- `--brand-800 #174b4f`: primary action.
- `--brand-700 #1d5d60`: hover/secondary brand.
- `--brand-100 #dff0ed`: soft success/brand tint.
- `--sand-500 #c99655`: hospitality accent.
- `--page #f3f6f5`: app background.
- `--surface #ffffff`: card/modal.
- `--text #182322`: main text.
- `--muted #667472`: secondary text.
- `--line #dde5e2`: borders.
- Semantic colors: emerald/sage success, amber warning, muted red danger, slate info.

### Radius

- Controls: 10–12px.
- Cards: 16px.
- Large modal: 20px.
- Pills: 999px.

### Typography

- Page title: 30–34px / 750–800.
- Section title: 17–20px / 700.
- Body: 14px.
- Secondary: 12–13px.
- Numeric KPI: 28–34px / 750.

## Components

### App shell

Sidebar có 2 nhóm `Vận hành` và `Quản lý`, active state rõ, icon 18px. Finance group chỉ hiện cho role được phép. Mobile dùng drawer + overlay.

### Buttons

`btn-primary`, `btn-light`, `btn-danger`, `btn-ghost`, `btn-icon`. Không dùng native button không style trong production admin.

### Forms

Input/select/textarea dùng chung border/radius/focus ring. Label 12px semibold. Validation nằm ngay dưới field.

### Tables

Panel chứa toolbar riêng; header sticky khi hợp lý; row hover nhẹ; empty state có hierarchy. Không để native select/search phá design language.

### Modal

Overlay tối vừa phải + blur nhẹ, card 16–20px, header/body/footer tách rõ. Form dài chia section theo nghĩa nghiệp vụ.

### Calendar

- First column sticky, mỗi ngày có min-width đủ đọc.
- Empty cell chỉ hiện CTA rõ khi hover/focus, không lặp `+ Đặt` quá dày.
- Booking chip hiển thị giờ, khách, số tiền/trạng thái.
- Status được phân biệt bằng surface + border, không dựa duy nhất vào màu chữ.
- Today có tint riêng.

## Rollout

1. UI-1: tokens, app shell, controls, modal, table.
2. UI-2: Dashboard, Calendar, Booking editor/detail.
3. UI-3: Customers, Housekeeping, Rooms, Settings.
4. UI-4: Finance, Reports, mobile/accessibility polish.
