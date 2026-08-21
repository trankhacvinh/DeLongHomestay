using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Customers;

public sealed class CustomerService(AppDbContext db)
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(
        Guid propertyId,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var customers = db.Customers.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            var normalizedPhone = NormalizePhone(query);
            customers = customers.Where(x =>
                x.Name.ToLower().Contains(q) ||
                x.Phone.Contains(query.Trim()) ||
                (normalizedPhone != string.Empty && x.NormalizedPhone.Contains(normalizedPhone)));
        }

        return await customers
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CustomerDto(
                x.Id, x.PropertyId, x.Name, x.Phone, x.Email, x.IdentityNumber, x.Note, x.IsActive, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetAsync(Guid propertyId, Guid customerId, CancellationToken cancellationToken = default)
    {
        return await db.Customers
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == customerId)
            .Select(x => new CustomerDto(
                x.Id, x.PropertyId, x.Name, x.Phone, x.Email, x.IdentityNumber, x.Note, x.IsActive, x.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(
        Guid propertyId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetAsync(propertyId, customerId, cancellationToken);
        if (customer is null) return null;

        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.CustomerId == customerId)
            .OrderByDescending(x => x.CheckInUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new CustomerBookingHistoryDto(
                x.Id,
                x.Code,
                x.RoomId,
                x.Room.Code,
                x.Room.Name,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.Status,
                x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
                x.Payments.Where(payment => !payment.IsVoided)
                    .Sum(payment => payment.Type == Domain.Enums.PaymentType.Receipt ? payment.Amount : -payment.Amount),
                x.RoomAmount + x.ExtraAmount - x.DiscountAmount - x.Payments.Where(payment => !payment.IsVoided)
                    .Sum(payment => payment.Type == Domain.Enums.PaymentType.Receipt ? payment.Amount : -payment.Amount),
                x.Source,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new CustomerProfileDto(customer, bookings);
    }

    public async Task<(CustomerDto? Customer, string? Error)> CreateAsync(
        Guid propertyId,
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.Name, request.Phone, request.Email);
        if (validation is not null) return (null, validation);

        var normalizedPhone = NormalizePhone(request.Phone);
        if (await db.Customers.AnyAsync(
                x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone,
                cancellationToken))
        {
            return (null, "Số điện thoại đã tồn tại trong cơ sở này.");
        }

        var customer = new Customer
        {
            PropertyId = propertyId,
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            NormalizedPhone = normalizedPhone,
            Email = Clean(request.Email),
            IdentityNumber = Clean(request.IdentityNumber),
            Note = Clean(request.Note),
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, customer.Id, cancellationToken), null);
    }

    public async Task<(CustomerDto? Customer, string? Error)> UpdateAsync(
        Guid propertyId,
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.Name, request.Phone, request.Email);
        if (validation is not null) return (null, validation);

        var customer = await db.Customers.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == customerId,
            cancellationToken);
        if (customer is null) return (null, "Không tìm thấy khách hàng.");

        var normalizedPhone = NormalizePhone(request.Phone);
        if (await db.Customers.AnyAsync(
                x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone && x.Id != customerId,
                cancellationToken))
        {
            return (null, "Số điện thoại đã tồn tại trong cơ sở này.");
        }

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.NormalizedPhone = normalizedPhone;
        customer.Email = Clean(request.Email);
        customer.IdentityNumber = Clean(request.IdentityNumber);
        customer.Note = Clean(request.Note);
        customer.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, customerId, cancellationToken), null);
    }

    public async Task<Customer?> FindOrCreateEntityAsync(
        Guid propertyId,
        Guid? customerId,
        string customerName,
        string customerPhone,
        CancellationToken cancellationToken = default)
    {
        if (customerId.HasValue)
        {
            return await db.Customers.SingleOrDefaultAsync(
                x => x.PropertyId == propertyId && x.Id == customerId.Value && x.IsActive,
                cancellationToken);
        }

        var normalizedPhone = NormalizePhone(customerPhone);
        if (normalizedPhone == string.Empty || string.IsNullOrWhiteSpace(customerName)) return null;

        var existing = await db.Customers.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone,
            cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsActive) return null;
            existing.Name = customerName.Trim();
            return existing;
        }

        var customer = new Customer
        {
            PropertyId = propertyId,
            Name = customerName.Trim(),
            Phone = customerPhone.Trim(),
            NormalizedPhone = normalizedPhone,
            IsActive = true
        };
        db.Customers.Add(customer);
        return customer;
    }

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length >= 10)
        {
            digits = $"0{digits[2..]}";
        }
        return digits;
    }

    private static string? Validate(string name, string phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Tên khách hàng là bắt buộc.";
        if (name.Trim().Length > 200) return "Tên khách hàng tối đa 200 ký tự.";
        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone.Length < 8 || normalizedPhone.Length > 20) return "Số điện thoại không hợp lệ.";
        if (!string.IsNullOrWhiteSpace(email) && email.Trim().Length > 254) return "Email quá dài.";
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
