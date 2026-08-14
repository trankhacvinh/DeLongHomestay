using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Bookings;

public sealed record BookingDto(
    Guid Id,
    Guid PropertyId,
    string Code,
    BookingType Type,
    Guid RoomId,
    string RoomCode,
    string RoomName,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid? RoomRateId,
    string? RateName,
    decimal? UnitPrice,
    int? NightCount,
    DateTime CheckInUtc,
    DateTime CheckOutUtc,
    BookingStatus Status,
    decimal RoomAmount,
    decimal ExtraAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string? Source,
    string? Note,
    DateTime CreatedAtUtc);

public sealed class CreateBookingRequest
{
    public Guid RoomId { get; init; }
    public Guid? CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public BookingType Type { get; init; } = BookingType.TimeSlot;
    public Guid? RoomRateId { get; init; }
    public string? RateName { get; init; }
    public decimal? UnitPrice { get; init; }
    public int? NightCount { get; init; }
    public DateTimeOffset CheckIn { get; init; }
    public DateTimeOffset CheckOut { get; init; }
    public BookingStatus Status { get; init; } = BookingStatus.Held;
    public decimal RoomAmount { get; init; }
    public decimal ExtraAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public string? Source { get; init; }
    public string? Note { get; init; }
}

public sealed class UpdateBookingRequest
{
    public Guid RoomId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public BookingType Type { get; init; } = BookingType.TimeSlot;
    public Guid? RoomRateId { get; init; }
    public string? RateName { get; init; }
    public decimal? UnitPrice { get; init; }
    public int? NightCount { get; init; }
    public DateTimeOffset CheckIn { get; init; }
    public DateTimeOffset CheckOut { get; init; }
    public decimal RoomAmount { get; init; }
    public decimal ExtraAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public string? Source { get; init; }
    public string? Note { get; init; }
}

public sealed record ChangeBookingStatusRequest(BookingStatus Status);
public sealed record BookingOperationError(string Code, string Message);
