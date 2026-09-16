namespace DeLong.Web.Domain.Entities;

public sealed class PropertySePaySettings : EntityBase
{
    public Guid PropertyId { get; set; }
    public string ApiTokenProtected { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string BankId { get; set; } = "ACB";
    public string BankAccountNumber { get; set; } = string.Empty;
    public string QrAccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string SubAccount { get; set; } = string.Empty;
    public string MemoPrefix { get; set; } = string.Empty;
    public string WebhookKeyProtected { get; set; } = string.Empty;
    public int HoldMinutes { get; set; } = 15;
    public int SettlementGraceMinutes { get; set; } = 3;
}
