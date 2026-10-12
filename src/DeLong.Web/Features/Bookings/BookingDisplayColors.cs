namespace DeLong.Web.Features.Bookings;

public static class BookingDisplayColors
{
    public static IReadOnlyList<(string Value, string Name)> Options { get; } =
    [
        ("#C94B4B", "Đỏ"), ("#397967", "Xanh lá"), ("#D39B3C", "Vàng"),
        ("#7C5CC4", "Tím"), ("#287D9B", "Xanh dương"), ("#7A8582", "Xám")
    ];

    public static bool IsValid(string? color) => color is null ||
        Options.Any(x => string.Equals(x.Value, color, StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? color) => color?.ToUpperInvariant();
}
