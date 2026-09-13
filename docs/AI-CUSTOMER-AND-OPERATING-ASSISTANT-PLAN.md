# Kế hoạch hoàn thiện AI Customer Chat và AI Operating Assistant

> Trạng thái: **Bản kế hoạch để review — chưa triển khai**  
> Phạm vi: Customer AI, Staff AI, Owner/Admin AI, báo cáo động, booking draft, cache bền vững, ngân sách và chống lạm dụng.  
> Kiến trúc nền: .NET 10, Razor Pages, Vue 3 progressive enhancement, Minimal APIs, EF Core, PostgreSQL và FusionCache.

## 1. Mục tiêu

- Khách có thể hỏi về phòng, giá, phòng trống, tiện nghi, chính sách, địa chỉ và booking của chính mình.
- Khách có thể đi từ hội thoại đến một bản nháp đặt phòng, sau đó xác nhận qua quy trình booking/Pay2S hiện có.
- Staff có thể hỏi nhanh về booking, check-in/out, availability, dọn phòng và công việc trong ca theo đúng quyền.
- Owner/Admin có thể hỏi doanh thu thực thu, công suất, KPI và phân tích hoạt động mà không phải mở từng màn hình báo cáo.
- Admin có thể yêu cầu AI đề xuất cấu hình; mọi thay đổi tiếp tục phải có preview, xác nhận, kiểm tra stale state, transaction và audit.
- Giảm tối đa số lần gọi AI, input token và output token bằng typed tools, cache bền vững, cache RAM, hội thoại rút gọn và câu trả lời mẫu.
- Không cho AI truy cập trực tiếp database, tự sinh SQL, đọc secrets hoặc tự ý thay đổi nghiệp vụ.

## 2. Quyết định nghiệp vụ đã chốt

- [x] Công suất được tính theo **ca/khung có thể bán**, không đơn thuần theo số phòng/ngày.
- [x] Nguồn sự thật là `BookingRateSegment` và khoảng `checkIn/checkOut` thực tế; không hard-code luôn có bốn ca.
- [x] Công suất loại trừ booking `Cancelled`, `Rejected`, `NoShow` và các ca không thể bán do khóa/bảo trì.
- [x] Doanh thu trong AI là **tiền thực thu**.
- [x] Báo cáo phải tách rõ:
  - Tổng tiền đã thu (`Receipt`).
  - Tổng tiền đã hoàn (`Refund`).
  - Thực thu ròng = tiền thu - tiền hoàn.
  - Chi phí là chỉ số riêng, không tự trừ vào doanh thu nếu người dùng không hỏi dòng tiền/lợi nhuận.
- [x] Khách xem booking bằng một trong hai cách:
  - Đăng nhập tài khoản khách.
  - Nhập đúng mã booking + số điện thoại đã chuẩn hóa.
- [x] Customer AI không đổi hoặc hủy booking.
- [x] Khi khách hỏi đổi/hủy, AI hiển thị số điện thoại/email công khai của cơ sở; nếu thiếu thì yêu cầu khách liên hệ trực tiếp cơ sở.
- [x] Staff được xem dữ liệu tài chính theo permission riêng, không theo role chung một cách mặc định.
- [x] Customer AI dùng chung ngân sách AI của cơ sở nhưng có hạn mức con, chống spam và công tắc bật/tắt riêng.
- [x] Không làm RAG/vector database trong các giai đoạn đầu; ưu tiên dữ liệu có cấu trúc và typed tools.

## 3. Baseline hiện có cần giữ nguyên

- [x] Admin AI chỉ dành cho Admin, giới hạn theo cơ sở, có antiforgery và rate limit.
- [x] API key OpenAI/Gemini/DeepSeek được mã hóa và không trả ngược ra trình duyệt.
- [x] Lịch sử hội thoại Admin được lưu theo cơ sở và người dùng.
- [x] Admin AI hỗ trợ DOCX, PDF và ảnh trong phạm vi hiện tại.
- [x] Có thống kê input/output token, chi phí ước tính và ngân sách tháng.
- [x] Mutation dùng `AiChangeProposal`, preview trước/sau, xác nhận rõ ràng, stale-state check, transaction, rollback và audit.
- [x] Pricing server-side là nguồn tính giá; client và AI không được tự quyết định tổng tiền.
- [x] Conflict phòng được quyết định server-side/PostgreSQL trước khi tạo Pay2S intent.
- [x] Payment là entity riêng; booking không lưu payment JSON.
- [x] Danh mục tiện nghi theo phòng đã có trong Room Content.
- [x] FusionCache đã có cho một số dữ liệu public.

## 4. Kiến trúc mục tiêu

