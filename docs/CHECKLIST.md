# Checklist nghiệm thu

## Foundation

- [ ] `dotnet restore` pass.
- [ ] `dotnet build -c Release` pass.
- [ ] `dotnet test -c Release` pass.
- [ ] PostgreSQL local có database `delong_dev`.
- [ ] Connection string thật nằm trong User Secrets, không nằm trong Git.
- [ ] Initial migration tạo/apply thành công.
- [ ] Seed tạo De Long + 6 phòng đúng rates.
- [ ] Seed admin từ User Secrets.
- [ ] Login/logout Identity hoạt động.
- [ ] User không có quyền bị Access Denied/403 đúng.

## Vue/API mẫu Rooms

- [ ] Trang `/Admin/Rooms` render initial data từ Razor.
- [ ] Vue mount trong page scope, không SPA.
- [ ] Search/filter không reload.
- [ ] Thêm phòng mở modal và POST API.
- [ ] Sửa phòng mở modal và PUT API.
- [ ] Ngừng phòng mở confirm modal và DELETE API.
- [ ] Mutation gửi antiforgery token.
- [ ] API validation trả ProblemDetails dễ hiểu.
- [ ] Button có loading state và toast.

## Trước Booking milestone

- [ ] Chốt CurrentProperty resolver thay cho property seed ID trong page mẫu.
- [ ] Thêm Customer.
- [ ] Thêm Booking/Payment/Audit schema.
- [ ] Thiết kế PostgreSQL overlap guard.
- [ ] Có integration test database riêng `delong_test`.
