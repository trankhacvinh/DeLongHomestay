namespace DeLong.Web.Domain.Entities;

public sealed class Customer : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NormalizedPhone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? IdentityNumber { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}
