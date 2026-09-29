namespace DeLong.Web.Features.Site;

public sealed record PublicPolicyLink(string Slug, string Title);

public static class PublicPolicyLinks
{
    public static readonly IReadOnlyList<PublicPolicyLink> All =
    [
        new("chinh-sach-dat-phong", "Chính sách đặt phòng"),
        new("chinh-sach-huy-phong", "Chính sách hoàn/hủy phòng"),
        new("chinh-sach-bao-mat-thong-tin", "Chính sách bảo mật thông tin"),
        new("chinh-sach-thanh-toan", "Chính sách thanh toán"),
        new("dieu-khoan-va-dieu-kien-giao-dich-chung", "Điều khoản và điều kiện giao dịch chung")
    ];
}
