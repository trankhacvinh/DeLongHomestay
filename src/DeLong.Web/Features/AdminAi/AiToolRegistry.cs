using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.AdminAi;

public sealed record AiToolDescriptor(string Name, string Description, IReadOnlySet<AiAudience> Audiences, bool IsMutation = false, string? RequiredPermission = null);

public sealed class AiToolRegistry
{
    private static readonly IReadOnlyList<AiToolDescriptor> Tools =
    [
        new("property_info", "Thông tin công khai và liên hệ của cơ sở.", Set(AiAudience.Customer, AiAudience.Staff, AiAudience.Admin)),
        new("room_search", "Tìm và so sánh phòng theo sức chứa, tiện nghi.", Set(AiAudience.Customer, AiAudience.Staff, AiAudience.Admin)),
        new("room_price", "Tính giá phòng từ PricingService theo ngày và khung.", Set(AiAudience.Customer, AiAudience.Staff, AiAudience.Admin)),
        new("room_availability", "Kiểm tra phòng trống theo thời gian thực.", Set(AiAudience.Customer, AiAudience.Staff, AiAudience.Admin)),
        new("booking_lookup", "Tra cứu booking bằng tài khoản hoặc mã và số điện thoại.", Set(AiAudience.Customer, AiAudience.Staff, AiAudience.Admin)),
        new("operations_summary", "Tóm tắt booking, check-in/out và dọn phòng.", Set(AiAudience.Staff, AiAudience.Admin), RequiredPermission: "UseStaffAi"),
        new("payment_summary", "Tổng hợp tiền thực thu và hoàn tiền.", Set(AiAudience.Staff, AiAudience.Admin), RequiredPermission: "ViewFinance"),
        new("business_report", "Báo cáo quản trị theo kỳ và cơ sở.", Set(AiAudience.Admin), RequiredPermission: "UseAdminAi"),
        new("configuration_proposal", "Tạo preview cấu hình để Admin duyệt.", Set(AiAudience.Admin), true, "UseAdminAi")
    ];

    public IReadOnlyList<AiToolDescriptor> For(AiAudience audience, Func<string, bool>? hasPermission = null) =>
        Tools.Where(x => x.Audiences.Contains(audience) && (x.RequiredPermission is null || hasPermission?.Invoke(x.RequiredPermission) == true))
            .ToArray();

    private static IReadOnlySet<AiAudience> Set(params AiAudience[] values) => values.ToHashSet();
}
