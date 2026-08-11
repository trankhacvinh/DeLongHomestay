# PostgreSQL local setup (không Docker)

## 1. Yêu cầu

- .NET 10 SDK.
- PostgreSQL cài trực tiếp trên máy.
- `dotnet-ef` 10.x.

Kiểm tra:

```bash
dotnet --version
psql --version
dotnet ef --version
```

Nếu chưa có EF CLI:

```bash
dotnet tool install --global dotnet-ef --version 10.0.10
```

## 2. Tạo database

Ví dụ bằng `psql` với tài khoản quản trị PostgreSQL:

```sql
CREATE DATABASE delong_dev;
CREATE DATABASE delong_test;
```

Có thể dùng một user riêng thay cho `postgres` khi triển khai thật.

## 3. Cấu hình User Secrets

Tại thư mục `src/DeLong.Web`:

```bash
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=delong_dev;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Seed:AdminEmail" "admin@delong.local"
dotnet user-secrets set "Seed:AdminPassword" "CHANGE_THIS_STRONG_PASSWORD"
```

Không commit password/connection string thật vào Git.

## 4. Apply migrations

Từ root repository:

```bash
dotnet restore DeLongHomestay.sln
dotnet ef database update --project src/DeLong.Web --startup-project src/DeLong.Web
```

Migrations sẽ tạo extension `btree_gist` và exclusion constraint chống booking overlap.

## 5. Seed dữ liệu development

Trong `src/DeLong.Web/appsettings.Development.json`, bật `Database:SeedOnStartup` khi cần seed De Long + 6 phòng và admin development.

Sau khi seed xong có thể tắt lại để startup không phải kiểm tra seed mỗi lần.

## 6. Chạy web

```bash
dotnet run --project src/DeLong.Web
```

Mở URL HTTPS/HTTP được in trong console.

## 7. Integration test database

Đặt biến môi trường riêng cho test:

### PowerShell

```powershell
$env:DELONG_TEST_CONNECTION="Host=localhost;Port=5432;Database=delong_test;Username=postgres;Password=YOUR_PASSWORD"
dotnet test tests/DeLong.Tests --filter "Category=Integration"
```

### bash/zsh

```bash
export DELONG_TEST_CONNECTION='Host=localhost;Port=5432;Database=delong_test;Username=postgres;Password=YOUR_PASSWORD'
dotnet test tests/DeLong.Tests --filter 'Category=Integration'
```

Không trỏ integration test vào `delong_dev` hoặc database production.
