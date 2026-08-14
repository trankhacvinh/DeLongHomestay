using System.ComponentModel.DataAnnotations;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    public bool IsRequired { get; private set; }
    public string? ReturnUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        IsRequired = user.MustChangePassword;
        ReturnUrl = NormalizeReturnUrl(returnUrl);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        IsRequired = user.MustChangePassword;
        ReturnUrl = NormalizeReturnUrl(returnUrl);
        if (!ModelState.IsValid) return Page();

        var result = await userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }
            return Page();
        }

        user.MustChangePassword = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var error in update.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["PasswordChanged"] = "Mật khẩu đã được đổi thành công.";
        return LocalRedirect(ReturnUrl ?? Url.Page("/Admin/Index")!);
    }

    private string? NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "PasswordMismatch" => "Mật khẩu hiện tại không đúng.",
        "PasswordTooShort" => "Mật khẩu mới phải có ít nhất 8 ký tự.",
        "PasswordRequiresUniqueChars" => "Mật khẩu mới chưa đủ khác biệt giữa các ký tự.",
        _ => error.Description
    };

    public sealed class ChangePasswordInput
    {
        [Required(ErrorMessage = "Nhập mật khẩu hiện tại."), DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhập mật khẩu mới."), MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự."), DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhập lại mật khẩu mới."), DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Hai mật khẩu mới không khớp."), Display(Name = "Nhập lại mật khẩu mới")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
