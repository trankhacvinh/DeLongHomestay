# Deploy De Long Homestay lên Ubuntu VPS (không Docker)

Tài liệu này áp dụng cho project `src/DeLong.Web` hiện chạy .NET 10, PostgreSQL, Razor Pages và Nginx. Quy trình dùng các thư mục release bất biến cùng symlink `current`; dữ liệu upload, Data Protection keys và database nằm ngoài release để không mất khi cập nhật.

> Các giá trị viết dạng `<...>` phải được thay bằng giá trị thật. Không chép nguyên dấu `<` và `>` vào cấu hình.

## 1. Kiến trúc triển khai

```text
/var/www/delong/
├── current -> /var/www/delong/releases/20260914T120000Z
└── releases/
    ├── 20260913T090000Z/
    └── 20260914T120000Z/

/var/lib/delong/
├── App_Data/                 # ảnh gốc, file riêng, Data Protection keys
└── uploads/rooms/            # ảnh public đã xử lý

/etc/delong/delong.env        # secret và cấu hình production
/var/backups/delong/          # backup tạm trên VPS; phải sao chép thêm ra máy khác
/etc/systemd/system/delong.service
/etc/nginx/sites-available/delong
```

App chỉ lắng nghe `127.0.0.1:5080`. Nginx nhận HTTP/HTTPS công khai và chuyển tiếp vào Kestrel.

## 2. Chuẩn bị VPS lần đầu

Khuyến nghị Ubuntu 24.04 LTS hoặc phiên bản Ubuntu còn được hỗ trợ.

```bash
sudo apt update
sudo apt upgrade -y
sudo apt install -y nginx postgresql postgresql-contrib curl ca-certificates tar rsync
sudo apt install -y aspnetcore-runtime-10.0

dotnet --list-runtimes
nginx -v
psql --version
```

Nếu Ubuntu chưa tìm thấy `aspnetcore-runtime-10.0`, làm theo mục cài .NET 10 dành đúng phiên bản Ubuntu trong tài liệu Microsoft; không trộn package .NET từ nhiều repository.

Tạo tài khoản chạy ứng dụng và thư mục:

```bash
sudo useradd --system --home /var/lib/delong --shell /usr/sbin/nologin delong
sudo install -d -o delong -g delong -m 750 /var/www/delong/releases
sudo install -d -o delong -g delong -m 750 /var/lib/delong/App_Data
sudo install -d -o delong -g delong -m 750 /var/lib/delong/uploads/rooms
sudo install -d -o root -g delong -m 750 /etc/delong
sudo install -d -o root -g root -m 700 /var/backups/delong
```

Không chạy ứng dụng bằng `root` hoặc bằng user `postgres`.

## 3. Tạo PostgreSQL database

Mở `psql` bằng tài khoản quản trị:

```bash
sudo -u postgres psql
```

Trong `psql`, thay mật khẩu bằng chuỗi mạnh riêng cho production:

```sql
CREATE ROLE delong_app LOGIN PASSWORD '<MAT_KHAU_DATABASE_MANH>';
CREATE DATABASE delong_prod OWNER delong_app ENCODING 'UTF8';
\c delong_prod
CREATE EXTENSION IF NOT EXISTS btree_gist;
\q
```

Chỉ mở cổng PostgreSQL ra Internet nếu database nằm trên máy khác và đã cấu hình firewall, TLS cùng `pg_hba.conf`. Nếu PostgreSQL cùng VPS, giữ kết nối qua `127.0.0.1:5432`.

## 4. Tạo file cấu hình bí mật

Tạo `/etc/delong/delong.env`:

```bash
sudo nano /etc/delong/delong.env
```

Nội dung mẫu:

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5080
ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=delong_prod;Username=delong_app;Password=<MAT_KHAU_DATABASE_MANH>"
AllowedHosts=delong.pmedia.vn
ReverseProxy__KnownProxies__0=127.0.0.1
Database__AutoMigrate=false
Database__SeedOnStartup=false
Storage__DataRoot=/var/lib/delong/App_Data
Storage__MediaPublicRoot=/var/lib/delong/uploads/rooms
Storage__MediaRequestPath=/uploads/rooms
Storage__RequirePersistent=true
Storage__MinimumFreeSpaceMb=256
Ai__GloballyEnabled=true

