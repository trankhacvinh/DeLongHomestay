namespace DeLong.Web.Domain.Entities;

public sealed class CustomerAccountSettings : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public bool RegistrationEnabled { get; set; } = true;
    public bool AuthenticatorEnabled { get; set; } = true;
    public bool LoyaltyEnabled { get; set; }
    public int LoyaltySpendPerPoint { get; set; } = 10_000;
    public string BenefitText { get; set; } = "Lưu thông tin, đặt phòng nhanh và tích điểm cho mỗi lần lưu trú.";
    public string TermsTitle { get; set; } = "Điều khoản tài khoản khách";
    public string TermsHtml { get; set; } = "<p>Tôi đồng ý cung cấp thông tin để quản lý tài khoản và lịch sử đặt phòng.</p>";
    public int TermsVersion { get; set; } = 1;
}
