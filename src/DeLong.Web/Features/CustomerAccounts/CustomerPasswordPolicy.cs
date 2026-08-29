namespace DeLong.Web.Features.CustomerAccounts;

public static class CustomerPasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MinimumCharacterGroups = 3;

    public const string RequirementMessage =
        "Mật khẩu phải có ít nhất 8 ký tự và kết hợp ít nhất 3 nhóm: chữ thường, chữ hoa, số, ký tự đặc biệt.";

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            return RequirementMessage;

        var groups = 0;
        if (password.Any(char.IsLower)) groups++;
        if (password.Any(char.IsUpper)) groups++;
        if (password.Any(char.IsDigit)) groups++;
        if (password.Any(character => !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character))) groups++;

        return groups >= MinimumCharacterGroups ? null : RequirementMessage;
    }
}
