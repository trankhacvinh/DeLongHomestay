# SePay VietQR + webhook

## Phạm vi

- Tích hợp webhook ngân hàng SePay (payload ID số) và QR VietQR. Không phải SePay Payment Gateway/IPN cổng thanh toán.
- Mỗi cơ sở có hồ sơ riêng. SePay được ưu tiên cho phiên mới khi `Enabled=true`; nếu tắt, hệ thống dùng cấu hình Pay2s hiện có. Muốn dừng toàn bộ thu tự động phải tắt cả hai.
- Phiên đang mở được dùng lại theo nhà cung cấp đã tạo; bật SePay không chuyển nhà cung cấp cho QR cũ.
- Giữ tên bảng/class `pay2_s_payment_intents`/`Pay2SService` và các route quản trị cũ để tương thích vòng đời hiện có. Cột `Provider` phân biệt `Pay2S`/`SePay`. Không đổi tên hoặc gán lại các Payment lịch sử.
- Migration `20260915091623_AddSePayProvider` thêm cấu hình, sổ giao dịch nhận được, snapshot xác thực/QR và index `(provider, transaction_id)`; mặc định các phiên cũ là Pay2S.

## Cấu hình

1. Áp dụng migration bằng quy trình release đang dùng sau backup. Sao lưu cả Data Protection key ring. Không cấu hình khóa thật trong Git.
2. Liên kết ngân hàng trên SePay. Xác định số tài khoản, VA/subAccount và chuỗi bắt buộc theo ngân hàng.
3. Vào **Cấu hình → Thanh toán → SePay** theo đúng cơ sở.
4. Nhập mã ngân hàng (ví dụ ACB/Vietcombank), số tài khoản mà webhook trả về, tài khoản/VA để tạo QR. Nếu không dùng VA, hai số này giống nhau. Nhập chính xác `subAccount` nếu webhook có giá trị; để trống nếu không có.
5. Nhập tiền tố nội dung theo yêu cầu ngân hàng: `SEVQR` cho VietinBank; `TKP` + mã VA cho VA theo nội dung. Mã phiên `DH` + 10 chữ số được nối phía sau. Không dùng mã booking để khớp lỏng.
6. Tạo một khóa webhook ngẫu nhiên riêng, ít nhất 32 ký tự; nhập vào DeLong và chọn xác thực **API Key** tại SePay. Khóa gửi trong `Authorization: Apikey <key>`.
7. Trên SePay, tạo webhook **tiền vào**, đúng tài khoản, tới `https://<domain>/api/payments/sepay/<property-id>/webhook`. Thiết lập mã thanh toán tiền tố `DH`, hậu tố **10 chữ số**.
8. Lưu thời gian giữ phòng (1–60 phút) và đệm xác nhận (0–15 phút), bật SePay. QR do nhân viên tạo giữ quy tắc không có đệm.
9. Thử trên môi trường staging/QA với tài khoản và khóa Test Mode riêng trước. Không đưa giao dịch Test Mode vào database thật.

QR dùng `https://vietqr.app/img` theo tài liệu hiện hành. Khách thấy số tiền và QR trong `/payment/sepay`. Nội dung QR gồm tiền tố ngân hàng + mã phiên; cần giữ nguyên toàn bộ nội dung khi chuyển.

## Nhận tiền

- Chỉ webhook/API nhà cung cấp đã xác thực được ghi sổ; truy cập trang thành công không tạo Payment.
- Kiểm tra chiều tiền vào, tài khoản/subAccount, mã phiên duy nhất và số tiền nguyên VND khớp hoàn toàn.
- Khóa transaction ở PostgreSQL theo ID giao dịch và khóa phiên/booking; UNIQUE ID ở sổ SePay chống gửi lại. Một ID chỉ được hạch toán một lần toàn hệ thống, kể cả nhiều webhook theo cơ sở.
- Đúng tiền, phiên còn hiệu lực: ghi `PaymentMethod.SePay`, xác nhận booking giữ phòng, redeem voucher và sử dụng thông báo/email hiện có.
- Tiền đến sau khi phiên đóng, booking hủy/kết thúc/no-show hoặc hết thời gian đệm: ghi Payment và trạng thái `PaidAfterExpiry`; không tự khôi phục phòng. Xử lý qua màn hình booking.
- Lệch số tiền, không khớp mã hoặc khách chuyển thêm giao dịch mới: lưu sổ SePay, không tạo Payment vào booking. Quản trị đối chiếu ngân hàng, ghi thu/hoàn tại booking khi phù hợp, lưu ghi chú đối soát. Hệ thống không cộng dồn tự động các lần chuyển thiếu.
- API trả `200 {success:true}` sau khi đã lưu kết quả. Lỗi xác thực trả 401, payload/tài khoản sai trả 400, lỗi database không được trả thành công.
- Phiên lưu snapshot khóa và tài khoản. Callback cũ vẫn xác thực được khi tắt/đổi cấu hình; khóa hiện hành cũng được chấp nhận cho callback của phiên cũ. Giữ key ring để giải mã snapshot.
- Giao dịch dùng chung ngân hàng nhưng mã thuộc cơ sở khác bị bỏ qua ở endpoint không sở hữu mã, không chiếm ID giao dịch.
- Thời điểm ghi Payment là lúc DeLong tiếp nhận/xác nhận; tiền về ngân hàng trước hạn nhưng callback sau thời gian giải phóng vẫn cần xử lý như tiền muộn.

