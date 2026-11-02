# Hủy phòng & hoàn tiền: UAT

## Hủy booking
1. Mở một booking đã xác nhận (Lịch phòng, Lịch V2 hoặc Đặt phòng) rồi bấm Hủy.
2. Hộp thoại bắt buộc nhập **lý do hủy**. Để trống thì không hủy được.
3. Nếu khách đã thanh toán, phải chọn một trong ba cách: **Hoàn tiền ngay** (ghi luôn khoản hoàn), **Hoàn sau** (vào danh sách Chờ hoàn), **Không hoàn (giữ cọc)**.
4. Nếu khách chưa thanh toán thì không hỏi tiền.
5. Booking tự hủy do quá hạn thanh toán **không** vào danh sách này.

## Danh sách Hủy & hoàn tiền (menu Vận hành)
- Cột: khách, thời gian hủy, nhân viên hủy và lý do, tiền booking, đã thanh toán, trạng thái hoàn, hoàn lúc và nhập lúc.
- Lọc theo khoảng ngày (ngày hủy, ngày nhận phòng hoặc ngày hoàn tiền), trạng thái hoàn, nhân viên hủy, chi nhánh. Tìm theo mã booking, tên, SĐT, phòng.
- Ô tổng theo bộ lọc: số booking, tổng giá trị, đã thanh toán, đã hoàn, **còn phải hoàn**, giữ lại.
- Nút Hoàn tiền trên từng dòng: sửa số tiền cần hoàn, ghi khoản hoàn mới, sửa số tiền hoặc thời điểm của khoản đã nhập, hủy khoản nhập nhầm.
- Xuất Excel theo bộ lọc.
- Người dùng chỉ thấy chi nhánh mình được phép truy cập. Chọn "Tất cả chi nhánh" chỉ xuất hiện khi có quyền từ hai chi nhánh trở lên. Staff được ghi nhận hoàn tiền như khi ghi thu tiền.

## Thông báo
- Email cho danh sách quản lý khi hủy: bật ở Cài đặt, mục thông báo (cần bật email booking và có người nhận).
- Telegram khi hủy có thêm người hủy, đã thanh toán, cách xử lý tiền, lý do. Sự kiện Telegram mới "Hoàn tiền" khi có khoản hoàn: cần tick trong Cài đặt nếu đã từng tùy chỉnh danh sách sự kiện.

## Thống kê
- Thu chi và Báo cáo vẫn tính theo ngày phát sinh giao dịch (thu trừ hoàn).
- Thêm: **Còn phải hoàn cho khách** (booking hủy chưa hoàn xong, tính đến hiện tại) và **Giữ lại từ booking hủy** (trong kỳ, đã nằm trong thực thu).
- Cảnh báo "đơn hủy còn tiền đã thu" chỉ còn khi chưa hoàn và chưa ghi nhận giữ cọc.

## Dữ liệu cũ
Lần đầu mở danh sách của một chi nhánh, hệ thống dựng hồ sơ cho các booking đã hủy thủ công trước đây (lấy người hủy và giờ hủy từ lịch sử thao tác). Hồ sơ cũ không có lý do, và nếu khách đã trả tiền mà chưa hoàn thì hiện là Chờ hoàn để quản lý quyết định hoàn hoặc đổi sang giữ cọc.
