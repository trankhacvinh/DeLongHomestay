using System.ComponentModel.DataAnnotations;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = NormalizeReturnUrl(returnUrl);

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = NormalizeReturnUrl(returnUrl);
        var target = ReturnUrl ?? Url.Page("/Admin/Index")!;
        if (!ModelState.IsValid) return Page();

        var identifier = Input.Identifier.Trim();
        var user = identifier.Contains('@', StringComparison.Ordinal)
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return Page();
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã ngừng hoạt động. Vui lòng liên hệ quản trị viên.");
            return Page();
        }

        if (user.IsCustomerAccount)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập, email, mật khẩu hoặc mã Authenticator không đúng.");
            return Page();
        }

        var emergencyBypass = configuration.GetValue<bool>("Authentication:AdminEmergencyBypassTwoFactor");
        Microsoft.AspNetCore.Identity.SignInResult result;
        if (emergencyBypass &&
            await userManager.IsInRoleAsync(user, "Admin") &&
            await userManager.CheckPasswordAsync(user, Input.Password))
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
            logger.LogCritical(
                "ADMIN EMERGENCY 2FA BYPASS was used for user {UserId} ({UserName}). Disable Authentication:AdminEmergencyBypassTwoFactor immediately after recovery.",
                user.Id,
                user.UserName);
            await signInManager.SignInAsync(user, Input.RememberMe);
            result = Microsoft.AspNetCore.Identity.SignInResult.Success;
        }
        else
        {
            result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        }
        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("/Account/LoginWith2fa", new { returnUrl = target, rememberMe = Input.RememberMe });
        }
        if (result.Succeeded)
        {
            if (user.MustChangePassword)
            {
                return LocalRedirect($"/Account/ChangePassword?returnUrl={Uri.EscapeDataString(target)}");
            }
            return LocalRedirect(target);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Tên đăng nhập, email hoặc mật khẩu không đúng.");
        return Page();
    }

    private string? NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public sealed class LoginInput
    {
        [Required, Display(Name = "Tên đăng nhập hoặc email")]
        public string Identifier { get; set; } = string.Empty;
        [DataType(DataType.Password), Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;
        [Display(Name = "Ghi nhớ")]
        public bool RememberMe { get; set; }
    }
}