```text
Customer / Staff / Admin chat
              │
              ▼
        AI Access Gateway
  xác thực · role · permission · quota
              │
              ▼
      Intent and Tool Router
  deterministic trước, model sau
              │
     ┌────────┼─────────┐
     ▼        ▼         ▼
Persistent  Typed     LLM Provider
cache       tools     OpenAI/Gemini/DeepSeek
PostgreSQL  services  chỉ khi cần
     │        │         │
     └────────┴─────────┘
              │
              ▼
     Answer / Booking draft /
     Admin change proposal
```

### Nguyên tắc

1. AI không có `DbContext`, connection string hoặc quyền gọi SQL.
2. AI chỉ chọn công cụ từ allowlist; server validate toàn bộ tham số.
3. Câu hỏi có thể trả lời bằng code/template thì không gọi model.
4. Tool chỉ trả đúng trường cần thiết, không gửi toàn bộ bảng dữ liệu.
5. Customer, Staff và Admin có tool catalog khác nhau.
6. Dữ liệu khách cá nhân không được đưa vào cache dùng chung.
7. Mọi mutation quan trọng đều dùng preview và xác nhận; Customer AI không có mutation tool.

## 5. Phân quyền AI

### Customer AI

Được phép:

- Xem thông tin công khai của cơ sở.
- Tìm, lọc và so sánh phòng.
- Xem tiện nghi, sức chứa và mô tả phòng.
- Kiểm tra giá và availability thời gian thực.
- Xem chính sách, phụ thu, giờ check-in/out và hướng dẫn đặt phòng công khai.
- Tạo `BookingDraft` rồi chuyển sang form đặt phòng chính thức.
- Tra cứu booking của chính khách sau khi xác thực.

Không được phép:

- Xem booking, số điện thoại hoặc thông tin khách khác.
- Xem doanh thu, chi phí, công suất và báo cáo nội bộ.
- Xem CCCD hoặc tài liệu định danh.
- Nhận secret/mã cửa trước khi booking đủ điều kiện.
- Đổi, hủy hoặc xác nhận booking.
- Thay đổi giá, voucher, nội dung hoặc cấu hình.

### Staff AI

Permission đề xuất:

- `UseStaffAi`
- `AiViewAvailability`
- `AiViewBookings`
- `AiViewCustomerContact`
- `AiViewHousekeeping`
- `AiViewPayments`
- `AiViewRevenue`
- `AiCreateOperationalProposal`

Staff chỉ nhìn thấy tool tương ứng permission. Không suy luận quyền chỉ từ việc giao diện có/không có nút.

### Owner/Admin AI

- Được dùng toàn bộ read-only reporting tools.
- Được tạo proposal cấu hình trong allowlist.
- Không được đọc API key, SMTP password, Pay2S secrets, Telegram bot token, mật khẩu hoặc CCCD.
- Không hard-delete booking/payment/customer/user.
- Các thao tác rủi ro cao tiếp tục thực hiện bằng màn hình nghiệp vụ, không qua AI.

## 6. Định nghĩa số liệu

### Công suất theo ca

```text
occupancyPercent = bookedSellableSegments / totalSellableSegments × 100
```

- Tử số: các segment thuộc booking đang hoạt động trong khoảng báo cáo.
- Mẫu số: các khung có thể bán của các phòng hoạt động, trừ thời gian bị khóa/bảo trì.
- Booking nhiều khung tính theo số segment.
- Booking cả ngày tính theo toàn bộ segment cấu thành.
- Qua đêm là một segment theo cấu hình thực tế.
- Báo cáo phải ghi rõ khoảng ngày và timezone cơ sở.

Thêm chỉ số riêng:

- Phòng đang có khách tại thời điểm hiện tại.
- Phòng còn trống tại thời điểm hiện tại.
- Ca đã bán/còn bán trong ngày.
- Công suất theo phòng, theo khung và theo ngày trong tuần.

### Doanh thu thực thu

```text
grossReceipts = SUM(Payment.Amount WHERE Type = Receipt AND !IsVoided)
refunds       = SUM(Payment.Amount WHERE Type = Refund  AND !IsVoided)
netReceipts   = grossReceipts - refunds
```

- Dùng `Payment.OccurredAtUtc` quy đổi theo timezone cơ sở.
- Không dùng tổng giá booking làm doanh thu thực thu.
- Không tính payment bị void.
- Chi phí được báo cáo độc lập.
- Nếu người dùng hỏi “dòng tiền ròng”, tính `netReceipts - expenses` và ghi rõ công thức.

## 7. Dữ liệu tiện nghi và chính sách

### Tiện nghi

- Tận dụng danh mục `Amenity` và liên kết tiện nghi theo phòng hiện có.
- Bổ sung nhóm/metadata nếu cần: phòng tắm, giải trí, bếp, tiện ích, đỗ xe, accessibility.
- Bổ sung các thuộc tính so sánh có cấu trúc thay vì chỉ viết trong HTML:
  - Mức riêng tư.
  - Phù hợp couple.
  - Phù hợp gia đình/nhóm.
  - Có view.
  - Diện tích phòng.
  - Có bồn tắm, máy chiếu, Netflix, tủ lạnh, máy sấy tóc.
