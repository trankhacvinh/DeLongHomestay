using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeLong.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedPublicPolicyPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var seededAtUtc = new DateTime(2026, 9, 29, 10, 49, 49, DateTimeKind.Utc);
            foreach (var page in Pages())
            {
                var sections = new[]
                {
                    Section(page.Id, "Hero", "Mở đầu", "centered", new
                    {
                        eyebrow = "CHÍNH SÁCH",
                        title = page.Title,
                        body = page.Description,
                        primaryText = string.Empty,
                        primaryUrl = string.Empty,
                        secondaryText = string.Empty,
                        secondaryUrl = string.Empty,
                        imageUrl = string.Empty
                    }, 0),
                    Section(page.Id, "RichText", "Nội dung chính sách", "wide", new { html = page.Html }, 1)
                };
                var payload = JsonSerializer.Serialize(new
                {
                    version = 1,
                    title = page.Title,
                    slug = page.Slug,
                    isPublished = true,
                    hideFromNavigation = true,
                    seoTitle = page.Title,
                    seoDescription = page.Description,
                    ogImageUrl = string.Empty,
                    noIndex = false,
                    canonicalUrl = string.Empty,
                    legacySlugs = Array.Empty<string>(),
                    sections
                });

                migrationBuilder.Sql($$"""
                    INSERT INTO home_section
                        (id, property_id, type, name, variant, content_json, sort_order, is_visible, created_at_utc, updated_at_utc)
                    SELECT
                        '{{page.Id}}'::uuid, NULL, '__CustomPage', '{{Sql(page.Title)}}', 'custom-page',
                        '{{Sql(payload)}}'::jsonb, -2147483628, FALSE,
                        TIMESTAMPTZ '{{seededAtUtc:yyyy-MM-dd HH:mm:ss}}Z', TIMESTAMPTZ '{{seededAtUtc:yyyy-MM-dd HH:mm:ss}}Z'
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM home_section
                        WHERE property_id IS NULL
                          AND type = '__CustomPage'
                          AND content_json ->> 'slug' = '{{Sql(page.Slug)}}'
                    );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM home_section
                WHERE id IN (
                    '0199a274-1000-7000-8000-000000000001'::uuid,
                    '0199a274-1000-7000-8000-000000000002'::uuid,
                    '0199a274-1000-7000-8000-000000000003'::uuid,
                    '0199a274-1000-7000-8000-000000000004'::uuid,
                    '0199a274-1000-7000-8000-000000000005'::uuid
                )
                  AND updated_at_utc = TIMESTAMPTZ '2026-09-29 10:49:49Z';
                """);
        }

        private static object Section(Guid pageId, string type, string name, string variant, object content, int sortOrder) => new
        {
            id = SectionId(pageId, sortOrder),
            type,
            name,
            variant,
            contentJson = JsonSerializer.Serialize(content),
            sortOrder,
            isVisible = true
        };

        private static string Sql(string value) => value.Replace("'", "''", StringComparison.Ordinal);

        private static Guid SectionId(Guid pageId, int sortOrder)
        {
            var bytes = pageId.ToByteArray();
            bytes[0] ^= (byte)(sortOrder + 1);
            return new Guid(bytes);
        }

        private static IReadOnlyList<PolicySeed> Pages() =>
        [
            new(new Guid("0199a274-1000-7000-8000-000000000001"), "chinh-sach-dat-phong", "Chính sách đặt phòng",
                "Quy trình chọn phòng, thanh toán và xác nhận đặt phòng tại De Long Homestay.",
                """
                <h2>1. Phạm vi áp dụng</h2><p>Chính sách này áp dụng cho yêu cầu đặt phòng trực tiếp trên website De Long Homestay và các yêu cầu được nhân viên tạo thay cho khách.</p>
                <h2>2. Quy trình đặt phòng</h2><ul><li>Chọn phòng, ngày và khung giờ còn nhận khách trên lịch đặt phòng.</li><li>Kiểm tra giờ nhận phòng, giờ trả phòng thực tế, số khách, giá và ghi chú trước khi gửi yêu cầu.</li><li>Cung cấp thông tin liên hệ và giấy tờ nhận dạng khi biểu mẫu yêu cầu.</li><li>Thanh toán đúng số tiền và nội dung chuyển khoản hiển thị trên mã QR.</li><li>Đơn chỉ được xác nhận khi hệ thống ghi nhận thanh toán thành công hoặc nhân viên xác nhận theo phương thức đã thỏa thuận.</li></ul>
                <h2>3. Thời gian giữ phòng</h2><p>Trong thời gian chờ thanh toán, phòng chỉ được giữ đến thời điểm đếm ngược hiển thị trên màn hình. Khi thời gian này kết thúc mà chưa ghi nhận thanh toán, yêu cầu có thể tự động hết hạn để phòng được mở lại.</p>
                <h2>4. Giờ nhận và trả phòng</h2><p>Giờ nhận, trả phòng trên đơn xác nhận là thời gian áp dụng cho booking. Khi lịch phòng cần thêm thời gian dọn phòng hoặc bị ảnh hưởng bởi lượt ở trước, website sẽ hiển thị giờ nhận và trả thực tế để khách cân nhắc trước khi đặt.</p>
                <h2>5. Thông tin chính xác</h2><p>Khách chịu trách nhiệm kiểm tra số điện thoại, email, ngày sử dụng, phòng, khung giờ và số tiền trước khi thanh toán. Nếu phát hiện sai lệch, hãy liên hệ De Long Homestay sớm nhất để được kiểm tra.</p>
                <h2>6. Xác nhận và tra cứu</h2><p>Sau khi thanh toán được ghi nhận, khách có thể dùng mã booking và thông tin liên hệ để tra cứu trạng thái đơn. Hãy lưu mã booking cho đến khi hoàn tất kỳ lưu trú.</p>
                """),
            new(new Guid("0199a274-1000-7000-8000-000000000002"), "chinh-sach-huy-phong", "Chính sách hoàn/hủy phòng",
                "Nguyên tắc tiếp nhận yêu cầu đổi lịch, hủy phòng và hoàn tiền.",
                """
                <h2>1. Gửi yêu cầu</h2><p>Yêu cầu đổi lịch hoặc hủy phòng cần được gửi qua kênh liên hệ chính thức của De Long Homestay và cung cấp mã booking, số điện thoại đặt phòng cùng lý do yêu cầu.</p>
                <h2>2. Điều kiện áp dụng</h2><p>Khả năng đổi lịch, số tiền được hoàn và chi phí phát sinh phụ thuộc vào loại phòng, khung giờ, ưu đãi, thời điểm gửi yêu cầu và điều kiện ghi trên xác nhận đặt phòng. Nhân viên sẽ thông báo phương án cụ thể để khách xác nhận trước khi xử lý.</p>
                <h2>3. Trường hợp không đến nhận phòng</h2><p>Nếu khách không đến trong thời gian đã đặt và không báo trước, booking có thể được xử lý là không đến nhận phòng. Khoản thanh toán đã thực hiện sẽ được xử lý theo điều kiện của booking đó.</p>
                <h2>4. Cơ sở không thể cung cấp phòng</h2><p>Nếu De Long Homestay không thể cung cấp đúng dịch vụ đã xác nhận, cơ sở sẽ chủ động liên hệ để đề xuất đổi phòng, đổi lịch hoặc hoàn lại khoản tiền tương ứng mà khách đã thanh toán.</p>
                <h2>5. Thời gian hoàn tiền</h2><p>Sau khi phương án hoàn tiền được hai bên xác nhận, De Long Homestay sẽ thực hiện theo phương thức phù hợp. Thời gian tiền về tài khoản phụ thuộc vào ngân hàng và kênh thanh toán; thông tin xử lý sẽ được lưu cùng booking.</p>
                """),
            new(new Guid("0199a274-1000-7000-8000-000000000003"), "chinh-sach-bao-mat-thong-tin", "Chính sách bảo mật thông tin",
                "Cách De Long Homestay thu thập, sử dụng và bảo vệ thông tin của khách.",
                """
                <h2>1. Thông tin được thu thập</h2><ul><li>Họ tên, số điện thoại, email và thông tin liên hệ cần thiết.</li><li>Thông tin booking như phòng, ngày giờ, số khách, ghi chú và lịch sử trạng thái.</li><li>Ảnh giấy tờ nhận dạng khi cần cho thủ tục nhận phòng và yêu cầu pháp luật.</li><li>Thông tin đối soát thanh toán như số tiền, mã giao dịch và nội dung chuyển khoản.</li></ul>
                <h2>2. Mục đích sử dụng</h2><p>Thông tin được dùng để tạo và xác nhận booking, hỗ trợ nhận/trả phòng, đối soát thanh toán, chăm sóc khách hàng, giải quyết khiếu nại và đáp ứng nghĩa vụ pháp lý.</p>
                <h2>3. Bảo vệ dữ liệu</h2><p>De Long Homestay giới hạn quyền truy cập theo nhiệm vụ của nhân viên và áp dụng biện pháp kỹ thuật phù hợp để bảo vệ dữ liệu. Website không yêu cầu khách cung cấp mật khẩu ngân hàng, mã OTP hoặc thông tin đăng nhập ngân hàng.</p>
                <h2>4. Chia sẻ thông tin</h2><p>Thông tin chỉ được chia sẻ cho đơn vị cung cấp dịch vụ cần thiết để vận hành booking, thanh toán hoặc theo yêu cầu hợp pháp của cơ quan có thẩm quyền. De Long Homestay không bán thông tin cá nhân của khách.</p>
                <h2>5. Thời gian lưu trữ</h2><p>Dữ liệu được lưu trong thời gian cần thiết cho vận hành, kế toán, giải quyết tranh chấp và nghĩa vụ pháp luật; sau đó được xóa hoặc ẩn danh theo quy trình phù hợp.</p>
                <h2>6. Quyền của khách</h2><p>Khách có thể yêu cầu kiểm tra, cập nhật hoặc đề nghị xử lý thông tin cá nhân bằng cách liên hệ De Long Homestay. Một số dữ liệu vẫn cần được giữ lại khi pháp luật hoặc nghĩa vụ giao dịch yêu cầu.</p>
                """),
            new(new Guid("0199a274-1000-7000-8000-000000000004"), "chinh-sach-thanh-toan", "Chính sách thanh toán",
                "Phương thức thanh toán, xác nhận giao dịch và cách xử lý sai lệch.",
                """
                <h2>1. Phương thức thanh toán</h2><p>Website hỗ trợ thanh toán chuyển khoản ngân hàng bằng mã QR hiển thị tại bước thanh toán. Các phương thức khác chỉ có hiệu lực khi được De Long Homestay xác nhận trực tiếp.</p>
                <h2>2. Nội dung chuyển khoản</h2><p>Khách cần chuyển đúng số tiền, đúng tài khoản và giữ nguyên nội dung thanh toán được tạo cho booking. Thay đổi nội dung hoặc chuyển thiếu tiền có thể khiến hệ thống không tự xác nhận được giao dịch.</p>
                <h2>3. Xác nhận thanh toán</h2><p>Trạng thái booking được cập nhật sau khi hệ thống nhận và đối chiếu giao dịch. Thông báo trừ tiền từ ngân hàng chưa đồng nghĩa booking đã được xác nhận nếu giao dịch chưa khớp mã, số tiền hoặc tài khoản nhận.</p>
                <h2>4. Giao dịch chưa được ghi nhận</h2><p>Nếu tài khoản đã bị trừ tiền nhưng booking chưa cập nhật, khách nên giữ biên lai và liên hệ De Long Homestay kèm mã booking, thời gian, số tiền và mã tham chiếu để nhân viên đối soát.</p>
                <h2>5. An toàn thanh toán</h2><p>De Long Homestay không yêu cầu cung cấp mật khẩu ngân hàng hoặc mã OTP. Khách chỉ nên thanh toán theo thông tin hiển thị trên website chính thức và kiểm tra tên người nhận trước khi xác nhận chuyển khoản.</p>
                """),
            new(new Guid("0199a274-1000-7000-8000-000000000005"), "dieu-khoan-va-dieu-kien-giao-dich-chung", "Điều khoản và điều kiện giao dịch chung",
                "Các nguyên tắc chung khi sử dụng website và dịch vụ lưu trú của De Long Homestay.",
                """
                <h2>1. Chấp nhận điều khoản</h2><p>Khi gửi yêu cầu đặt phòng hoặc sử dụng dịch vụ, khách xác nhận đã đọc thông tin phòng, giá, thời gian sử dụng, nội quy và các chính sách liên quan tại thời điểm giao dịch.</p>
                <h2>2. Thông tin dịch vụ</h2><p>De Long Homestay cố gắng trình bày chính xác tình trạng phòng, hình ảnh, tiện nghi, giá và thời gian. Trường hợp có sai sót rõ ràng, cơ sở sẽ liên hệ để làm rõ và chỉ tiếp tục giao dịch sau khi khách đồng ý.</p>
                <h2>3. Trách nhiệm của khách</h2><ul><li>Cung cấp thông tin đúng và có quyền sử dụng phương thức thanh toán đã chọn.</li><li>Tuân thủ số người, giờ nhận/trả phòng, quy định an toàn và nội quy tại cơ sở.</li><li>Giữ gìn tài sản, thông báo sớm các sự cố và chịu trách nhiệm đối với thiệt hại do mình hoặc người đi cùng gây ra theo thỏa thuận và pháp luật.</li></ul>
                <h2>4. Thay đổi booking</h2><p>Mọi thay đổi về phòng, thời gian, số khách, dịch vụ hoặc giá chỉ có hiệu lực khi được cập nhật trên booking hoặc được De Long Homestay xác nhận qua kênh liên hệ chính thức.</p>
                <h2>5. Sự kiện ngoài khả năng kiểm soát</h2><p>Khi dịch vụ bị ảnh hưởng bởi thiên tai, mất điện diện rộng, yêu cầu của cơ quan chức năng hoặc sự kiện khách quan khác, hai bên sẽ phối hợp chọn phương án hợp lý dựa trên phần dịch vụ chưa sử dụng.</p>
                <h2>6. Giải quyết phản ánh</h2><p>Phản ánh được ưu tiên giải quyết dựa trên booking, lịch sử thanh toán và trao đổi đã lưu. Hai bên ưu tiên thương lượng; nếu không đạt thỏa thuận, việc giải quyết thực hiện theo pháp luật Việt Nam.</p>
                <h2>7. Cập nhật điều khoản</h2><p>Nội dung có thể được cập nhật để phù hợp với hoạt động và quy định pháp luật. Phiên bản áp dụng cho booking là nội dung được công bố tại thời điểm khách thực hiện giao dịch.</p>
                """)
        ];

        private sealed record PolicySeed(Guid Id, string Slug, string Title, string Description, string Html);
    }
}
