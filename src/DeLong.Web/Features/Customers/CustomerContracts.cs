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
