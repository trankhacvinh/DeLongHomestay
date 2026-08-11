---
name: razor-migration
description: Quy ước production DeLongHomestay bằng .NET 10 Razor Pages, Vue progressive enhancement và PostgreSQL.
---

# Production Development Skill

## Kiến trúc cố định

- 1 production project: `src/DeLong.Web`.
- 1 test project: `tests/DeLong.Tests`.
- Razor Pages render shell/initial HTML.
- Vue 3 dùng in-DOM progressive enhancement trong `.cshtml`.
- Trong Razor ưu tiên `v-on:`, `v-bind:`, `v-model`, tránh shorthand `@click` gây xung đột Razor.
- Minimal APIs xử lý CRUD/mutation không cần reload.
- Feature code đặt gần nhau trong `Features/<Feature>/`.
- Service dùng `AppDbContext` trực tiếp; không tạo Repository Pattern chỉ để wrap EF Core.

## Frontend

- Mỗi Razor Page có Vue app scope nhỏ nếu cần interaction.
- Không Vue Router, Pinia, Alpine hoặc SPA shell nếu chưa có quyết định kiến trúc mới.
- `wwwroot/js/core/api.js` xử lý fetch, JSON, antiforgery và ProblemDetails thống nhất.
- Modal/confirm/loading/toast dùng Vue, không dùng browser `confirm()` cho UX chính.

## Backend

- Validate server-side dù client đã validate.
- Authorization theo role + property.
- Mutation API dùng antiforgery.
- Booking conflict phải được bảo vệ bằng transaction/database guard.
- Payment/audit/housekeeping là entity riêng.
- Không hard-delete dữ liệu nghiệp vụ đã phát sinh.

## Data

- PostgreSQL, snake_case.
- UUID/UUIDv7 cho ID mới.
- `decimal` cho tiền.
- UTC/timestamptz trong DB, hiển thị theo timezone property.
- Không Docker theo workflow của chủ dự án.
- Secrets dùng User Secrets/environment variables.

## Khi thay đổi kiến trúc

Cập nhật đồng thời `docs/ARCHITECTURE.md`, `docs/RAZOR-MIGRATION.md`, `docs/ROADMAP.md` và checklist liên quan.
