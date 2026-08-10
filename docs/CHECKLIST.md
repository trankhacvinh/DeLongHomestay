# Checklist nghiệm thu

## Foundation

- [x] `dotnet restore` pass trên GitHub Actions.
- [x] `dotnet build -c Release` pass trên GitHub Actions.
- [x] `dotnet test -c Release` pass trên GitHub Actions.
- [ ] PostgreSQL local có database `delong_dev`.
- [ ] Connection string thật nằm trong User Secrets, không nằm trong Git.
- [ ] Initial migration tạo/apply thành công.
- [ ] Seed tạo De Long + 6 phòng đúng rates.
- [ ] Seed admin từ User Secrets.
- [ ] Login/logout Identity hoạt động với PostgreSQL local.
- [x] Rooms API kiểm tra role + `UserPropertyAccess`.

## Vue/API mẫu Rooms

- [ ] Trang `/Admin/Rooms` render initial data từ PostgreSQL local.
- [ ] Vue mount trong page scope, không SPA.
- [ ] Search/filter không reload.
- [ ] Thêm phòng mở modal và POST API.
- [ ] Sửa phòng mở modal và PUT API.
- [ ] Ngừng phòng mở confirm modal và DELETE API.
- [x] Mutation API được cấu hình antiforgery token.
- [x] API validation dùng ProblemDetails.
- [ ] Kiểm tra trực tiếp loading state và toast trên browser.

## Trước Booking milestone

- [ ] Thay property seed ID trong page mẫu bằng CurrentProperty resolver/selector.
- [ ] Thêm Customer.
- [ ] Thêm Booking/Payment/Audit schema.
- [ ] Thiết kế PostgreSQL overlap guard.
- [ ] Có integration test database riêng `delong_test`.
