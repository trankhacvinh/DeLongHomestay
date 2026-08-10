---
name: razor-migration
description: Quy ước chuyển prototype sang ASP.NET Core Razor Pages + PostgreSQL.
---

# Razor Migration Skill

## Mục tiêu

Port UX đã nghiệm thu, không thiết kế lại tùy tiện.

## Mapping

- Static page → Razor Page.
- `store.js` mutation → Application Service/Page Handler.
- `data.js` → database seed/migration.
- CSS class/tokens giữ tối đa.
- PostgreSQL dùng normalized tables; không mang JSON blob từ Excel nếu không có lý do domain rõ ràng.

## Backend rules

- Validate server-side.
- Transaction cho booking conflict/payment state changes.
- Authorization theo role + property.
- Audit sensitive mutations.
- UTC strategy phải được quyết định rõ; UI hiển thị timezone Việt Nam.
- Database indexes tối thiểu cho room/check-in/check-out, customer phone, booking status/property.

## Không làm

- Không port localStorage auth.
- Không lưu plaintext password.
- Không dựa vào client-side conflict check.
