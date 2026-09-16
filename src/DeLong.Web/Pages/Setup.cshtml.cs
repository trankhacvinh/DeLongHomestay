using System.ComponentModel.DataAnnotations;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Setup;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages;

[AllowAnonymous]
public sealed class SetupModel(
    InitialSetupService setupService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<SetupModel> logger) : PageModel
{
    [BindProperty]
    public SetupInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await setupService.IsRequiredAsync(cancellationToken))
            return RedirectToPage("/Account/Login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await setupService.IsRequiredAsync(cancellationToken))
            return RedirectToPage("/Account/Login");
        if (!ModelState.IsValid) return Page();

        try
        {
            var result = await setupService.CompleteAsync(
                new InitialSetupRequest(Input.DisplayName, Input.Email, Input.Password, Input.SeedDeLongData),
                cancellationToken);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Không thể hoàn tất setup.");
                return Page();
            }

            var admin = await userManager.FindByIdAsync(result.AdminUserId!.Value.ToString());
            if (admin is null)
            {
                logger.LogError("Initial setup committed but administrator {AdminUserId} could not be loaded.", result.AdminUserId);
                return RedirectToPage("/Account/Login");
            }

            await signInManager.SignInAsync(admin, isPersistent: false);
            return result.SeededDeLongData
                ? RedirectToPage("/Admin/Index", new { propertyId = DbSeeder.DeLongPropertyId })
                : RedirectToPage("/Admin/Properties/Index");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Initial setup failed.");
            ModelState.AddModelError(string.Empty, "Không thể hoàn tất setup. Hãy kiểm tra kết nối và migration cơ sở dữ liệu rồi thử lại.");
            return Page();
        }
    }

    public sealed class SetupInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Tên hiển thị phải từ 2 đến 120 ký tự.")]
        [Display(Name = "Tên quản trị viên")]
        public string DisplayName { get; set; } = "De Long Admin";

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(320)]
        [Display(Name = "Email đăng nhập")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        [Display(Name = "Nhập lại mật khẩu")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Tạo dữ liệu mẫu De Long")]
        public bool SeedDeLongData { get; set; } = true;
    }
}
