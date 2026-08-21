namespace DeLong.Web.Features.Customers;

public sealed record CustomerDto(
    Guid Id,
    Guid PropertyId,
    string Name,
    string Phone,
    string? Email,
    string? IdentityNumber,
    string? Note,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record CustomerBookingHistoryDto(
    Guid Id,
    string Code,
    Guid RoomId,
    string RoomCode,
    string RoomName,
    DateTime CheckInUtc,
    DateTime CheckOutUtc,
    Domain.Enums.BookingStatus Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string? Source,
    DateTime CreatedAtUtc);

public sealed record CustomerProfileDto(
    CustomerDto Customer,
    IReadOnlyList<CustomerBookingHistoryDto> Bookings,
    bool HasIdentityDocuments = false,
    int IdentityDocumentBookingCount = 0);

public sealed record CreateCustomerRequest(
    string Name,
    string Phone,
    string? Email,
    string? IdentityNumber,
    string? Note);

public sealed record UpdateCustomerRequest(
    string Name,
    string Phone,
    string? Email,
    string? IdentityNumber,
    string? Note,
    bool IsActive);
