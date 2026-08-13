namespace DeLong.Web.Features.PublicBooking;

public sealed record PublicRateDto(
    Guid Id,
    string Name,
    string StartTime,
    string EndTime,
    bool IsOvernight,
    decimal Price,
    bool Available);

public sealed record PublicRoomDto(
    Guid Id,
    string Code,
    string Name,
    int Capacity,
    bool HasBathtub,
    decimal FromPrice,
    IReadOnlyList<PublicRateDto> Rates);

public sealed record PublicCatalogDto(
    Guid PropertyId,
    string PropertyName,
    string TimeZoneId,
    IReadOnlyList<PublicRoomDto> Rooms);

public sealed record PublicAvailabilityDto(
    string Date,
    IReadOnlyList<PublicRoomDto> Rooms);

public sealed class PublicBookingRequest
{
    public Guid RoomId { get; init; }
    public Guid RateId { get; init; }
    public string StayDate { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string? Website { get; init; }
}

public sealed record PublicBookingResult(
    Guid BookingId,
    string Code,
    string RoomName,
    string RateName,
    DateTime CheckInUtc,
    DateTime CheckOutUtc,
    decimal TotalAmount);

public sealed record PublicBookingError(string Code, string Message);
