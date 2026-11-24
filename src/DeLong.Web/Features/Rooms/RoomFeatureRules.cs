namespace DeLong.Web.Features.Rooms;

public static class RoomFeatureRules
{
    // DeLong's first rooms were badged by code before amenities existed. Keep that only for the DeLong
    // property; every other branch (e.g. The Chilling) shows "Có bồn tắm" from its amenity list.
    private static readonly string[] LegacyDeLongBathtubRooms = ["COCO-01", "MOON-04", "AMBER-05", "ROMAN-06"];

    public static bool HasBathtub(string? propertyCode, string roomCode, IEnumerable<string>? amenityNames) =>
        (amenityNames?.Any(x => x.Contains("bồn tắm", StringComparison.OrdinalIgnoreCase)) ?? false) ||
        (string.Equals(propertyCode, DeLong.Web.Features.Site.PublicPropertyResolver.LegacyPropertyCode, StringComparison.OrdinalIgnoreCase) &&
         LegacyDeLongBathtubRooms.Contains(roomCode, StringComparer.OrdinalIgnoreCase));
}
