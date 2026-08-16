# Production readiness — De Long Homestay

Tài liệu này là checklist trung lập với nhà cung cấp hosting. Không bật production trước khi `/health/ready` trả `Healthy` và đã diễn tập restore ít nhất một lần.

## 0. Chạy local

Repo có launch profile mặc định cho Development. Lệnh sau dùng storage local `App_Data` + `wwwroot/uploads` và `/health/ready` phải `Healthy` khi PostgreSQL đang chạy:

```bash
dotnet run --project src/DeLong.Web
```

Nếu cố tình chạy `Production` ở local, app vẫn dùng hai root rõ ràng trong `appsettings.Production.json`:

```text
Storage:DataRoot=App_Data
Storage:MediaPublicRoot=wwwroot/uploads/rooms
```

Vì vậy `/health/ready` không yêu cầu bạn phải tạo một volume ngoài source chỉ để chạy thử Production local.

## 1. Cấu hình production

Không commit secret vào Git. Production nên cấp secret bằng environment variables / secret store của host.

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require
AllowedHosts=your-domain.example

# Mặc định đã có trong appsettings.Production.json:
Storage__DataRoot=App_Data
Storage__MediaPublicRoot=wwwroot/uploads/rooms
Storage__MediaRequestPath=/uploads/rooms
Storage__RequirePersistent=true

# Chỉ thêm nếu reverse proxy không chạy loopback/local.
ReverseProxy__KnownProxies__0=127.0.0.1

Database__AutoMigrate=false
Database__SeedOnStartup=false
```

`Storage__DataRoot` chứa ảnh gốc và ASP.NET Data Protection keys. `Storage__MediaPublicRoot` chứa WebP large/card/thumbnail được public ở `/uploads/rooms/...`.

De Long giữ media public ở `wwwroot/uploads/rooms` cho dễ quản lý. Đây là cấu trúc mặc định cả Development lẫn Production. Khi deploy, cần bảo đảm `App_Data` và `wwwroot/uploads` **không bị xóa khi release mới được triển khai**. Với host dùng một working directory cố định thì có thể giữ nguyên như trên; với host thay toàn bộ release directory thì mount/persist riêng hai thư mục này hoặc override `Storage__DataRoot` / `Storage__MediaPublicRoot` sang vị trí bền vững.

## 2. Health checks

- `GET /health/live`: process ASP.NET đang chạy.
- `GET /health/ready`: kiểm tra PostgreSQL và khả năng ghi storage.

Trong Production, `Storage:RequirePersistent=true` yêu cầu hai storage root phải được cấu hình rõ ràng. `appsettings.Production.json` đã cấu hình sẵn `App_Data` và `wwwroot/uploads/rooms`, nên chạy Production local hoặc single-folder deployment không còn báo `Unhealthy` chỉ vì thiếu biến môi trường storage.

Readiness chỉ có thể xác nhận thư mục tồn tại và ghi được; nó không thể tự chứng minh hosting có giữ filesystem sau redeploy. Việc `App_Data` và `wwwroot/uploads` sống sót qua một lần redeploy staging vẫn là điều kiện UAT trước go-live.

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
2. Runtime files — `App_Data` + `wwwroot/uploads/rooms` hoặc các root đã override khi deploy.

Repo có `scripts/backup-production.sh` và `scripts/backup-production.ps1`. Script dùng `DATABASE_URL` dạng PostgreSQL URI cho `pg_dump` và tạo checksum SHA-256.

Khi dùng cấu trúc mặc định, đặt:

```text
DELONG_DATA_ROOT=src/DeLong.Web/App_Data
DELONG_MEDIA_ROOT=src/DeLong.Web/wwwroot/uploads/rooms
```

Trên server nên dùng absolute path tương ứng với thư mục deploy thật.

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
4. Restore `App_Data` và `wwwroot/uploads/rooms` vào thư mục staging riêng.
5. Chạy app staging.
6. Kiểm tra `/health/ready`.
7. UAT: đăng nhập → lịch phòng → booking → payment → public rooms/images.

Ghi lại ngày restore, tên backup và kết quả trong nhật ký vận hành.

## 7. Media hiện có khi chuyển từ local lên production

Các ảnh đã upload local không nằm trong Git. Nếu production dùng cùng cấu trúc mặc định, copy nguyên hai thư mục runtime:

```text
src/DeLong.Web/App_Data/                 -> <production content root>/App_Data/
src/DeLong.Web/wwwroot/uploads/rooms/   -> <production content root>/wwwroot/uploads/rooms/
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
- `App_Data` và `wwwroot/uploads/rooms` sống sót qua một lần redeploy staging.
- Tài khoản nhân viên thật và role đúng.
- Public booking end-to-end PASS.
- Nhân viên thực hiện được booking → giữ/xác nhận → thanh toán → nhận phòng → trả phòng → dọn phòng.
- Có người chịu trách nhiệm kiểm tra log/health và phục hồi backup khi cần.

## 10. Release stabilization guards

Production startup từ chối chạy nếu `Database:AutoMigrate=true`, `Database:SeedOnStartup=true`, `Storage:RequirePersistent=false`, hoặc storage roots không được cấu hình rõ. `AllowedHosts=*` và thiếu `ReverseProxy:KnownProxies` được ghi warning để không làm hỏng local/staging nhưng phải xử lý trước go-live.

`/health/ready` ngoài PostgreSQL + khả năng ghi storage còn kiểm tra **pending EF migrations** và dung lượng trống tối thiểu (`Storage:MinimumFreeSpaceMb`, production mặc định 256 MB). Health endpoint public mặc định không trả description chi tiết; chỉ bật `Operations:ExposeHealthDetails=true` khi thật sự cần chẩn đoán trong môi trường được bảo vệ.

Mỗi response có `X-Request-ID`; nếu upstream gửi request id hợp lệ thì app giữ lại để correlation. Request chậm hơn `Operations:SlowRequestThresholdMs` (mặc định 1500 ms) được log ở mức Warning.

Public booking gửi `Idempotency-Key` và lưu key theo từng cơ sở. Retry cùng key trả lại booking đã tạo thay vì tạo thêm một yêu cầu trùng; đây là lớp bảo vệ cho double-click/network retry, không thay thế rate limiting.

Sau deploy chạy smoke test không ghi dữ liệu:

```bash
./scripts/smoke-production.sh https://your-domain.example de-long
```

PowerShell:

```powershell
./scripts/smoke-production.ps1 -BaseUrl https://your-domain.example -SiteSlug de-long
```

### SMTP credential encryption

SMTP password được lưu trong PostgreSQL dưới dạng ASP.NET Core Data Protection ciphertext. Khi backup/restore môi trường có cấu hình email, phải backup và restore **cả database lẫn Data Protection key ring**. Nếu chỉ restore database sang key ring khác, password SMTP cũ không thể giải mã; Admin phải nhập lại password trong Cấu hình → Thông báo.

