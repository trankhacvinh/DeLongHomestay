namespace DeLong.Web.Features.CustomerAccounts;

public sealed record CustomerAccountSettingsDto(
    bool RegistrationEnabled,
    bool AuthenticatorEnabled,
    bool LoyaltyEnabled,
    int LoyaltySpendPerPoint,
    string BenefitText,
    string TermsTitle,
    string TermsHtml,
    int TermsVersion);

public sealed record UpdateCustomerAccountSettingsRequest(
    bool RegistrationEnabled,
    bool AuthenticatorEnabled,
    bool LoyaltyEnabled,
    int LoyaltySpendPerPoint,
    string BenefitText,
    string TermsTitle,
    string TermsHtml);

public sealed record RegisterCustomerRequest(
    string Phone,
    string Password,
    string Name,
    string? Email,
    bool TermsAccepted,
    int TermsVersion);

public sealed record CustomerLoginRequest(string Phone, string Password, bool RememberMe);
public sealed record CustomerAuthenticatorLoginRequest(string Phone, string Code, bool RememberMe);
public sealed record ChangeCustomerPasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ConfirmAuthenticatorRequest(string Code);

public sealed record CustomerAccountBookingDto(
    Guid Id,
    string Code,
    string PropertyName,
    string RoomName,
    DateTime CheckInUtc,
    DateTime CheckOutUtc,
    string Status,
    decimal TotalAmount,
    int EarnedPoints);

public sealed record LoyaltyEntryDto(
    Guid Id,
    int Points,
    string Reason,
    string? BookingCode,
    DateTime CreatedAtUtc);

public sealed record CustomerAccountProfileDto(
    Guid UserId,
    string Name,
    string Phone,
    string? Email,
    bool HasIdentityDocuments,
    bool AuthenticatorConfigured,
    int LoyaltyBalance,
    IReadOnlyList<CustomerAccountBookingDto> Bookings,
    IReadOnlyList<LoyaltyEntryDto> LoyaltyHistory);
