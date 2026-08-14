# Production readiness — De Long Homestay

Tài liệu này là checklist trung lập với nhà cung cấp hosting. Không bật production trước khi `/health/ready` trả `Healthy` và đã diễn tập restore ít nhất một lần.

## 0. Chạy local

Repo có launch profile mặc định cho Development. Lệnh sau dùng storage local `App_Data` + `wwwroot/uploads` và `/health/ready` phải `Healthy` khi PostgreSQL đang chạy:

```bash
dotnet run --project src/DeLong.Web
```

Nếu cố tình chạy `Production` ở local thì readiness sẽ yêu cầu storage production giống production thật. Có thể dùng cách đó để diễn tập deployment.

## 1. Cấu hình bắt buộc

Không commit secret vào Git. Production nên cấp secret bằng environment variables / secret store của host.

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require
AllowedHosts=your-domain.example

# DataRoot chứa ảnh gốc + Data Protection keys, phải tồn tại qua redeploy.
Storage__DataRoot=/srv/delong/data

# Ảnh public mặc định vẫn nằm trong wwwroot/uploads/rooms như khi development.
# appsettings.Production.json đã cấu hình giá trị này; chỉ override khi cần.
Storage__MediaPublicRoot=wwwroot/uploads/rooms
Storage__MediaRequestPath=/uploads/rooms
Storage__RequirePersistent=true

# Chỉ thêm nếu reverse proxy không chạy loopback/local.
ReverseProxy__KnownProxies__0=127.0.0.1

Database__AutoMigrate=false
Database__SeedOnStartup=false
```

`Storage__DataRoot` chứa ảnh gốc và ASP.NET Data Protection keys. `Storage__MediaPublicRoot` chứa WebP large/card/thumbnail được public ở `/uploads/rooms/...`.

De Long giữ media public ở `wwwroot/uploads/rooms` cho dễ quản lý. Khi deploy, cần bảo đảm đúng thư mục này **không bị xóa khi release mới được triển khai**: hoặc host giữ working directory, hoặc mount/persist riêng chính thư mục `wwwroot/uploads`. Backup script vẫn phải sao lưu nó.

## 2. Health checks

- `GET /health/live`: process ASP.NET đang chạy.
- `GET /health/ready`: kiểm tra PostgreSQL và khả năng ghi storage.

Trong Production, `Storage:RequirePersistent=true`. `Storage:DataRoot` phải được cấu hình explicit. `Storage:MediaPublicRoot` đã explicit trong `appsettings.Production.json` là `wwwroot/uploads/rooms`; readiness kiểm tra thư mục đó ghi được nhưng không thể tự chứng minh host có giữ filesystem sau redeploy, nên phần persistence vẫn phải được xác nhận trong UAT deployment.

Load balancer/monitor nên dùng `/health/ready` để quyết định instance có nhận traffic hay không.

## 3. Logging

Production ghi JSON ra stdout/stderr để hosting platform thu thập. Mỗi request có method, path, status, thời gian xử lý và trace id. Không log query string/body để giảm nguy cơ lộ SĐT, tên khách hay thông tin thanh toán.

## 4. Database migrations

`Database:AutoMigrate` phải để `false` ở production. Migration là bước release có chủ đích:

```bash
dotnet ef database update \
  --project src/DeLong.Web \
  --startup-project src/DeLong.Web
```

Trước migration phải backup database. Nếu migration lớn/rủi ro, tạo staging copy và chạy trước trên staging.

## 5. Backup

Backup cần gồm **hai phần**:

1. PostgreSQL — booking, khách, payment, room content, audit...
2. Runtime files — `Storage__DataRoot` + `wwwroot/uploads/rooms`.

Repo có script `scripts/backup-production.sh` và `scripts/backup-production.ps1`. Script dùng `DATABASE_URL` dạng PostgreSQL URI cho `pg_dump` và tạo checksum SHA-256.

Khuyến nghị tối thiểu khi go-live:

- DB backup hằng ngày.
- Giữ 7 bản daily + 4 bản weekly trở lên.
- Một bản sao nằm ngoài cùng máy chạy app.
- Test restore định kỳ; backup chưa từng restore thử chưa được coi là backup đáng tin cậy.

## 6. Restore rehearsal

Không diễn tập restore trực tiếp trên production database.

1. Tạo database rỗng/staging.
2. Set `DATABASE_URL` tới database staging.
3. Chạy script restore với cờ xác nhận.
4. Restore runtime files vào thư mục staging riêng.
5. Chạy app staging.
6. Kiểm tra `/health/ready`.
7. UAT: đăng nhập → lịch phòng → booking → payment → public rooms/images.

Ghi lại ngày restore, tên backup và kết quả trong nhật ký vận hành.

## 7. Media hiện có khi chuyển từ local lên production

Các ảnh đã upload local không nằm trong Git. Nếu production dùng cùng cấu trúc `wwwroot/uploads`, copy:

```text
src/DeLong.Web/App_Data/room-images/  -> Storage__DataRoot/room-images/
src/DeLong.Web/wwwroot/uploads/rooms/ -> <production content root>/wwwroot/uploads/rooms/
```

Database URL ảnh vẫn là `/uploads/rooms/...`, nên không cần rewrite record.

## 8. Reverse proxy / HTTPS

App đọc `X-Forwarded-For` và `X-Forwarded-Proto`. Chỉ khai báo proxy thực tế trong `ReverseProxy:KnownProxies`. HTTPS nên terminate ở reverse proxy/load balancer; cookie đăng nhập production luôn `Secure`.

## 9. Go-live gate

Chỉ go-live khi đủ:

- CI xanh.
- Production config không chứa secret trong repo.
- `/health/live` và `/health/ready` xanh.
- PostgreSQL backup + restore rehearsal PASS.
- `Storage__DataRoot` và `wwwroot/uploads/rooms` sống sót qua một lần redeploy staging.
- Tài khoản nhân viên thật và role đúng.
- Public booking end-to-end PASS.
- Nhân viên thực hiện được booking → giữ/xác nhận → thanh toán → nhận phòng → trả phòng → dọn phòng.
- Có người chịu trách nhiệm kiểm tra log/health và phục hồi backup khi cần.
