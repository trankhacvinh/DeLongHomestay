using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.PublicBooking;

public sealed record PublicRateDto(
    Guid Id,
    string Name,
    string StartTime,
    string EndTime,
    RoomRateType Type,
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

public sealed record PublicStayRoomDto(
    Guid Id,
    string Code,
    string Name,
    int Capacity,
    bool HasBathtub,
    PublicRateDto NightlyRate,
    int Nights,
    decimal TotalAmount,
    bool Available);

public sealed record PublicStayAvailabilityDto(
    string CheckInDate,
    string CheckOutDate,
    int Nights,
    IReadOnlyList<PublicStayRoomDto> Rooms);

public sealed class PublicBookingRequest
{
    public BookingType Type { get; init; } = BookingType.TimeSlot;
    public Guid RoomId { get; init; }
    public Guid RateId { get; init; }
    public string StayDate { get; init; } = string.Empty;
    public string CheckInDate { get; init; } = string.Empty;
    public string CheckOutDate { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public int GuestCount { get; init; } = 1;
    public bool PolicyAccepted { get; init; }
    public int PolicyVersion { get; init; }
    public bool HasIdentityFront { get; init; }
    public bool HasIdentityBack { get; init; }
    public string? Note { get; init; }
    public string? Website { get; init; }
}

public sealed record PublicBookingResult(
    Guid BookingId,
    string Code,
    BookingType Type,
    string RoomName,
    string RateName,
    int? NightCount,
    DateTime CheckInUtc,
    DateTime CheckOutUtc,
    decimal TotalAmount,
    DateTime? HoldExpiresAtUtc = null);

public sealed record PublicBookingError(string Code, string Message);