- Mọi nhận định “đẹp nhất”, “riêng tư nhất”, “cao cấp nhất” phải dựa trên tag/thuộc tính đã cấu hình và nói rõ tiêu chí.

### Chính sách cơ sở

Tạo cấu hình có cấu trúc cho:

- Giờ check-in/check-out mặc định.
- Check-in sớm/check-out trễ và phụ thu.
- Khách đến muộn/sau nửa đêm.
- Self check-in.
- Thú cưng, hút thuốc, tiệc, nấu ăn, đồ ăn ngoài.
- Giới hạn số khách và phụ thu thêm người.
- Chính sách trẻ em.
- CCCD.
- Hủy/hoàn tiền.
- Mất chìa khóa/thẻ, hư hỏng tài sản.
- Đỗ xe máy/ô tô.
- Trang trí sinh nhật và dịch vụ bổ sung.

Nội dung HTML công khai vẫn được giữ để hiển thị đẹp; AI ưu tiên trường cấu trúc ngắn để chính xác và tiết kiệm token.

## 8. Dữ liệu mới dự kiến

### `PropertyAiPublicSettings`

- `PropertyId`
- `IsEnabled`
- `MaxRequestsPerMinutePerIp`
- `MaxRequestsPerDayPerIp`
- `MaxTurnsPerConversation`
- `MaxMessageCharacters`
- `DailyBudgetUsd`
- `MonthlyBudgetUsd`
- `ReservedAdminBudgetPercent`
- `RequireCaptchaAfterRequests`
- `CooldownSeconds`
- `AnonymousConversationTtlMinutes`

### `PropertyAiKnowledgeSnapshot`

- `PropertyId`
- `Version`
- `ContentJson`
- `ContentHash`
- `IsDirty`
- `UpdatedAtUtc`

Chứa thông tin public đã rút gọn: cơ sở, phòng, tiện nghi, chính sách, liên hệ và metadata website. Không chứa booking, payment, CCCD hoặc secrets.

### `AiResponseCache`

- `PropertyId`
- `Audience`
- `Intent`
- `NormalizedParametersHash`
- `DataVersion`
- `ResponseJson`
- `ExpiresAtUtc`
- `HitCount`
- `LastHitAtUtc`

Cache kết quả dữ liệu/tool, không nhất thiết cache nguyên văn câu trả lời.

### `AiConversationSummary`

- `ConversationId`
- `SummaryJson`
- `LastSummarizedMessageId`
- `UpdatedAtUtc`

Lưu intent hiện tại, ngày, số khách, ngân sách, tiện nghi và các phòng đã đề xuất. Không gửi lại toàn bộ lịch sử cho mỗi request.

### `AiToolExecutionLog`

- `PropertyId`, `ConversationId`, `UserId`
- `Audience`, `ToolName`
- `ParametersHash`
- `CacheStatus`
- `DurationMs`, `IsSuccess`, `ErrorCode`
- `CreatedAtUtc`

Không lưu raw secrets hoặc dữ liệu nhạy cảm trong log.

### `AiBookingDraft`

- `PropertyId`, `ConversationId`
- `RoomId`
- `SelectedRateSegmentsJson`
- `CheckInUtc`, `CheckOutUtc`
- `GuestCount`
- `VoucherCode`
- `QuotedAmount`, `QuoteVersion`
- `ExpiresAtUtc`
- `Status`

Draft không khóa phòng. Giá và availability phải được tính lại khi khách gửi form chính thức.

### Mở rộng `AiUsageRecord`

- `Audience`: Customer/Staff/Admin.
- `CachedInputTokens`.
- `ProviderRequestId` nếu an toàn.
- `WasApplicationCacheHit`.
- `EstimatedCostBeforeCacheUsd`.
- `EstimatedCostUsd`.
- `BudgetBucket`: Public/Internal.

## 9. Tool catalog dự kiến

### Public tools

- `get_property_contact`
- `get_property_policies`
- `search_rooms`
- `compare_rooms`
- `get_room_details`
- `get_room_rates`
- `check_availability`
- `quote_booking`
- `create_booking_draft`
- `lookup_own_booking`
- `get_change_cancel_contact`

### Staff tools

- `get_current_room_status`
- `get_today_bookings`
- `get_checkins`
- `get_checkouts`
- `get_next_booking_for_room`
- `get_unpaid_bookings`
- `get_unconfirmed_bookings`
- `get_housekeeping_workload`
- `get_priority_turnovers`
- `find_booking`
- `get_payment_status` — yêu cầu permission.

### Owner/Admin reporting tools

