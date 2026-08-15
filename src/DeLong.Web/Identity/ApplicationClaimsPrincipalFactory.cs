using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DeLong.Web.Identity;

public static class DeLongClaimTypes
{
    public const string DisplayName = "delong_display_name";
    public const string MustChangePassword = "delong_must_change_password";
}

public sealed class ApplicationClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(DeLongClaimTypes.DisplayName, user.DisplayName));
        }

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(DeLongClaimTypes.MustChangePassword, "true"));
        }

        return identity;
    }
}