## Đối soát thiếu webhook

- Có thao tác chủ động theo **ID giao dịch SePay v1 dạng số**, không có job polling tự động.
- Lưu API Token đối soát (khác khóa webhook) tại cấu hình SePay, rồi nhập ID cần truy vấn.
- Server gọi cố định `https://my.sepay.vn/userapi/transactions/details/{id}` với Bearer token; kiểm tra ID trả về và xử lý cùng quy tắc nhận tiền/chống trùng.
- Chức năng API đối soát này dùng API v1 môi trường thật. Không trộn UUID/API v2 hay token Test Mode vào luồng này.
- Sổ hiển thị 200 giao dịch gần nhất của cơ sở; ghi chú xử lý giữ người thực hiện/thời điểm. Ghi chú không tự ghi thu hoặc chuyển tiền hoàn qua ngân hàng.

## Chuyển đổi và khôi phục

- Giữ endpoint Pay2s, key ring và thông tin xác thực cũ trong giai đoạn chuyển tiếp để nhận khoản đến muộn.
- Nếu SePay có sự cố: tắt tạo phiên SePay mới, đối soát phiên hiện có; chỉ mở Pay2s mới khi đã kiểm tra cấu hình. Không xóa sổ giao dịch hoặc sửa Provider của phiên đã phát sinh.
- Sau khi có dữ liệu SePay, không chạy Down migration hoặc rollback binary cũ vốn không hiểu Provider mới. Ưu tiên sửa tiến hoặc tắt tạo phiên mới trong bản ứng dụng này; giữ schema và callback.

## Checklist nghiệm thu

- [ ] Migration nâng cấp database staging có lịch sử Pay2s; kiểm tra dữ liệu lịch sử giữ nguyên.
- [ ] API settings yêu cầu quyền theo cơ sở + antiforgery; khóa không được trả về JSON.
- [ ] QR khách và QR quản trị; hết hạn ẩn QR, tiếp tục đợi đệm, không chuyển tiền lần nữa.
- [ ] Webhook thành công/lặp/đồng thời, sai khóa, sai tài khoản/subAccount, tiền ra.
- [ ] Thiếu/thừa tiền, sai mã, chuyển thêm, nhiều cơ sở chung tài khoản.
- [ ] Hủy booking, tiền mặt trong khi QR mở, tiền đến muộn, voucher và thông báo.
- [ ] Đổi/tắt cấu hình vẫn xử lý phiên cũ; Pay2s cũ vẫn xác thực đúng provider.
- [ ] Thiếu webhook được đối soát bằng API, webhook đến sau không ghi trùng.
- [ ] SePay Test Mode qua HTTPS công khai, rồi giao dịch thật được chủ dự án cho phép.

## Tài liệu nhà cung cấp

- https://docs.sepay.vn/tich-hop-webhooks.html
- https://developer.sepay.vn/vi/tien-ich-khac/tao-qr-code
- https://docs.sepay.vn/api-giao-dich.html
- https://docs.sepay.vn/test-mode.html

## Kết quả kiểm tra local — 2026-09-15

- Build `tests/DeLong.Tests` thành công (0 lỗi); có cảnh báo NuGet audit do mạng và cảnh báo analyzer ở test cũ.
- Toàn bộ test project: **354 passed, 0 failed, 0 skipped**, với `DELONG_TEST_CONNECTION` trỏ database PostgreSQL QA riêng.
- Bao gồm chống trùng webhook đồng thời, tạo QR đồng thời, hoàn khoản muộn đồng thời, tiền mặt đóng QR, voucher, sai khóa/tài khoản/subAccount, lệch tiền, mã của cơ sở khác và đối soát API giả lập.
- JavaScript của 5 file thay đổi vượt qua `node --check`; `git diff --check` sạch.
- Trình duyệt chạy với database QA giao diện riêng: lưu cấu hình thành công; thiếu antiforgery trả **400**; cấu hình chưa đăng nhập và webhook thiếu khóa trả **401**; không có lỗi JavaScript; QR tải được; viewport mobile 390px không tràn ngang.
- Đây là kiểm tra mã nguồn, PostgreSQL và UI local. Chưa nghiệm thu webhook SePay Test Mode qua domain công khai, chưa gọi API đối soát thật hoặc chuyển khoản ngân hàng thật; chưa áp dụng migration/deploy vào database ứng dụng hay production.
