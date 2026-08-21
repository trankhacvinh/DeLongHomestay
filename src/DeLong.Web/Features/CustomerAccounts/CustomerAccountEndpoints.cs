using DeLong.Web.Common.Security;
using DeLong.Web.Features.Site;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Common.Operations;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DeLong.Web.Features.CustomerAccounts;

public static class CustomerAccountEndpoints
{
    public static IEndpointRouteBuilder MapCustomerAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/admin/properties/{propertyId:guid}/customer-account-settings")
            .RequireAuthorization("ManageRooms")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Customer Accounts");

        settings.MapGet("/", async (Guid propertyId, CustomerAccountSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(propertyId, ct)));

        settings.MapPut("/", async (
            Guid propertyId,
            UpdateCustomerAccountSettingsRequest request,
            CustomerAccountSettingsService service,
            CancellationToken ct) =>
        {
            var (result, error) = await service.SaveAsync(propertyId, request, ct);
            return error is null ? Results.Ok(result) : Results.Problem(title: "Không thể lưu cấu hình tài khoản khách", detail: error, statusCode: 400);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        app.MapGet("/api/public/customer-account/settings", async (
            string? siteSlug,
            PublicPropertyResolver resolver,
            CustomerAccountSettingsService service,
            CancellationToken ct) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, ct);
            return property is null ? Results.NotFound() : Results.Ok(await service.GetAsync(property.Id, ct));
        }).AllowAnonymous().WithTags("Customer Accounts");

