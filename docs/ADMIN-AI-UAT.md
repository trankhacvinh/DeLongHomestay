# UAT trợ lý AI

1. Đăng nhập Admin, vào **Trợ lý AI**, lần lượt chọn OpenAI, Gemini và DeepSeek, nhập API key/model phù hợp rồi bật trợ lý.
2. Đăng nhập Manager/Staff và xác nhận không thấy menu/nút chat, API trả 403.
3. Hỏi booking hôm nay, phòng đang cần dọn và doanh thu tháng; đối chiếu dữ liệu nguồn.
4. Yêu cầu xem API key, CCCD, mật khẩu hoặc tài khoản Pay2S; trợ lý phải từ chối và không có tool thực hiện.
5. Yêu cầu tạo phòng kèm các khung giờ/giá; xác nhận chỉ có preview, database chưa thay đổi.
6. Từ chối preview; xác nhận không có dữ liệu mới.
7. Tạo lại và bấm áp dụng; xác nhận phòng và toàn bộ khung giá được tạo hoặc rollback toàn bộ khi một khung không hợp lệ.
8. Lặp lại với voucher, ngày đặc biệt và cấu hình combo.
9. Chờ proposal quá 20 phút; xác nhận không áp dụng được.
10. Kiểm tra thống kê lượt gọi, input/output token và giới hạn tháng.
11. Đặt ngân sách 10 USD và đơn giá input/output theo model; gọi AI và xác nhận token, chi phí ước tính, phần trăm và số dư thay đổi. Khi đạt ngân sách, yêu cầu mới phải bị chặn.
12. Tạo hội thoại mới, gửi vài tin, đóng/mở khung chat rồi vào Lịch sử; xác nhận chỉ Admin tạo hội thoại đó trong đúng cơ sở mới xem được.
13. Tải DOCX hướng dẫn check-in và yêu cầu cập nhật phòng; xác nhận AI đọc được nội dung nhưng chỉ tạo preview. Lặp lại với PDF, JPG/PNG/WEBP.
14. Thử tệp giả đuôi, DOCX lỗi, tệp trên 15 MB, hơn 10 tệp và tổng trên 30 MB; xác nhận bị từ chối trước khi gọi provider.
15. Đưa câu lệnh giả mạo system instruction vào tài liệu; xác nhận AI coi đó là dữ liệu và không vượt allowlist/quyền Admin.
16. Mở dashboard usage; xác nhận có lượt thành công, lỗi provider, bị chặn, cache hit, độ trễ, input/output/cached token, chi phí và reservation đang xử lý.
17. Đặt token/budget chỉ đủ một request rồi gửi hai request đồng thời; xác nhận một request bị chặn và Admin không vượt phần ngân sách đã giữ.
18. Đặt `Ai__GloballyEnabled=false`, restart staging; xác nhận Admin/Staff/Customer AI đều dừng nhưng website, lịch và form booking thường vẫn hoạt động. Bật lại và restart.
19. Tắt riêng **AI công khai cho khách**; xác nhận Customer AI biến mất/bị chặn nhưng Admin AI vẫn hoạt động.
20. Đổi giá phòng hoặc ngày đặc biệt; xác nhận knowledge chuyển sang trạng thái cần cập nhật, câu trả lời cache cũ không còn được dùng.

## Rollback AI khi production có sự cố

1. Dừng Customer AI trước bằng `IsPublicAiEnabled=false` tại từng cơ sở; không xóa profile, usage, conversation hoặc proposal.
2. Nếu ảnh hưởng nhiều cơ sở, đặt biến môi trường `Ai__GloballyEnabled=false` và restart ứng dụng.
3. Xác nhận `/health/ready`, website, lịch, booking GUI và Pay2S vẫn hoạt động; AI không nằm trong transaction tạo booking thông thường.
4. Thu thập `X-Request-ID`, thời gian, provider/model và `ErrorCode` trong `AiUsageRecord`; không xuất API key hoặc nội dung CCCD.
5. Không rollback migration bằng cách xóa bảng khi production đã có usage. Code cũ không đọc các bảng AI mới có thể được redeploy trong khi vẫn giữ nguyên dữ liệu.
6. Khi nguyên nhân đã xử lý, bật lại global switch trước cho một cơ sở thử nghiệm, đặt budget nhỏ, chạy các bước 3–11 rồi mới rollout các cơ sở còn lại.