- `get_occupancy_summary`
- `get_revenue_summary`
- `compare_periods`
- `get_revenue_by_room`
- `get_revenue_by_source`
- `get_booking_performance_by_room`
- `get_demand_by_rate_segment`
- `get_booking_lead_time`
- `get_average_booking_duration`
- `get_cancellation_rate`
- `get_repeat_customer_rate`
- `get_guest_count_distribution`
- `get_expense_summary`
- `get_cashflow_summary`

### Admin proposal tools

- Giữ các proposal hiện có.
- Mở rộng theo allowlist sau khi từng service có validation và audit đầy đủ.
- Không cấp tool sửa payment account, email credentials, user credentials, CCCD hoặc hard-delete dữ liệu.

## 10. Chiến lược tiết kiệm token và chi phí

### Thứ tự xử lý

1. Kiểm tra command/câu hỏi nhanh deterministic.
2. Chuẩn hóa intent và tham số.
3. Kiểm tra persistent cache PostgreSQL.
4. Kiểm tra/làm nóng FusionCache.
5. Gọi typed tool nếu cache miss.
6. Dùng template trả lời nếu đủ rõ.
7. Chỉ gọi model khi cần hiểu ngôn ngữ tự do, so sánh hoặc phân tích.

### Cache key

Không dùng nguyên câu hỏi. Dùng:

```text
property + audience + intent + normalized parameters + data version
```

Ví dụ ba câu khác chữ nhưng cùng ý sẽ có cùng khóa:

```json
{
  "intent": "search_available_rooms",
  "date": "2026-09-10",
  "period": "evening",
  "amenities": ["bathtub"],
  "maxPrice": 400000
}
```

### TTL đề xuất

| Dữ liệu | Persistent cache | RAM cache | Invalidation |
|---|---:|---:|---|
| Địa chỉ/liên hệ | 24 giờ | 6 giờ | Khi sửa Site Settings |
| Tiện nghi/mô tả phòng | 24 giờ | 6 giờ | Khi sửa Room Content |
| Chính sách | 24 giờ | 6 giờ | Khi sửa policy |
| Giá | 15 phút | 5 phút | Khi sửa rate/pricing/special day |
| Availability | 30 giây | 15 giây | Khi booking/hold/cancel/move thay đổi |
| Báo cáo hôm nay | 3 phút | 1 phút | Theo TTL và mutation liên quan |
| Báo cáo tháng | 15 phút | 5 phút | Khi payment/refund/expense thay đổi |
| So sánh phòng tĩnh | 24 giờ | 6 giờ | Khi phòng/tiện nghi/giá thay đổi |

Không cache dùng chung booking cá nhân hoặc thông tin khách. Có thể cache ngắn theo đúng user/session đã xác thực.

### Rút gọn hội thoại

- Chỉ gửi 4–6 tin nhắn gần nhất.
- Gửi `AiConversationSummary` thay cho toàn bộ lịch sử.
- Không gửi lại file ở mọi lượt; chỉ dùng phần trích liên quan.
- Giới hạn tool result theo số dòng/trường.
- Câu trả lời public mặc định ngắn, có nút “Xem chi tiết”.

### Provider prompt cache

- Đặt system prompt và tool schema ổn định ở đầu request.
- Dùng cache key ổn định theo cơ sở/audience/model nếu provider hỗ trợ.
- Ghi nhận cached input tokens riêng để tính chi phí sát hơn.
- Provider cache chỉ là tối ưu bổ sung; persistent cache của ứng dụng mới là lớp sống qua restart.

### Model routing

- Model nhỏ/rẻ: FAQ, intent, phòng, giá, availability và báo cáo ngắn.
- Model trung bình/tốt hơn: phân tích nguyên nhân, chiến lược và file dài; chỉ Owner/Admin.
- Không cho Customer tự chọn model.

## 11. Ngân sách và chống spam

- Một ngân sách tổng theo cơ sở.
- Một bucket con cho Customer AI.
- Dự phòng một tỷ lệ cho Admin, mặc định đề xuất 30%.
- Kiểm tra ngân sách trước khi gọi provider.
- Tạm giữ chi phí tối đa ước tính của request để hạn chế vượt trần do nhiều request đồng thời.
- Sau response, đối soát bằng token provider trả về.
- Customer AI tự tắt khi hết bucket public; Admin còn dùng được phần dự phòng.
- Cảnh báo ở 70%, 85%, 95% và 100%.
- Rate limit mặc định đề xuất:
  - 5 request/phút/IP.
  - 30 request/ngày/IP.
  - 10–15 lượt/phiên anonymous.
  - Cooldown 2 giây.
  - Tối đa 500 ký tự/tin public.
  - CAPTCHA sau ngưỡng hoặc khi có hành vi bất thường.
- Hash IP có salt xoay vòng; không lưu IP thô lâu dài nếu không cần.
- Chặn câu hỏi lặp vòng và nhiều phiên đồng thời bất thường.

## 12. Các giai đoạn triển khai

## Giai đoạn 0 — ADR, audit dữ liệu và chuẩn hóa định nghĩa

