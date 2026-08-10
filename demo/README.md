# Demo HTML/localStorage

## Chạy local

```bash
python -m http.server 8080 -d demo
```

Mở `http://localhost:8080`.

## Lưu trữ

Key localStorage: `delong_homestay_demo_v1`.

Reset/export/import tại `Admin → Cấu hình`.

## Lưu ý

- Không dùng dữ liệu demo như database thật.
- Admin login chỉ là sessionStorage UX simulation.
- Khi production, business logic và auth chuyển server-side.
