using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DeLong.Web.Features.PublicAi;

public enum PublicAiIntent
{
    General,
    Availability,
    Pricing,
    BookingDraft,
    BookingLookup,
    ChangeOrCancel
}

public sealed record PublicAiQuestionContext(PublicAiIntent Intent, DateOnly? Date, string? BookingCode = null, string? Phone = null);

public static partial class PublicAiQuestionAnalyzer
{
    public static PublicAiQuestionContext Analyze(string message, DateOnly today)
    {
        var normalized = Normalize(message);
        var intent = ContainsAny(normalized, "huy booking", "huy phong", "huy dat", "doi booking", "doi phong", "doi ngay", "thay doi booking")
            ? PublicAiIntent.ChangeOrCancel
            : ContainsAny(normalized, "tra booking", "tra cuu", "kiem tra booking", "trang thai booking", "booking cua toi")
                ? PublicAiIntent.BookingLookup
            : ContainsAny(normalized, "dat phong", "muon dat", "book phong", "giu phong")
                ? PublicAiIntent.BookingDraft
            : ContainsAny(normalized, "con phong", "phong trong", "trong khong", "het phong", "lich phong")
            ? PublicAiIntent.Availability
            : ContainsAny(normalized, "gia", "bao nhieu", "chi phi", "bang gia")
                ? PublicAiIntent.Pricing
                : PublicAiIntent.General;

        return new PublicAiQuestionContext(intent, ParseDate(message, normalized, today),
            BookingCodePattern().Match(message) is { Success: true } code ? code.Value.ToUpperInvariant() : null,
            PhonePattern().Match(message) is { Success: true } phone ? phone.Value : null);
    }

    private static DateOnly? ParseDate(string original, string normalized, DateOnly today)
    {
        if (ContainsAny(normalized, "ngay mai", "sang mai", "trua mai", "chieu mai", "toi mai", "dem mai"))
            return today.AddDays(1);
        if (ContainsAny(normalized, "hom nay", "sang nay", "trua nay", "chieu nay", "toi nay", "dem nay"))
            return today;

        var match = DatePattern().Match(original);
        if (!match.Success) return null;
        var value = match.Value.Replace('-', '/');
        foreach (var format in new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd" })
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;
        return null;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(x => value.Contains(x, StringComparison.Ordinal));

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == 'đ' ? 'd' : character);
        return WhitespacePattern().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ");
    }

    [GeneratedRegex(@"(?<!\d)(?:\d{1,2}[/-]\d{1,2}[/-]\d{4}|\d{4}[/-]\d{1,2}[/-]\d{1,2})(?!\d)")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])BK-[A-Za-z0-9-]{6,40}(?![A-Za-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex BookingCodePattern();

    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)(?:[ .-]?\d){8,10}(?!\d)")]
    private static partial Regex PhonePattern();
}