### Checklist

- [x] Viết ADR cho AI Gateway, typed tools và persistent cache tại `docs/ADR-AI-OPERATING-ASSISTANT.md`.
- [ ] Chốt công thức công suất theo segment bằng test case thực tế.
- [ ] Chốt các trạng thái booking được tính/loại.
- [ ] Chốt doanh thu thực thu, hoàn tiền, chi phí và dòng tiền.
- [ ] Audit dữ liệu tiện nghi hiện có của từng phòng.
- [ ] Audit nội dung chính sách, liên hệ, phụ thu và check-in/out.
- [ ] Lập danh sách permission Staff hiện có và permission AI mới.
- [ ] Xác định các service hiện tại có thể tái sử dụng.
- [ ] Cập nhật architecture/data model/roadmap trước migration.

### Nghiệm thu

- [ ] Có bảng ví dụ tính công suất và doanh thu được chủ hệ thống duyệt.
- [ ] Không còn thuật ngữ báo cáo mơ hồ.
- [ ] Có ma trận role/permission/tool hoàn chỉnh.

## Giai đoạn 1 — Knowledge data và policy management

### Checklist

- [ ] Chuẩn hóa danh mục tiện nghi theo phòng.
- [ ] Bổ sung thuộc tính so sánh: diện tích, mức riêng tư, nhóm phù hợp và view.
- [ ] Tạo cấu hình chính sách có cấu trúc theo cơ sở.
- [ ] Làm màn hình Admin dễ nhập/sửa chính sách.
- [x] Tạo `PropertyAiKnowledgeSnapshot` và thao tác rebuild.
- [x] Đánh dấu snapshot dirty khi dữ liệu trực tiếp theo cơ sở thay đổi.
- [x] Không đưa secrets/booking/customer/payment vào public snapshot.
- [x] Có preview snapshot cho Admin kiểm tra.

### Nghiệm thu

- [ ] AI có đủ dữ liệu để trả lời danh sách tiện nghi/chính sách đã thống nhất.
- [ ] Snapshot tồn tại sau restart.
- [ ] Sửa dữ liệu làm tăng version và cache cũ không còn được dùng.

## Giai đoạn 2 — AI Gateway, persistent cache và cost guard

### Checklist

- [x] Tạo `AiAudience`: Customer, Staff, Admin.
- [x] Tạo nền AI Access Gateway kiểm tra property, trạng thái provider và quota theo audience.
- [x] Tạo tool registry và allowlist theo audience/permission.
- [ ] Validate tham số tool server-side.
- [x] Tạo `AiResponseCache`, `AiConversationSummary` và `AiToolExecutionLog`.
- [ ] Tích hợp persistent PostgreSQL cache + FusionCache.
- [x] Thêm cache invalidation PostgreSQL theo tag/data version.
- [x] Mở rộng usage record cho cached token và audience.
- [x] Thêm bucket Customer AI và phần ngân sách dự phòng Admin.
- [x] Thêm công tắc bật/tắt Customer AI.
- [x] Thêm dashboard cache hit, token và chi phí theo audience.
- [ ] Giữ prefix prompt ổn định để tận dụng provider prompt caching.

### Nghiệm thu

- [ ] Restart ứng dụng không làm mất persistent cache/snapshot.
- [ ] Cùng intent/tham số nhưng khác câu chữ dùng lại cùng dữ liệu cache.
- [ ] Dữ liệu thay đổi làm cache hết hiệu lực.
- [ ] Không request nào vượt permission hoặc truy cập chéo cơ sở.

## Giai đoạn 3 — Customer AI read-only

### Checklist

- [x] Thêm nút chat public responsive desktop/mobile.
- [ ] Hỗ trợ tiếng Việt tự nhiên và các câu hỏi nhanh.
- [x] Trả lời từ snapshot thông tin cơ sở, liên hệ và địa chỉ.
- [x] Tìm/lọc/so sánh phòng từ snapshot.
- [x] Trả lời tiện nghi và sức chứa từ snapshot.
- [x] Tool giá thời gian thực qua PricingService.
- [x] Tool availability thời gian thực.
- [ ] Trả lời subjective dựa trên tiêu chí cấu hình, không khẳng định vô căn cứ.
- [x] Hiển thị link phòng và nút chọn phòng; mã phòng do AI đề xuất luôn được server xác thực theo cơ sở và trạng thái public trước khi sinh URL.
- [ ] Bổ sung CAPTCHA; rate limit, quota ngày và public budget guard đã có.
- [x] Từ chối câu hỏi ngoài phạm vi trong một câu ngắn.
- [x] Không gửi dữ liệu nội bộ vào prompt Customer AI.

### Nghiệm thu

