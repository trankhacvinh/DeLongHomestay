# Data model mục tiêu

## Customer accounts và loyalty

- `asp_net_users.is_customer_account`: phân tách tài khoản khách khỏi đăng nhập quản trị.
- `customer_account_links`: duy nhất theo user/cơ sở và duy nhất theo customer.
- `customer_account_settings`: đăng ký, Authenticator, quy đổi điểm và điều khoản có phiên bản theo cơ sở.
- `customer_account_terms_acceptances`: bằng chứng đồng ý theo user/cơ sở/phiên bản.
- `loyalty_ledger_entries`: điểm có dấu, user, cơ sở, booking tùy chọn và lý do; booking duy nhất khi có.

## Property
- id
- name
- public_name
- address
- phone
- housekeeping_before_check_in_minutes (mặc định 0)
- housekeeping_after_check_out_minutes (mặc định 0)
- fanpage
- active

## Room
- id
- property_id
- code
- name
- capacity
- beds
- has_bathtub
- description
- guest_guide_html: hướng dẫn check-in, sử dụng phòng và quy định dành cho khách; HTML đã được làm sạch trước khi lưu
- housekeeping_status
- active

## RoomRatePreset
- id
- room_id
- label
- start_time
- end_time
- price
- sort_order

Preset không phải booking slot cứng. Booking được phép dùng giờ khác preset.

## Customer
- id
- name
- phone (normalized/indexed)
- citizen_id (production cần policy bảo vệ dữ liệu cá nhân)
- created_at

## Booking
- id
- property_id
- room_id
- customer_id
- source_id
- method
- check_in
- check_out
- base_price
- surcharge
- total_amount
- status
- note
- created_by
- created_at
- row_version/concurrency metadata

### Status đề xuất
`pending → confirmed → checked-in → completed`

Nhánh phụ: `cancelled`, `rejected`.

## Payment
- id
- booking_id
- paid_at
- amount
- method
- note
- created_by

`balance = booking.total_amount - SUM(payment.amount)`.

## Expense
- id
- property_id
- spent_at
- expense_category_id
- content
- amount
- note
- created_by

## HousekeepingTask
- id
- room_id
- booking_id nullable
- status (`dirty`, `cleaning`, `clean`)
- assigned_to nullable
- started_at nullable
- completed_at nullable
- note

Housekeeping Schedule V2 hiện là projection từ `Booking.check_in/check_out` và hai offset trên `Property`; chưa tạo hàng task trùng lặp trong database.

## User / Role / PropertyAccess
Production dùng ASP.NET Core Identity hoặc mô hình authentication tương đương, không lưu plaintext password.

## AuditLog
Ghi các thay đổi quan trọng: booking, payment, status, settings, user action.
