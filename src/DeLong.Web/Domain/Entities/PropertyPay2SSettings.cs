namespace DeLong.Web.Domain.Entities;

public sealed class PropertyPay2SSettings : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = "De Long Homestay";
    public string AccessKeyProtected { get; set; } = string.Empty;
    public string SecretKeyProtected { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankId { get; set; } = "ACB";
    public string ApiEndpoint { get; set; } = "https://payment.pay2s.vn/v1/gateway/api/create";
    public string CallbackBaseUrl { get; set; } = string.Empty;
    public int HoldMinutes { get; set; } = 15;
    public int SettlementGraceMinutes { get; set; } = 3;
}