- [ ] Trả đúng phòng, giá và availability với dữ liệu thực tế.
- [ ] Giá cuối tuần/ngày đặc biệt/phụ thu đúng PricingService.
- [ ] Không lộ doanh thu, booking khác hoặc dữ liệu khách.
- [ ] Mobile chat không che nội dung và đóng/mở ổn định.

## Giai đoạn 4 — Customer booking draft và tra cứu booking

### Checklist

- [x] Tạo `AiBookingDraft` có TTL.
- [x] Thu thập ngày, khung, phòng, số khách và voucher qua chat.
- [x] Lưu lịch sử Customer AI cục bộ trên thiết bị trong 30 ngày, giới hạn 10 cuộc trò chuyện và 30 tin nhắn/cuộc.
- [x] Giới hạn riêng tra cứu booking 5 lần/10 phút theo cơ sở và địa chỉ máy khách đã băm.
- [x] Chỉ cho chuỗi khung liên tục theo luật booking hiện tại.
- [x] Quote server-side, hiển thị chi tiết giá trước khi khách tiếp tục.
- [x] Draft không giữ phòng.
- [x] Chuyển draft vào form booking hiện có.
- [x] Khi gửi form, tính lại giá, availability, blacklist, CCCD và voucher.
- [ ] Chỉ booking thắng transaction mới tạo Pay2S intent.
- [ ] Tra cứu booking khi đăng nhập.
- [x] Tra cứu anonymous bằng mã booking + normalized phone.
- [ ] Rate limit riêng cho lookup để chống dò booking.
- [x] Không cho AI đổi/hủy booking.
- [x] Trả contact cơ sở khi khách muốn đổi/hủy.

### Nghiệm thu

- [ ] Draft hết hạn không khóa phòng.
- [ ] Hai khách cùng đặt chỉ một request thắng nếu phòng conflict.
- [ ] AI không bypass Pay2S, voucher, blacklist hoặc CCCD.
- [ ] Không thể dò booking bằng mã đơn thuần.

## Giai đoạn 5 — Staff AI

### Checklist

- [x] Thêm policy `UseStaffAi`; mọi truy vấn vẫn qua property access và quyền tài chính riêng.
- [x] Công cụ booking hôm nay/ngày mai/cuối tuần/tuần này.
- [x] Check-in/check-out theo khoảng thời gian hỗ trợ.
- [x] Phòng đang có khách/trống theo booking hiện tại; kèm clean/dirty/cleaning và cảnh báo hiện trạng chưa xử lý (khẩn cấp/cần kiểm tra).
- [x] Danh sách tình trạng dọn phòng và số phòng cần dọn.
- [x] Booking chờ xác nhận/đang giữ.
- [x] Thực thu chỉ hiện khi có permission `ViewFinance`.
- [x] Staff drawer giữ context kỳ cho câu ngắn tiếp theo; người dùng nói rõ hôm nay/mai/tuần/cuối tuần sẽ thay context.
- [x] Staff không có quyền tài chính không nhận dữ liệu tài chính.
- [x] Log toàn bộ lần gọi công cụ vận hành nội bộ.

### Nghiệm thu

- [x] Ma trận Staff permission được test server-side: quyền tài chính và cách ly dữ liệu giữa các cơ sở có integration test PostgreSQL.
- [ ] Kết quả lịch và housekeeping nhất quán với màn hình quản trị.
- [ ] Không lộ CCCD hoặc secrets.

## Giai đoạn 6 — Dynamic reporting cho Owner/Admin

### Checklist

- [x] Reporting query contracts nhận kỳ và trả khoảng ngày cùng timezone.
- [x] Công suất ngày/tuần/tháng/quý/năm.
- [x] Thực thu, hoàn tiền, chi phí và dòng tiền.
- [x] So sánh kỳ hiện tại với kỳ trước có cùng số ngày.
- [x] Báo cáo theo phòng, khung giờ, loại booking và nguồn booking.
- [x] Giá trị booking trung bình, thời lượng và thời gian đặt trước.
- [x] Khách mới/quay lại và tỷ lệ hủy.
- [x] Bảng phòng/khung bán tốt hoặc kém theo thứ tự giá trị trong kỳ.
- [x] Mỗi báo cáo trả `period`, `timezone`, `definition` và bảng dữ liệu nguồn.
- [x] Cache báo cáo PostgreSQL 5 phút theo cơ sở, kỳ và data version; booking/payment/expense làm mất cache `reports`.
- [x] Drawer Admin trình bày bảng từ typed report, không để model tạo số liệu.
- [x] Link “Xem báo cáo chi tiết” mở màn hình báo cáo đúng cơ sở và tháng chứa kỳ.

### Nghiệm thu

- [x] Số AI trả về khớp trực tiếp với query/report service qua integration test PostgreSQL.
- [x] Doanh thu dùng Payment, không dùng Booking total; đã có regression test tách rõ hai giá trị.
- [x] So sánh tháng hiện tại với tháng trước dùng đúng khoảng ngày month-to-date tương ứng.
- [x] Có regression tests cho timezone, biên nửa đêm, kỳ chưa kết thúc và tháng dài/ngắn.

