# Data model mục tiêu

## Property
- id
- name
- public_name
- address
- phone
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

## User / Role / PropertyAccess
Production dùng ASP.NET Core Identity hoặc mô hình authentication tương đương, không lưu plaintext password.

## AuditLog
Ghi các thay đổi quan trọng: booking, payment, status, settings, user action.
