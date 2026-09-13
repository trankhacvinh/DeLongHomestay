# ADR: AI Operating Assistant

- Trạng thái: Accepted
- Ngày: 12/09/2026
- Phạm vi: `src/DeLong.Web`

## Bối cảnh

Hệ thống có ba nhóm sử dụng AI với ranh giới dữ liệu và quyền khác nhau: khách công khai, nhân viên và Admin. AI cần đọc dữ liệu vận hành, tạo báo cáo và hỗ trợ cấu hình, nhưng không được tự truy cập database, bí mật kết nối, CCCD hoặc thay đổi dữ liệu ngoài quy trình duyệt.

## Quyết định

### Gateway và ngân sách

- Mọi provider call của Admin và Customer đi qua `AiAccessGateway`.
- `Ai:GloballyEnabled` là emergency kill switch toàn hệ thống; `PropertyAiProfile.IsEnabled` và `IsPublicAiEnabled` điều khiển theo cơ sở/audience.
- Trước provider call, gateway khóa `PropertyAiProfile` bằng PostgreSQL `FOR UPDATE`, cộng actual usage với các reservation còn hiệu lực và giữ trước token/chi phí tối đa ước tính.
- Khi provider trả lời hoặc lỗi, reservation được quyết toán thành `AiUsageRecord`. Reservation của tiến trình chết hết hạn sau 5 phút.
- Chi phí là ước tính theo đơn giá Admin nhập, không phải hóa đơn của provider. Token thực tế và cached-input token được lưu riêng.

### Typed tools và mutation

- Model không có `DbContext`, SQL hoặc quyền gọi service tùy ý.
- Server cung cấp catalog tool theo `AiAudience` và permission hiện hành.
- Customer AI chỉ đọc public snapshot/live availability và tạo booking draft không PII; draft không giữ phòng và không tạo thanh toán.
- Staff AI chỉ dùng typed query read-only; dữ liệu tài chính yêu cầu `ViewFinance`.
- Admin AI chỉ tạo typed proposal thuộc allowlist. Server resolve ID theo `property_id`, validate payload và dựng bảng before/after.
- Mutation chỉ chạy khi Admin bấm duyệt; apply có transaction, stale-state check, idempotency và audit người yêu cầu/người duyệt. Payment credentials, email credentials, API key, CCCD, custom code và hard-delete không thuộc allowlist.

### Cache và knowledge snapshot

- `PropertyAiKnowledgeSnapshot` là JSON công khai đã lọc, lưu PostgreSQL và có version/hash.
- `AiResponseCache` là persistent cache theo property, audience, normalized intent, canonical parameters và data version.
- Business write được `AiDataInvalidationInterceptor` phân loại theo tag; cache liên quan bị xóa và knowledge snapshot được đánh dấu dirty/tăng version.
- Cache trong RAM nếu bổ sung sau này chỉ là lớp tăng tốc; PostgreSQL vẫn là nguồn bền vững sau restart.

### Quan sát vận hành

- Dashboard theo tháng tách audience, thành công, lỗi provider, request bị chặn, cache hit, latency, input/output/cached token, chi phí và reservation đang xử lý.
- Provider lỗi không thay đổi booking engine; website và form booking thông thường không phụ thuộc provider.

## Hệ quả

- Thêm một lần ghi/khóa ngắn trước provider call để đổi lấy kiểm soát đồng thời đúng theo cơ sở.
- Không thể đảm bảo chi phí khớp hóa đơn nếu Admin cấu hình sai đơn giá; UAT phải đối soát bảng giá và usage thật của từng provider.
- Chức năng mới phải bổ sung typed tool/proposal, permission, property isolation, audit và test; không mở quyền bằng prompt.

## Điều kiện thay đổi ADR

Phải cập nhật ADR trước khi cho AI tự động apply không cần duyệt, đọc dữ liệu nhạy cảm, dùng RAG/vector store, thêm provider mới hoặc thay PostgreSQL persistent cache bằng kho khác.
