using System.ComponentModel.DataAnnotations;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginWith2faModel(
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }
    public bool RememberMe { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null, bool rememberMe = false)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToPage("/Account/Login");
        ReturnUrl = NormalizeReturnUrl(returnUrl);
        RememberMe = rememberMe;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, bool rememberMe = false)
    {
        ReturnUrl = NormalizeReturnUrl(returnUrl);
        RememberMe = rememberMe;
        if (!ModelState.IsValid) return Page();

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToPage("/Account/Login");
        if (!user.IsActive || user.IsCustomerAccount) return RedirectToPage("/Account/Login");

        var code = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, rememberMe, Input.RememberBrowser);
        if (result.Succeeded)
        {
            var target = ReturnUrl ?? Url.Page("/Admin/Index")!;
            if (user.MustChangePassword)
                return LocalRedirect($"/Account/ChangePassword?returnUrl={Uri.EscapeDataString(target)}");
            return LocalRedirect(target);
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do nhập sai mã nhiều lần.");
            return Page();
        }
        ModelState.AddModelError(string.Empty, "Mã Authenticator không đúng.");
        return Page();
    }

    private string? NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public sealed class InputModel
    {
        [Required, Display(Name = "Mã Authenticator")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "Tin cậy trình duyệt này")]
        public bool RememberBrowser { get; set; }
    }
}
