using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.CustomerAccounts;

public sealed class CustomerAccountSettingsService(AppDbContext db)
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public async Task<CustomerAccountSettingsDto> GetAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var settings = await db.CustomerAccountSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        return ToDto(settings ?? new CustomerAccountSettings { PropertyId = propertyId });
    }

    public async Task<(CustomerAccountSettingsDto? Settings, string? Error)> SaveAsync(
        Guid propertyId,
        UpdateCustomerAccountSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LoyaltySpendPerPoint is < 1 or > 1_000_000_000)
            return (null, "Số tiền quy đổi một điểm phải từ 1 đến 1.000.000.000đ.");
        if (string.IsNullOrWhiteSpace(request.BenefitText) || request.BenefitText.Trim().Length > 1000)
            return (null, "Nội dung lợi ích phải từ 1 đến 1.000 ký tự.");
        if (string.IsNullOrWhiteSpace(request.TermsTitle) || request.TermsTitle.Trim().Length > 240)
            return (null, "Tiêu đề điều khoản phải từ 1 đến 240 ký tự.");
        var termsHtml = Sanitizer.Sanitize(request.TermsHtml ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(termsHtml)) return (null, "Nội dung điều khoản không được để trống.");

        var settings = await db.CustomerAccountSettings.SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null)
        {
            settings = new CustomerAccountSettings { PropertyId = propertyId };
            db.CustomerAccountSettings.Add(settings);
        }

        var termsChanged = !string.Equals(settings.TermsTitle, request.TermsTitle.Trim(), StringComparison.Ordinal) ||
                           !string.Equals(settings.TermsHtml, termsHtml, StringComparison.Ordinal);
        settings.RegistrationEnabled = request.RegistrationEnabled;
        settings.AuthenticatorEnabled = request.AuthenticatorEnabled;
        settings.LoyaltyEnabled = request.LoyaltyEnabled;
        settings.LoyaltySpendPerPoint = request.LoyaltySpendPerPoint;
        settings.BenefitText = request.BenefitText.Trim();
        settings.TermsTitle = request.TermsTitle.Trim();
        settings.TermsHtml = termsHtml;
        if (termsChanged) settings.TermsVersion++;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(settings), null);
    }

    private static CustomerAccountSettingsDto ToDto(CustomerAccountSettings x) => new(
        x.RegistrationEnabled, x.AuthenticatorEnabled, x.LoyaltyEnabled, x.LoyaltySpendPerPoint,
        x.BenefitText, x.TermsTitle, x.TermsHtml, x.TermsVersion);

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "em", "u", "s", "ul", "ol", "li", "h2", "h3", "blockquote", "a" })
            sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        return sanitizer;
    }
}