# Dùng cho scripts/backup-production.sh. Password trong URI phải URL-encode.
DATABASE_URL="postgresql://delong_app:<MAT_KHAU_URL_ENCODED>@127.0.0.1:5432/delong_prod"
DELONG_BACKUP_DIR=/var/backups/delong
DELONG_DATA_ROOT=/var/lib/delong/App_Data
DELONG_MEDIA_ROOT=/var/lib/delong/uploads/rooms
```

Khóa quyền đọc:

```bash
sudo chown root:delong /etc/delong/delong.env
sudo chmod 640 /etc/delong/delong.env
```

Không lưu API key, mật khẩu database, SMTP hoặc Pay2S vào Git. `appsettings.json` trong source không được coi là nơi chứa secret production; environment variables ở trên phải override toàn bộ giá trị thật.

## 5. Build gói release trên máy phát triển/CI

Máy build cần .NET SDK 10 và `dotnet-ef` cùng major version 10.

```bash
dotnet tool install --global dotnet-ef --version '10.*'
dotnet restore
dotnet test tests/DeLong.Tests/DeLong.Tests.csproj -c Release --no-restore

DELONG_BUILD_ID="$(date -u +%Y%m%dT%H%M%SZ)"
DELONG_PUBLISH_DIR="artifacts/publish-$DELONG_BUILD_ID"
mkdir -p artifacts
mkdir "$DELONG_PUBLISH_DIR"
dotnet publish src/DeLong.Web/DeLong.Web.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -p:BuildId="$DELONG_BUILD_ID" \
  -o "$DELONG_PUBLISH_DIR"

dotnet ef migrations bundle \
  --project src/DeLong.Web \
  --startup-project src/DeLong.Web \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained \
  --output "$DELONG_PUBLISH_DIR/efbundle"
  
  dotnet ef migrations bundle \
  --configuration Release \
  --target-runtime linux-x64 \
  --self-contained \
  --force \
  --output "efbundle"

