using Microsoft.AspNetCore.Identity;

namespace DeLong.Web.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public bool IsCustomerAccount { get; set; }
}
