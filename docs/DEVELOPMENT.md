# Development setup

## Yêu cầu

- .NET 10 SDK.
- PostgreSQL cài trực tiếp trên máy hoặc database server truy cập được.
- Không cần Docker.

## Database development

Tạo database riêng cho development và test:

```sql
CREATE DATABASE delong_dev;
CREATE DATABASE delong_test;
```

Không commit mật khẩu database. Tại project web:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=delong_dev;Username=postgres;Password=YOUR_PASSWORD" --project src/DeLong.Web
dotnet user-secrets set "Seed:AdminEmail" "admin@example.local" --project src/DeLong.Web
dotnet user-secrets set "Seed:AdminPassword" "CHANGE_TO_A_STRONG_LOCAL_PASSWORD" --project src/DeLong.Web
```

## Migration

Foundation commit chưa check-in migration vì migration phải được sinh/kiểm tra bằng .NET SDK thật sau khi model build pass.

```bash
dotnet tool install --global dotnet-ef --version 10.0.10
dotnet ef migrations add InitialCreate --project src/DeLong.Web --startup-project src/DeLong.Web --output-dir Data/Migrations
dotnet ef database update --project src/DeLong.Web --startup-project src/DeLong.Web
```

Sau khi migration tồn tại có thể bật local:

```bash
dotnet user-secrets set "Database:AutoMigrate" "true" --project src/DeLong.Web
dotnet user-secrets set "Database:SeedOnStartup" "true" --project src/DeLong.Web
```

Production deployment phải có bước migration rõ ràng, không dựa mù quáng vào auto-migrate.

## Chạy

```bash
dotnet restore DeLongHomestay.sln
dotnet build DeLongHomestay.sln
dotnet test DeLongHomestay.sln
dotnet run --project src/DeLong.Web
```

## Vue

Vue được pin version và load bằng global build trong `_Layout.cshtml`. Không cần npm/Vite ở baseline. Mỗi page interaction có JS riêng trong `wwwroot/js/pages/` và mount vào đúng page scope.