mkdir -p "$DELONG_PUBLISH_DIR/scripts"
cp scripts/backup-production.sh scripts/restore-production.sh scripts/smoke-production.sh "$DELONG_PUBLISH_DIR/scripts/"
chmod +x "$DELONG_PUBLISH_DIR/efbundle"
chmod +x "$DELONG_PUBLISH_DIR"/scripts/*.sh
tar -C "$DELONG_PUBLISH_DIR" -czf artifacts/delong-release.tar.gz .
sha256sum artifacts/delong-release.tar.gz
```

Nếu VPS ARM64, thay toàn bộ `linux-x64` bằng `linux-arm64`. Không dùng artifact x64 trên ARM64.

Upload artifact:

```bash
scp artifacts/delong-release.tar.gz <SSH_USER>@<VPS_IP>:/tmp/
```

Không build trực tiếp từ nhánh đang làm dở trên VPS. Chỉ deploy commit/tag đã test và ghi lại commit tương ứng với artifact.

## 6. Cài release đầu tiên

Trên VPS:

```bash
RELEASE_ID="$(date -u +%Y%m%dT%H%M%SZ)"
RELEASE_DIR="/var/www/delong/releases/$RELEASE_ID"

sudo install -d -o delong -g delong -m 750 "$RELEASE_DIR"
sudo tar -xzf /tmp/delong-release.tar.gz -C "$RELEASE_DIR"
sudo chown -R delong:delong "$RELEASE_DIR"
sudo chmod 750 "$RELEASE_DIR/efbundle"
```

Chạy migration bằng cấu hình production. Lần đầu chưa có service nên chưa cần dừng app:

```bash
sudo -u delong bash -c '
  set -a
  source /etc/delong/delong.env
  set +a
  cd '"$RELEASE_DIR"'
  ./efbundle --verbose
'
```

Chỉ trỏ `current` sau khi migration thành công:

```bash
sudo ln -sfn "$RELEASE_DIR" /var/www/delong/current
sudo chown -h delong:delong /var/www/delong/current
```

## 7. Cấu hình systemd

Tạo `/etc/systemd/system/delong.service`:

```ini
[Unit]
Description=De Long Homestay ASP.NET Core
After=network-online.target postgresql.service
Wants=network-online.target

[Service]
Type=simple
User=delong
Group=delong
WorkingDirectory=/var/www/delong/current
ExecStart=/usr/bin/dotnet /var/www/delong/current/DeLong.Web.dll
EnvironmentFile=/etc/delong/delong.env
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=delong
NoNewPrivileges=true
PrivateTmp=true
UMask=0027

[Install]
WantedBy=multi-user.target
```

Nạp và chạy:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now delong
sudo systemctl status delong --no-pager
sudo journalctl -u delong -n 100 --no-pager
curl --fail --silent http://127.0.0.1:5080/health/live
curl --fail --silent http://127.0.0.1:5080/health/ready
curl --fail --silent http://127.0.0.1:5080/version
```

`/health/ready` chỉ xanh khi PostgreSQL kết nối được, không còn pending migration, storage ghi được và còn đủ dung lượng.
`/version` trả về version, build ID và commit Git nằm trong artifact. Sau mỗi lần deploy, đối chiếu `buildId` với `DELONG_BUILD_ID` đã dùng khi publish; endpoint đặt `no-store` để tránh đọc nhầm dữ liệu cache.

## 8. Cấu hình Nginx

Tạo `/etc/nginx/sites-available/delong`:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name delong.pmedia.vn;

    client_max_body_size 300M;

    location / {
        proxy_pass http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
    }
}
```

Nếu domain đi qua Cloudflare, Nginx phải chuyển IP khách thật cho ASP.NET thay vì IP edge Cloudflare; nếu không mọi khách có thể dùng chung một rate-limit bucket. Khi origin chỉ cho phép Cloudflare truy cập, đặt thêm trong `http {}`:

```nginx
map $http_cf_connecting_ip $delong_client_ip {
    default $http_cf_connecting_ip;
    ""      $remote_addr;
}
```

Và thay dòng `X-Forwarded-For` trong `location /` bằng:

```nginx
proxy_set_header X-Forwarded-For $delong_client_ip;
```

Ứng dụng chỉ tin proxy Nginx loopback qua `ReverseProxy__KnownProxies__0=127.0.0.1`. Không tin trực tiếp `CF-Connecting-IP` khi origin vẫn mở công khai vì client có thể tự giả header này.

### Giới hạn tần suất (rate limit)

Mỗi giới hạn public tính theo IP khách trong 10 phút. Giai đoạn beta giữ mức thoáng mặc định; muốn siết hoặc nới thì thêm vào `/etc/delong/delong.env` rồi restart service, không cần build lại:

| Biến môi trường | Mặc định | Áp dụng cho |
| --- | --- | --- |
| `RateLimiting__PublicBooking` | 120 | Gửi booking |
| `RateLimiting__PublicBookingIdentity` | 240 | Tải ảnh CCCD |
| `RateLimiting__PublicBookingCompletion` | 120 | Hoàn tất booking |
| `RateLimiting__PublicLookup` | 120 | Tra cứu booking, PDF hướng dẫn |
| `RateLimiting__PublicVoucher` | 120 | Kiểm tra voucher |
| `RateLimiting__AccountStatus` | 300 | Kiểm tra số điện thoại tài khoản khách |
| `RateLimiting__AccountLogin` | 60 | Đăng nhập/đăng ký tài khoản khách |

Mỗi lần chặn, log có dòng `Rate limit rejected ... from <IP>`. Nếu IP trong các dòng này luôn là IP của Cloudflare hoặc `127.0.0.1` thì Nginx chưa chuyển IP khách thật (xem cấu hình phía trên), nghĩa là mọi khách đang dùng chung một giới hạn.

Kích hoạt:

```bash
sudo ln -sfn /etc/nginx/sites-available/delong /etc/nginx/sites-enabled/delong
sudo nginx -t
sudo systemctl reload nginx
```

Sau khi DNS đã trỏ về VPS, cài HTTPS bằng Certbot hoặc cơ chế TLS đang dùng trên server. Ví dụ:

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d delong.pmedia.vn
sudo certbot renew --dry-run
```

Mở firewall tối thiểu:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw enable
sudo ufw status
```

Không mở cổng `5080` ra Internet.

## 9. Quy trình cập nhật code và database

### 9.1 Trên máy build

1. Chốt commit/tag cần phát hành.
2. Chạy test Release.
3. Tạo lại `publish`, `efbundle`, checksum và tar như mục 5.
4. Upload tar lên `/tmp` trên VPS.

### 9.2 Trên VPS

Lưu đường dẫn release hiện tại:

```bash
PREVIOUS_RELEASE="$(readlink -f /var/www/delong/current)"
echo "$PREVIOUS_RELEASE"
```

Giải nén release mới nhưng chưa đổi symlink:

```bash
RELEASE_ID="$(date -u +%Y%m%dT%H%M%SZ)"
RELEASE_DIR="/var/www/delong/releases/$RELEASE_ID"
sudo install -d -o delong -g delong -m 750 "$RELEASE_DIR"
sudo tar -xzf /tmp/delong-release.tar.gz -C "$RELEASE_DIR"
sudo chown -R delong:delong "$RELEASE_DIR"
sudo chmod 750 "$RELEASE_DIR/efbundle"
```

Backup trước mọi migration:

```bash
sudo bash -c '
  set -a
  source /etc/delong/delong.env
  set +a
  /var/www/delong/current/scripts/backup-production.sh
'
```

Kiểm tra thư mục backup mới có `database.dump`, archive runtime và `SHA256SUMS` trước khi tiếp tục.

Dừng app để tránh code cũ ghi dữ liệu trong lúc đổi schema, chạy migration từ release mới, rồi chuyển symlink:

```bash
sudo systemctl stop delong

sudo -u delong bash -c '
  set -a
  source /etc/delong/delong.env
  set +a
  cd '"$RELEASE_DIR"'
  ./efbundle --verbose
'

sudo ln -sfn "$RELEASE_DIR" /var/www/delong/current
sudo chown -h delong:delong /var/www/delong/current
sudo systemctl start delong
```

Nếu `efbundle` lỗi: **không đổi symlink**, chạy lại app cũ và đọc log. Không dùng `dotnet ef database update <migration-cũ>` tùy tiện trên production.

### 9.3 Xác minh sau cập nhật

```bash
sudo systemctl status delong --no-pager
sudo journalctl -u delong -n 150 --no-pager
curl --fail --silent http://127.0.0.1:5080/health/live
curl --fail --silent http://127.0.0.1:5080/health/ready
curl --fail --silent https://delong.pmedia.vn/health/ready

cd /var/www/delong/current
./scripts/smoke-production.sh https://delong.pmedia.vn de-long
```

Kiểm tra migration trực tiếp nếu cần:

```bash
sudo -u delong bash -c '
  set -a
  source /etc/delong/delong.env
  set +a
  psql "$DATABASE_URL" -c '\''SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY migration_id DESC LIMIT 10;'\''
'
```

`/health/ready = Healthy` là kiểm tra quan trọng nhất vì application model hiện tại tự so sánh danh sách migration trong binary với database. Chỉ thấy service `active` chưa chứng minh database mới nhất.

Sau đó UAT tối thiểu:

- Đăng nhập Admin.
- Mở lịch V1 và V2.
- Xem chi tiết một booking.
- Mở trang khách và ảnh phòng.
- Tạo một booking thử trên staging; không tạo dữ liệu thử trên production nếu chưa có quy trình hủy/đối soát.
- Nếu release thay đổi Pay2S, SMTP, Telegram hoặc AI provider, kiểm tra riêng bằng tài khoản/test mode phù hợp.

## 10. Rollback

### Chỉ rollback code, không có migration hoặc migration tương thích ngược

```bash
sudo systemctl stop delong
sudo ln -sfn "$PREVIOUS_RELEASE" /var/www/delong/current
sudo chown -h delong:delong /var/www/delong/current
sudo systemctl start delong
curl --fail --silent http://127.0.0.1:5080/health/ready
```

### Release đã đổi schema không tương thích

1. Giữ service dừng.
2. Xác định đúng backup tạo ngay trước release.
3. Restore database và hai thư mục runtime theo `scripts/restore-production.sh` vào trạng thái trước release.
4. Trỏ `current` về release cũ.
5. Start service và chạy health/smoke.

Restore database sẽ làm mất mọi giao dịch phát sinh sau thời điểm backup. Nếu hệ thống đã nhận booking/payment sau deploy, không restore mù quáng; phải đối soát và lập kế hoạch phục hồi dữ liệu trước.

Không xóa migration đã chạy khỏi source, không sửa file migration cũ và không xóa dòng trong `__EFMigrationsHistory` để “ép” database khớp.

## 11. Backup định kỳ

Có thể chạy thủ công:

```bash
sudo bash -c '
  set -a
  source /etc/delong/delong.env
  set +a
  /var/www/delong/current/scripts/backup-production.sh
'
```

Cấu hình cron bằng `sudo crontab -e`, ví dụ 02:30 mỗi ngày:

```cron
30 2 * * * set -a; . /etc/delong/delong.env; set +a; /var/www/delong/current/scripts/backup-production.sh >> /var/log/delong-backup.log 2>&1
```

Backup trên cùng VPS không bảo vệ được khi VPS hoặc ổ đĩa hỏng. Đồng bộ bản backup mã hóa sang một máy/kho lưu trữ khác và diễn tập restore định kỳ.

## 12. Chẩn đoán nhanh

```bash
# Log ứng dụng
sudo journalctl -u delong -f

# 200/Healthy nhưng Nginx lỗi
sudo nginx -t
sudo tail -n 100 /var/log/nginx/error.log

# App có nghe đúng loopback không
sudo ss -lntp | grep 5080

# Dung lượng đĩa
df -h

# Quyền thư mục runtime
sudo -u delong test -w /var/lib/delong/App_Data && echo writable
sudo -u delong test -w /var/lib/delong/uploads/rooms && echo writable

# Runtime đã cài
dotnet --list-runtimes
```

Các nguyên nhân thường gặp:

| Hiện tượng | Kiểm tra |
|---|---|
| Service `active` nhưng `/health/ready` lỗi | Pending migration, PostgreSQL hoặc quyền ghi storage |
| HTTP 502 từ Nginx | Service dừng, sai port hoặc Kestrel chưa khởi động |
| Đăng nhập/giải mã SMTP lỗi sau chuyển server | Thiếu `App_Data`/Data Protection key ring cũ |
| Ảnh cũ mất sau update | Release đã ghi upload bên trong thư mục bị thay; kiểm tra hai storage root tuyệt đối |
| API lưu trả 400 sau deploy | Đọc response/log có trace ID; xác nhận `/health/ready`, migration history và binary đúng release |
| `efbundle` báo kết nối sai | Kiểm tra quyền đọc `/etc/delong/delong.env`, connection string và password URL-encode trong `DATABASE_URL` |

## 13. Checklist mỗi lần release

- [ ] Commit/tag đã chốt và ghi lại.
- [ ] Test Release đạt.
- [ ] Artifact và checksum được tạo mới.
- [ ] Release mới giải nén vào thư mục mới, không ghi đè `current`.
- [ ] Backup database + `App_Data` + uploads hoàn tất.
- [ ] App đã dừng trước migration.
- [ ] `efbundle` thành công.
- [ ] Symlink `current` trỏ đúng release mới.
- [ ] Service `active`.
- [ ] `/health/live` đạt.
- [ ] `/health/ready` đạt, tức database không còn pending migration.
- [ ] Smoke script đạt.
- [ ] Kiểm tra Admin, lịch, booking và trang khách.
- [ ] Backup đã được sao chép ra ngoài VPS.
- [ ] Giữ ít nhất một release cũ để rollback.
