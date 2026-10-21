namespace DeLong.Web.Features.Bookings;

public static class BookingDisplayColors
{
    public static IReadOnlyList<(string Value, string Name)> Options { get; } =
    [
        ("#C94B4B", "Đỏ — Chưa thanh toán hết"), ("#397967", "Xanh lá — Đã xác nhận"), ("#D39B3C", "Vàng — Giữ phòng"),
        ("#7C5CC4", "Tím — Có yêu cầu đặc biệt"), ("#287D9B", "Xanh dương — Nhiều khung"), ("#7A8582", "Xám — Hoàn tất")
    ];

    public static bool IsValid(string? color) => color is null ||
        Options.Any(x => string.Equals(x.Value, color, StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? color) => color?.ToUpperInvariant();
}
