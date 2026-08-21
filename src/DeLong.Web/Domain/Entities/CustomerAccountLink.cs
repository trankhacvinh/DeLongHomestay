namespace DeLong.Web.Domain.Entities;

public sealed class CustomerAccountLink
{
    public Guid UserId { get; set; }
    public Identity.ApplicationUser User { get; set; } = null!;
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
