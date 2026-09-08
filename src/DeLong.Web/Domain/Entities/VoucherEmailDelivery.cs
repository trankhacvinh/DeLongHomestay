namespace DeLong.Web.Domain.Entities;

public sealed class VoucherEmailDelivery : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public Voucher Voucher { get; set; } = null!;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