## Giai đoạn 7 — Mở rộng Admin AI actions

### Checklist

- [x] Chuẩn hóa mỗi mutation thành typed proposal riêng.
- [x] Mở rộng proposal cho nội dung website/SEO/phòng theo allowlist đã duyệt.
- [x] Preview bảng trước/sau dễ đọc.
- [x] Admin xác nhận đúng một lần tại nút áp dụng.
- [x] Stale-state check trước mutation.
- [x] Transaction và rollback toàn batch.
- [x] Audit người yêu cầu, người duyệt và dữ liệu trước/sau; lưu riêng `AppliedByUserId`/`RejectedByUserId`.
- [x] Không cho AI tự chạy mutation khi chỉ có câu “xác nhận” không gắn proposal hợp lệ.
- [x] Không mở quyền sửa payment/email/API credentials.

### Nghiệm thu

- [x] Proposal hết hạn hoặc đã xử lý không áp dụng lại được.
- [x] Batch lỗi giữa chừng rollback toàn bộ.
- [x] Không thể tham chiếu đối tượng thuộc cơ sở khác.

## Giai đoạn 8 — AI analysis và đề xuất kinh doanh

### Checklist

- [x] Tách `Data facts`, `AI interpretation` và `Recommendation`.
- [x] Nêu rõ khoảng dữ liệu và độ tin cậy.
- [x] Phân tích thực thu/công suất giảm dựa trên kỳ so sánh.
- [x] Gợi ý khung giờ có thể cần promotion khi công suất thấp.
- [x] Không tự thay đổi giá hoặc tạo voucher từ recommendation.
- [x] Recommendation muốn áp dụng phải tạo proposal mới.
- [x] Không gọi nhận định là dữ liệu thực tế hoặc dự báo.
- [x] Dưới 10 booking trong hai kỳ chỉ báo thiếu dữ liệu, không đưa khuyến nghị thay đổi.

### Nghiệm thu

- [x] Fact khớp reporting service.
- [x] Nhận định không thêm số liệu ngoài tool result.
- [x] Recommendation ghi rõ là đề xuất tham khảo và không tự mutation.

## Giai đoạn 9 — Security, QA, rollout và vận hành

### Checklist

- [x] Unit test intent normalization và cache key.
- [x] Integration test typed tools với PostgreSQL QA cô lập (24/24 Admin AI; toàn bộ suite 298/298 ngày 12/09/2026).
- [x] Concurrent test booking draft → booking thật: hai request từ draft cùng phòng/khung chạy bằng hai `DbContext`, PostgreSQL chỉ cho một request thắng.
- [x] Authorization/property isolation: Admin endpoint role/property filter + apply không chạm cơ sở khác; Staff integration test quyền tài chính/property; Customer knowledge integration test loại dữ liệu cơ sở khác và PII.
- [x] Prompt injection tests từ chat, file và nội dung database; proposal ngoài allowlist bị server từ chối dù provider trả về.
- [x] Rate-limit/quota/budget concurrency tests: quota và ngân sách provider được giữ bằng hàng đợi PostgreSQL có khóa theo cơ sở; request đồng thời không thể cùng tiêu phần hạn mức cuối.
- [x] Cache invalidation và restart recovery tests: cache PostgreSQL đọc lại được từ DbContext mới; đổi giá xóa cache pricing và đánh dấu/tăng version knowledge snapshot.
- [ ] Browser UAT desktop/mobile Customer, Staff và Admin.
- [ ] UAT OpenAI, Gemini và DeepSeek thật với ngân sách nhỏ.
- [x] Đọc input/output/cached token từ OpenAI, Gemini và DeepSeek; lưu cost snapshot theo đơn giá Admin cấu hình. Đối soát hóa đơn thật vẫn thuộc UAT provider.
- [ ] Load test public chat và availability.
- [x] Dashboard vận hành: lỗi provider, cache hit, latency, input/output/cached token, chi phí, request bị chặn và phần quota đang được giữ.
- [x] Kill switch toàn hệ thống qua `Ai:GloballyEnabled`; kill switch theo cơ sở và riêng Customer AI qua profile.
- [ ] Migration backup/restore rehearsal.
- [x] Rollout theo cơ sở/feature flag qua `PropertyAiProfile.IsEnabled` và `IsPublicAiEnabled`.
- [x] Có fallback: AI/provider tách khỏi booking engine; AI lỗi vẫn dùng booking GUI và trang thông tin thường.

### Nghiệm thu cuối

