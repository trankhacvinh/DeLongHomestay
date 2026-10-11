# Kiểm tra dữ liệu và AI tạo booking

## Phạm vi và quyền

- `/Admin/DataQuality`, API `/api/admin/properties/{propertyId}/data-quality`: Admin/Manager và phân quyền chi nhánh. Mutation có antiforgery.
- Không dùng AI để quyết định dữ liệu đúng/sai. Các quy tắc xác định chạy từ booking/payment thật, trong một snapshot PostgreSQL nhất quán.
- Tự quét các chi nhánh đang hoạt động khi ứng dụng chạy và mỗi 30 phút; nút Kiểm tra lại quét ngay. Quét toàn bộ lịch sử của chi nhánh, chỉ lấy các trường đối soát cần thiết; không đọc CCCD/tài khoản ngân hàng/API key.
- Phát hiện: overlap của Held/Confirmed/CheckedIn/Completed (không Cancelled/NoShow), nghi trùng đơn cùng khách/phòng/thời gian, Completed còn số dư, Cancelled/NoShow còn thực thu, thời gian/tổng tiền/thực thu bất hợp lý, PaidAfterExpiry chưa có thời điểm giải quyết.
- Tiền thu là SUM Receipt trừ Refund, bỏ payment void. Đây là dấu hiệu cần kiểm tra: tiền mặt chưa nhập, phí hủy hợp lệ hoặc dữ liệu nhập cũ không mặc nhiên là sai. Không cộng số dư đơn hủy vào công nợ phải thu.

## Danh sách xử lý

- Bảng `data_quality_issues`, UUID/property_id, fingerprint duy nhất theo chi nhánh, bằng chứng và hash. Lịch sử phát hiện/ghi nhận lưu qua AuditService hiện có.
- Một phát hiện không tạo lại ở mỗi lần quét. Khi số liệu thay đổi hoặc vấn đề tái xuất hiện: mở lại và giữ ghi nhận trước trong audit. Khi hết điều kiện: gắn Không còn phát hiện, giữ lịch sử, không tự xác nhận đã đối soát.
- Trạng thái: Cần xử lý / Đang xử lý / Đã xử lý / Đã kiểm tra hợp lệ. Ghi nhận phải có ghi chú 3–1.000 ký tự. Đã xử lý chỉ được lưu sau lần quét không còn phát hiện; vẫn có dấu hiệu nhưng đã xác minh hợp lệ thì dùng Đã kiểm tra hợp lệ kèm lý do.
- Thông báo chuông cho Admin/Manager: chỉ khi có phát hiện mới hoặc bằng chứng thay đổi; cập nhật realtime sau commit. Không gửi email/Telegram/khách. Worker và thao tác tay dùng khóa PostgreSQL theo chi nhánh và retry khi snapshot cạnh tranh.
- Phân trang 100 mục, lọc/tìm trên toàn bộ dữ liệu phía máy chủ; tổng số và số chờ xử lý lấy từ toàn bộ dữ liệu. Danh sách không đồng nghĩa toàn bộ dữ liệu đã chính xác: các sai sót ngoài quy tắc vẫn cần kiểm tra nghiệp vụ.
- Nút xem đơn mở màn hình booking hiện có để thu bổ sung, xử lý thanh toán muộn hoặc cập nhật theo quyền. Chức năng kiểm tra không sửa/hủy/xóa booking/payment hay tự hoàn tiền.

## AI

- Nhờ AI phân tích mở ngăn AI quản trị và điền yêu cầu; người dùng gửi để gọi provider đã cấu hình. Snapshot AI có tối đa 50 mục của đúng chi nhánh, ưu tiên mục được yêu cầu (kể cả đã hết phát hiện); ngoài snapshot AI phải yêu cầu kiểm tra lại thay vì đoán.
- AI quản trị (quyền UseAdminAi hiện tại: Admin) hỗ trợ `CreateBooking`. Cần phòng, tên, điện thoại, ngày giờ có offset; thiếu/mơ hồ phải hỏi lại. Server xác định phòng/khung thật, kiểm tra khóa, overlap, ngày giờ và chuẩn bị bản xem trước.
- Khung lẻ/qua đêm: giá hiện hành qua PricingService, gồm cuối tuần/ngày đặc biệt/phụ thu. Khung còn một phần giữ nguyên giá khung, không chia theo giờ. Giá tiền phòng chỉ ghi đè khi người dùng cung cấp; phải duyệt số tiền và phụ thu trong preview.
- Giờ tùy chỉnh/nhiều khung: cần giá thỏa thuận rõ ràng, bỏ rateReference. Không tự đoán giá combo/multi-day; không tự tạo receipt hoặc QR.
- Duyệt mới tạo Confirmed qua BookingService hiện có. Kiểm tra lại dưới khóa dòng phòng, giữ nguyên check-in/out thực tế. Proposal hết hạn 20 phút, chỉ áp dụng một lần; không cho CreateBooking trong Batch. Sau thành công hiển thị mã và link đơn.
- Không cho AI gửi payload preparedBooking tự chế. Không bổ sung quyền sửa/hủy booking hoặc thu/hoàn tiền tự động. Staff/customer AI giữ nguyên phạm vi hiện có.

## Release

- Migration `20261011072819_AddDataQualityIssues` chỉ thêm bảng và index/FK chi nhánh; không backfill/chỉnh booking, payment hoặc dữ liệu production cũ.
- Phải chạy migration bằng bundle mới (hoặc EF trong môi trường được quản lý) trước khi start code mới. Database:AutoMigrate=false vẫn giữ nguyên.
- Đã rehearsal trên PostgreSQL thử nghiệm: schema mới, quét đồng thời, dedup/reopen/clear, audit, phân quyền chi nhánh, antiforgery, AI preview/apply và giữ giá qua đêm một phần.
- Chưa triển khai production; chưa xác minh browser/mobile thực tế hoặc gọi provider thật. Integration AI dùng HTTP provider giả, không gửi dữ liệu ra ngoài.
