namespace DeLong.Web.Domain.Entities;

public sealed class CustomerAccountTermsAcceptance : EntityBase
{
    public Guid UserId { get; set; }
    public Identity.ApplicationUser User { get; set; } = null!;
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public int TermsVersion { get; set; }
    public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;
}
