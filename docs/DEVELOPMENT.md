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

## DataRoot development ổn định

Mặc định nếu không cấu hình, development dùng `src/DeLong.Web/App_Data`. Cách này tiện lúc bắt đầu nhưng không ổn định nếu đổi thư mục checkout, xóa source hoặc chạy `git clean`: Data Protection keys, dữ liệu private của booking và các khóa lưu trong DataRoot có thể thay đổi trong khi browser vẫn giữ cookie cũ.

Nên cấu hình **một đường dẫn tuyệt đối nằm ngoài repository** bằng User Secrets. Ví dụ Windows:

```bash
dotnet user-secrets set "Storage:DataRoot" "C:\DeLongHomestayData" --project src/DeLong.Web
```

Linux/macOS dùng một đường dẫn tuyệt đối thuộc user hiện tại, ví dụ:

```bash
dotnet user-secrets set "Storage:DataRoot" "/home/YOUR_USER/.local/share/DeLongHomestay" --project src/DeLong.Web
```

Sau khi đặt một lần, cùng browser có thể tiếp tục dùng auth/Data Protection state qua các lần pull, rebuild và checkout source khác nhau. Không commit DataRoot hoặc master key vào Git.

## Cache khi phát triển

Trong `Development`, toàn bộ static asset do ASP.NET Core phục vụ (JS, CSS, ảnh trong `wwwroot` và room media public) trả:

```text
Cache-Control: no-store,no-cache,max-age=0,must-revalidate
Pragma: no-cache
Expires: 0
X-DeLong-Cache-Policy: development-no-store
```

Vì vậy khi sửa JS/CSS local **không cần tăng `?v=...`, đổi trình duyệt, mở ẩn danh hoặc Clear Site Data** chỉ để thấy file mới. `asp-append-version` và các query version hiện có vẫn được giữ cho production, nhưng Development không biến chúng thành immutable cache.

Có thể kiểm tra trong DevTools → Network → chọn một file `.js`/`.css` → Response Headers. Header `X-DeLong-Cache-Policy: development-no-store` xác nhận app đang chạy cache policy development mới.

Nếu browser đang giữ một asset `immutable` từ một build cũ trước thay đổi này, chỉ cần hard refresh một lần sau khi lấy code mới. Các response static sau đó sẽ không còn được lưu lại ở Development.

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
