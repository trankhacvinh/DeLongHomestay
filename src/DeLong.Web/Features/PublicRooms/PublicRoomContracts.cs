using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.PublicRooms;

public sealed record PublicRoomRateDto(
    Guid Id,
    string Name,
    string StartTime,
    string EndTime,
    RoomRateType Type,
    decimal Price);

public sealed record PublicRoomImageDto(
    Guid Id,
    string LargeUrl,
    string CardUrl,
    string ThumbnailUrl,
    string AltText,
    bool IsCover,
    int SortOrder,
    int Width,
    int Height,
    int LargeWidth,
    int LargeHeight,
    double FocalX,
    double FocalY);

public sealed record PublicRoomCardDto(
    Guid Id,
    string Code,
    string Name,
    string Slug,
    int Capacity,
    string? ShortDescription,
    string? CoverCardUrl,
    double CoverFocalX,
    double CoverFocalY,
    bool HasBathtub,
    decimal QuickFromPrice,
    decimal? OvernightPrice,
    decimal? NightlyPrice,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Amenities,
    IReadOnlyList<PublicRoomRateDto> Rates);

public sealed record PublicRoomDetailDto(
    Guid Id,
    string Code,
    string Name,
    string Slug,
    int Capacity,
    string? ShortDescription,
    string? DescriptionHtml,
    bool HasBathtub,
    decimal QuickFromPrice,
    decimal? OvernightPrice,
    decimal? NightlyPrice,
    IReadOnlyList<string> Amenities,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<PublicRoomImageDto> Images,
    IReadOnlyList<PublicRoomRateDto> Rates);

public sealed record PublicRoomCatalogDto(IReadOnlyList<PublicRoomCardDto> Rooms);