- [x] Không có đường truy cập secrets/CCCD/payment credentials trong snapshot/tool catalog/allowlist; có source contract và integration test PII snapshot.
- [x] Không có mutation ngoài preview/permission/transaction; prompt injection và proposal ngoài allowlist không thể apply.
- [x] Customer AI không thể xem dữ liệu nội bộ hoặc dữ liệu cơ sở khác qua knowledge snapshot.
- [x] Số liệu thực thu báo cáo khớp payment trong database và timezone cơ sở; công suất segment chờ chốt nghiệp vụ riêng.
- [x] Hết ngân sách public chỉ chặn Customer AI; test xác nhận phần Admin reserve vẫn hoạt động và booking engine không phụ thuộc AI.
- [x] Restart không mất lịch sử, snapshot hoặc persistent cache (xác thực persistent cache/snapshot bằng DbContext mới; lịch sử Admin đã lưu PostgreSQL).
- [x] Build Release đạt; toàn bộ 306/306 test đạt trên PostgreSQL QA ngày 12/09/2026.
- [x] Có checklist rollback production trong `docs/ADMIN-AI-UAT.md`.

## 13. API/UI dự kiến

### Public

- `/api/public/ai/chat`
- `/api/public/ai/conversations/{publicConversationKey}`
- `/api/public/ai/booking-drafts/{draftKey}`
- `/api/public/ai/booking-lookup`

Public conversation key là token ngẫu nhiên, hết hạn và không chứa ID tuần tự.

### Internal

- `/api/admin/properties/{propertyId}/ai/...` giữ Admin AI hiện tại.
- Bổ sung audience/permission tại gateway, không nhân bản business logic.
- Staff endpoint dùng authorization policy phù hợp và `PropertyAccessFilter`.

### Admin settings

- Tab Kết nối provider.
- Tab Ngân sách và đơn giá token.
- Tab Customer AI: bật/tắt, quota, CAPTCHA, model và giới hạn.
- Tab Staff AI: permission và tool được phép.
- Tab Knowledge: trạng thái snapshot, version, rebuild và preview.
- Tab Usage: Customer/Staff/Admin, cache hit, cached token và chi phí.

## 14. Các trường hợp bắt buộc phải test

- [ ] Hai câu khác chữ nhưng cùng intent/tham số dùng cùng cache key.
- [x] Cùng câu nhưng khác ngày/phòng/cơ sở không dùng nhầm cache nhờ canonical parameters + property scope; có unit test property scope.
- [ ] Booking mới làm invalid availability cache.
- [x] Giá mới làm invalid quote cache.
- [x] Sửa phòng/giá/tiện nghi/chính sách thuộc interceptor làm tăng knowledge version.
- [x] Restart ứng dụng nạp lại cache từ PostgreSQL (integration test bằng DbContext mới).
- [x] Khách anonymous không tra booking chỉ bằng mã; Public AI yêu cầu đồng thời mã + phone.
- [x] Mã + sai phone bị từ chối mà không tiết lộ booking có tồn tại.
- [x] Staff không permission tài chính không hỏi được doanh thu/payment.
- [x] Doanh thu loại payment void và trừ refund; integration test đối chiếu payment trong/ngoài kỳ theo timezone cơ sở.
- [ ] Công suất loại booking terminal và ca bị khóa.
- [x] Hết public budget nhưng Admin reserve vẫn hoạt động.
- [x] Nhiều request đồng thời không vượt ngân sách đáng kể; hệ thống giữ chi phí tối đa ước tính trước khi gọi provider và quyết toán theo usage thực tế.
- [ ] Provider lỗi không ảnh hưởng booking engine.
- [x] Prompt injection không thay đổi tool allowlist hoặc quyền.

## 15. Thứ tự ưu tiên đề xuất

1. Giai đoạn 0–2: nền dữ liệu, gateway, cache và cost guard.
2. Giai đoạn 3: Customer AI read-only.
3. Giai đoạn 4: booking draft và tra cứu booking.
4. Giai đoạn 6: báo cáo Owner/Admin.
5. Giai đoạn 5: Staff AI theo permission.
6. Giai đoạn 7: mở rộng Admin actions.
7. Giai đoạn 8–9: phân tích nâng cao và production hardening.

Không triển khai tất cả trong một migration hoặc một đợt release. Mỗi giai đoạn phải có feature flag, migration độc lập, test và rollback riêng.

## 16. Nội dung cần chủ hệ thống review trước khi bắt đầu

- [ ] Đồng ý công thức công suất theo ca/segment.
- [ ] Đồng ý doanh thu thực thu = receipt - refund; expense báo riêng.
- [ ] Đồng ý Customer AI chỉ tạo draft, không trực tiếp tạo/đổi/hủy booking.
- [ ] Đồng ý persistent cache dùng PostgreSQL, FusionCache làm lớp RAM.
- [ ] Đồng ý chưa dùng RAG/vector database.
- [ ] Đồng ý dành mặc định 30% ngân sách cho Admin.
- [ ] Đồng ý rate limit public mặc định trong mục 11.
- [ ] Đồng ý thứ tự triển khai trong mục 15.
