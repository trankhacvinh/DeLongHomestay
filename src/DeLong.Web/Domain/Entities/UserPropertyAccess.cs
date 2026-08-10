using DeLong.Web.Identity;

namespace DeLong.Web.Domain.Entities;

public sealed class UserPropertyAccess
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
}