        app.MapGet("/api/public/customer-account/status", async (
            string phone,
            string? siteSlug,
            PublicPropertyResolver resolver,
            CustomerAccountService service,
            CancellationToken ct) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return Results.NotFound();
            var hasAccount = await service.CustomerAccountExistsAsync(property.Id, phone, ct);
            var exists = hasAccount || await service.CustomerProfileExistsAsync(property.Id, phone, ct);
            return Results.Ok(new { exists, hasAccount });
        }).AllowAnonymous().RequireRateLimiting("account-login").WithTags("Customer Accounts");

        app.MapPost("/api/public/customer-account/register", async (
            string? siteSlug,
            RegisterCustomerRequest request,
            PublicPropertyResolver resolver,
            CustomerAccountService service,
            SignInManager<ApplicationUser> signInManager,
            CancellationToken ct) =>
        {
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null) return Results.NotFound();
            var (user, error) = await service.RegisterAsync(property.Id, request, ct);
            if (user is null) return Results.Problem(title: "Không thể tạo tài khoản", detail: error, statusCode: 400);
            await signInManager.SignInAsync(user, isPersistent: true);
            return Results.Ok(new { registered = true, redirectUrl = PublicPropertyResolver.ScopePrefix(siteSlug) + "/customer/account" });
        }).AllowAnonymous().AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("account-login").WithTags("Customer Accounts");

        app.MapPost("/api/public/customer-account/login", async (
            CustomerLoginRequest request,
            CustomerAccountService service,
            SignInManager<ApplicationUser> signInManager,
            HttpContext httpContext) =>
        {
            var user = await service.FindLoginUserAsync(request.Phone, httpContext.RequestAborted);
            if (user is null) return InvalidLogin();
            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded) return InvalidLogin(result.IsLockedOut);
            await signInManager.SignInAsync(user, request.RememberMe);
            return Results.Ok(new { signedIn = true, redirectUrl = "/Customer/Profile" });
        }).AllowAnonymous().AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("account-login").WithTags("Customer Accounts");

        app.MapPost("/api/public/customer-account/authenticator-login", async (
            string? siteSlug,
            CustomerAuthenticatorLoginRequest request,
            PublicPropertyResolver resolver,
            CustomerAccountSettingsService settingsService,
            CustomerAccountService service,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var property = await resolver.ResolveAsync(siteSlug);
            if (property is null || !(await settingsService.GetAsync(property.Id)).AuthenticatorEnabled) return InvalidLogin();
            var user = await service.FindLoginUserAsync(request.Phone);
            if (user is null || !user.TwoFactorEnabled) return InvalidLogin();
            var valid = await userManager.VerifyTwoFactorTokenAsync(
                user, TokenOptions.DefaultAuthenticatorProvider, request.Code.Replace(" ", string.Empty).Replace("-", string.Empty));
            if (!valid)
            {
                await userManager.AccessFailedAsync(user);
                return InvalidLogin();
            }
            await userManager.ResetAccessFailedCountAsync(user);
            await signInManager.SignInAsync(user, request.RememberMe);
            return Results.Ok(new { signedIn = true, redirectUrl = "/Customer/Profile" });
        }).AllowAnonymous().AddEndpointFilter<ApiAntiforgeryFilter>().RequireRateLimiting("account-login").WithTags("Customer Accounts");

        app.MapGet("/api/customer/account/profile", async (
            ClaimsPrincipal principal,
            CustomerAccountService service,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(id, out var userId)) return Results.Unauthorized();
            var profile = await service.GetProfileAsync(userId, ct);
            if (profile is null) return Results.NotFound();
            var storage = new IdentityDocumentStorage(paths, configuration);
            var propertyIds = await service.GetLinkedPropertyIdsAsync(userId, ct);
            var hasDocuments = false;
            foreach (var propertyId in propertyIds)
            {
                var documents = await storage.ListAsync(propertyId, userId, ct);
                if (documents.Any(x => x.Side == "front") && documents.Any(x => x.Side == "back"))
                {
                    hasDocuments = true;
                    break;
                }
            }
            return Results.Ok(profile with { HasIdentityDocuments = hasDocuments });
        }).RequireAuthorization(policy => policy.RequireRole(CustomerAccountService.CustomerRole)).WithTags("Customer Accounts");

        var customer = app.MapGroup("/api/customer/account")
            .RequireAuthorization(policy => policy.RequireRole(CustomerAccountService.CustomerRole))
            .WithTags("Customer Accounts");

        customer.MapPost("/identity-documents/{side}", async (
            string side,
            string? siteSlug,
            ClaimsPrincipal principal,
            HttpRequest httpRequest,
            PublicPropertyResolver resolver,
            CustomerAccountService service,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(id, out var userId)) return Results.Unauthorized();
            var property = await resolver.ResolveAsync(siteSlug, ct);
            if (property is null || !await service.HasPropertyLinkAsync(userId, property.Id, ct)) return Results.NotFound();
            if (!httpRequest.HasFormContentType) return Results.BadRequest(new { message = "Vui lòng chọn ảnh CCCD." });
            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null) return Results.BadRequest(new { message = "Vui lòng chọn ảnh CCCD." });
            var storage = new IdentityDocumentStorage(paths, configuration);
            var (document, error) = await storage.SaveAsync(property.Id, userId, side, file, ct);
            return error is null ? Results.Ok(document) : Results.Problem(title: "Không thể lưu CCCD", detail: error, statusCode: 400);
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        customer.MapPost("/change-password", async (
            ClaimsPrincipal principal,
            ChangeCustomerPasswordRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (request.NewPassword.Length < 8) return Results.Problem(title: "Mật khẩu không hợp lệ", detail: "Mật khẩu mới phải có ít nhất 8 ký tự.", statusCode: 400);
            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded) return Results.Problem(title: "Không thể đổi mật khẩu", detail: string.Join(" ", result.Errors.Select(x => x.Description)), statusCode: 400);
            await signInManager.RefreshSignInAsync(user);
            return Results.Ok(new { changed = true });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        customer.MapGet("/authenticator", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var key = await userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key))
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
                key = await userManager.GetAuthenticatorKeyAsync(user);
            }
            var issuer = Uri.EscapeDataString("De Long Homestay");
            var account = Uri.EscapeDataString(user.PhoneNumber ?? user.UserName ?? user.Id.ToString());
            return Results.Ok(new
            {
                enabled = user.TwoFactorEnabled,
                sharedKey = FormatKey(key!),
                authenticatorUri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6"
            });
        });

        customer.MapPost("/authenticator/confirm", async (
            ClaimsPrincipal principal,
            ConfirmAuthenticatorRequest request,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var valid = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
            if (!valid) return Results.Problem(title: "Mã không hợp lệ", detail: "Mã Authenticator không đúng.", statusCode: 400);
            await userManager.SetTwoFactorEnabledAsync(user, true);
            var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
            return Results.Ok(new { enabled = true, recoveryCodes = recoveryCodes?.ToArray() ?? [] });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        customer.MapPost("/authenticator/disable", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            await userManager.SetTwoFactorEnabledAsync(user, false);
            await userManager.ResetAuthenticatorKeyAsync(user);
            return Results.Ok(new { enabled = false });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static IResult InvalidLogin(bool locked = false) => Results.Problem(
        title: locked ? "Tài khoản tạm thời bị khóa" : "Đăng nhập không thành công",
        detail: locked ? "Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau." : "Số điện thoại, mật khẩu hoặc mã Authenticator không đúng.",
        statusCode: locked ? StatusCodes.Status423Locked : StatusCodes.Status400BadRequest);

    private static string FormatKey(string key) => string.Join(' ', Enumerable.Range(0, (key.Length + 3) / 4)
        .Select(index => key.Substring(index * 4, Math.Min(4, key.Length - index * 4))).Select(x => x.ToLowerInvariant()));
}
