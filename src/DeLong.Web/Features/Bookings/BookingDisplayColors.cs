using System.Text.RegularExpressions;
using DeLong.Web.Features.Operations;

namespace DeLong.Web.Features.Bookings;

public static partial class BookingDisplayColors
{
    public static IReadOnlyList<(string Value, string Name)> Options { get; } = ForSettings(new(
        "#64748B", "#D39B3C", "#397967", "#176B63", "#C94B4B", "#D6A72C", "#7C5CC4", "#287D9B"));

    public static IReadOnlyList<(string Value, string Name)> ForSettings(BookingCalendarDisplaySettings settings) =>
        new (string Value, string Name)[]
        {
            (settings.UnpaidColor, "Chưa thanh toán hết"), (settings.FlexibleTimeColor, "Giờ linh động"),
            (settings.MixedSlotColor, "Nhiều khung"), (settings.SpecialRequestColor, "Có yêu cầu đặc biệt"),
            (settings.RequestedColor, "Yêu cầu mới"), (settings.HeldColor, "Giữ phòng"),
            (settings.ConfirmedColor, "Đã có khách"), (settings.CheckedInColor, "Đã nhận phòng")
        }.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
        .Select(group => (group.Key, $"{string.Join(" / ", group.Select(x => x.Name))} — {group.Key}"))
        .ToList();

    public static bool IsValid(string? color) => color is null || HexColor().IsMatch(color);

    public static string? Normalize(string? color) => color?.ToUpperInvariant();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
