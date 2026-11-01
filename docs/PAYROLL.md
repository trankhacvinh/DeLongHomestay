# Chấm công và tính lương

Trang `/Admin/Payroll` (menu Tài chính → Chấm công & Lương). Nhân viên **không** phải tài khoản đăng nhập; họ là bản ghi `employees` theo từng chi nhánh.

## Quy tắc tính (theo xác nhận của chủ cơ sở)

| Mục | Quy tắc |
|---|---|
| Lương ngày | Lương tháng ÷ 30 (cố định, cấu hình `salary_days_base`) |
| Lương ca | Lương ngày ÷ số ca/ngày của nhân viên (mặc định 4, 1–6) |
| Lương cơ bản | Lương ngày × số ngày thực trong tháng nằm trong thời gian làm việc. Tháng 31 ngày: 6.000.000 → 6.200.000; tháng 2 (28 ngày) → 5.600.000 |
| Nghỉ không lương (K) | Trừ đúng giá trị ca đó |
| Nghỉ có phép (P) | Không trừ tiền, nhưng mất chuyên cần |
| Nghỉ lễ (L) | Không trừ tiền, không mất chuyên cần |
| Làm bù (B) | Không trừ tiền, nhưng mất chuyên cần |
| Làm ngày lễ | Tổng ×2 (cộng thêm 1× giá ca); ngày Tết tổng ×3 |
| Chuyên cần | Mặc định 300.000, cấu hình được; chỉ khi cả tháng đi làm đủ mọi ca (cho phép L), nhân viên được bật cờ và làm trọn tháng |
| Tăng/giảm | Khoản tự do có loại + lý do bắt buộc; số âm là trừ |
| Ứng lương | Ghi khi tháng chưa chốt, trừ vào còn phải trả |
| Trả lương | Chỉ khi tháng đã chốt, không vượt số còn phải trả |

Lương đổi giữa tháng: mỗi ca dùng mức lương có hiệu lực ngày đó (`employee_salary_rates`). Mỗi dòng tiền làm tròn một lần tới đồng (`AwayFromZero`). Công thức thuần nằm trong `Features/Payroll/PayrollCalculator.cs`. Phần mềm không cảnh báo/chặn theo luật lao động (theo yêu cầu).

## Quyền

- Admin, Manager: xem/nhập bảng chấm công (`ManageAttendance`). Manager không nhận bất kỳ trường tiền nào từ API.
- Chỉ Admin: nhân viên + lương, tăng/giảm, cấu hình, ngày lễ, chốt/mở chốt, ứng/trả (`ManagePayroll`).
- Mọi route có `PropertyAccessFilter`, mutation có `ApiAntiforgeryFilter`.
- Audit loại `Employee`, `Attendance`, `PayrollAdjustment`, `PayrollPeriod`, `PayrollPolicy`, `PayrollHoliday`, `PayrollTransaction` bị ẩn khỏi API audit chung (`PayrollAuditTypes`).

## Chốt tháng và tiền

- Chốt chỉ được khi không còn ca trống. Chốt lưu snapshot các dòng lương (`payroll_periods.snapshot_json`); sau đó không sửa công, tăng/giảm, ngày lễ, lương trong tháng đó.
- Mở chốt cần lý do và chỉ khi chưa có khoản trả lương còn hiệu lực.
- Ứng/trả tạo `Expense` cùng transaction (danh mục "Ứng lương"/"Lương nhân viên", mô tả không có tên nhân viên, `source_type = 'Payroll'`, `source_id` = id giao dịch). Phiếu chi liên kết không hủy được từ màn Chi phí (`linked_source`); hủy từ trang lương sẽ hủy cả hai. Không xóa vật lý.
- Tạo giao dịch idempotent theo `request_key` + hash, khóa tháng bằng advisory lock.

## Schema

Migration `AddPayroll`: bảng `employees`, `employee_salary_rates`, `attendance_entries`, `payroll_adjustments`, `payroll_holidays`, `payroll_policies`, `payroll_periods`, `payroll_transactions`; thêm cột nullable `expenses.source_type`, `expenses.source_id` và index. Không backfill dữ liệu cũ.

## Giao diện

Kiểu Excel: thanh công thức, các sheet ở đáy (Chấm công, Bảng lương, Tăng/giảm, Ứng & trả, Nhân viên, Cấu hình). Chấm công bằng phím `x`/`K`/`P`/`L`/`B`, kéo chuột chọn vùng, Delete xoá, mũi tên/Enter/Tab di chuyển; tự lưu sau 0,7 giây.
