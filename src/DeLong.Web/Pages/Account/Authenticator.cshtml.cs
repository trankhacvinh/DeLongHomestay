using System.ComponentModel.DataAnnotations;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Account;

[Authorize(Policy = "AdminArea")]
public sealed class AuthenticatorModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public string SharedKey { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public IReadOnlyList<string> RecoveryCodes { get; private set; } = [];
    [BindProperty, Required] public string Code { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync() { await LoadAsync(); return Page(); }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var code = Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
        {
            ModelState.AddModelError(nameof(Code), "Mã Authenticator không đúng.");
            await LoadAsync();
            return Page();
        }
        await userManager.SetTwoFactorEnabledAsync(user, true);
        RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToArray() ?? [];
        await LoadAsync(true);
        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        return RedirectToPage();
    }

    private async Task LoadAsync(bool keepCodes = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return;
        Enabled = user.TwoFactorEnabled;
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key)) { await userManager.ResetAuthenticatorKeyAsync(user); key = await userManager.GetAuthenticatorKeyAsync(user); }
        SharedKey = string.Join(' ', Enumerable.Range(0, (key!.Length + 3) / 4).Select(index => key.Substring(index * 4, Math.Min(4, key.Length - index * 4)).ToLowerInvariant()));
        if (!keepCodes) RecoveryCodes = [];
    }
}
