# DeLongHomestay

Web app quản lý De Long Homestay, chuyển dần từ quy trình Excel sang ASP.NET Core.

## Hai phần của repository

- `demo/`: prototype HTML/JavaScript + localStorage để chốt UI/UX và nghiệp vụ.
- `src/DeLong.Web/`: ứng dụng production .NET 10 đang được xây dựng.

## Production stack

- ASP.NET Core Razor Pages (.NET 10).
- Vue 3 in-DOM progressive enhancement trong Razor Pages.
- Minimal APIs cho CRUD/mutation không cần reload trang.
- EF Core + Npgsql + PostgreSQL.
- ASP.NET Core Identity + cookie authentication.
- 1 production project + 1 test project.
- Không Docker.

## Cấu trúc

```text
DeLongHomestay/
├── demo/
├── docs/
├── skills/
├── src/
│   └── DeLong.Web/
│       ├── Pages/
│       ├── Domain/
│       ├── Features/
│       ├── Data/
│       ├── Identity/
│       ├── Common/
│       └── wwwroot/
├── tests/
│   └── DeLong.Tests/
└── DeLongHomestay.sln
```

## Trạng thái production

Foundation đầu tiên gồm Identity/PostgreSQL wiring; Property/Room/RoomRate/UserPropertyAccess; seed definition De Long + 6 phòng; Razor admin shell; Vue + `api.js` + antiforgery; Rooms feature mẫu; và CI restore/build/test.

Initial EF migration sẽ được tạo sau khi foundation build pass bằng .NET SDK thật. Xem `docs/DEVELOPMENT.md`.

## Tài liệu

- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/RAZOR-MIGRATION.md`
- `docs/ROADMAP.md`
- `docs/CHECKLIST.md`

Demo HTML cũ vẫn được giữ nguyên làm UI/UX reference; không mang localStorage/auth demo sang production.
