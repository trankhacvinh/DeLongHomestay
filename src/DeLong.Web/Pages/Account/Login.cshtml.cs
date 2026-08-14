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
    UserManager<ApplicationUser> userManager) : PageModel
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

        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
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

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
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

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return Page();
    }

    private string? NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public sealed class LoginInput
    {
        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;
        [Display(Name = "Ghi nhớ")]
        public bool RememberMe { get; set; }
    }
}
