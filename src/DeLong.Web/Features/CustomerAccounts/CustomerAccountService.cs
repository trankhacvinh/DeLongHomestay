using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.CustomerAccounts;

public sealed class CustomerAccountService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
{
    public const string CustomerRole = "Customer";

    public async Task<(ApplicationUser? User, string? Error)> RegisterAsync(
        Guid propertyId,
        RegisterCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.CustomerAccountSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken)
            ?? new CustomerAccountSettings { PropertyId = propertyId };
        if (!settings.RegistrationEnabled) return (null, "Cơ sở hiện không nhận đăng ký tài khoản khách.");
        var phone = CustomerService.NormalizePhone(request.Phone);
        if (phone.Length is < 8 or > 20) return (null, "Số điện thoại không hợp lệ.");
        var customer = await db.Customers.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.NormalizedPhone == phone,
            cancellationToken);
        var name = string.IsNullOrWhiteSpace(request.Name) ? customer?.Name?.Trim() : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return (null, "Tên khách không hợp lệ.");
        if (request.Password.Length < 8) return (null, "Mật khẩu phải có ít nhất 8 ký tự.");
        if (!request.TermsAccepted || request.TermsVersion != settings.TermsVersion)
            return (null, "Bạn cần đọc và đồng ý phiên bản điều khoản hiện tại.");
        if (await userManager.FindByNameAsync(phone) is not null) return (null, "Không thể tạo tài khoản với thông tin đã nhập.");

        var email = string.IsNullOrWhiteSpace(request.Email) ? customer?.Email?.Trim() : request.Email.Trim();
        var identityEmail = email;
        if (identityEmail is not null && await userManager.FindByEmailAsync(identityEmail) is not null)
        {
            // Customer accounts authenticate by phone. A legacy/admin Identity user may already own
            // the contact email stored on the customer profile, so do not let that block activation.
            identityEmail = null;
        }
        if (!await roleManager.RoleExistsAsync(CustomerRole))
            await roleManager.CreateAsync(new IdentityRole<Guid>(CustomerRole));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = phone,
            PhoneNumber = phone,
            PhoneNumberConfirmed = true,
            Email = identityEmail,
            EmailConfirmed = identityEmail is not null,
            DisplayName = name,
            IsActive = true,
            IsCustomerAccount = true,
            LockoutEnabled = true
        };
        var create = await userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded) return (null, string.Join(" ", create.Errors.Select(x => x.Description)));
        var role = await userManager.AddToRoleAsync(user, CustomerRole);
        if (!role.Succeeded) return (null, string.Join(" ", role.Errors.Select(x => x.Description)));

        if (customer is null)
        {
            customer = new Customer
            {
                PropertyId = propertyId,
                Name = name,
                Phone = request.Phone.Trim(),
                NormalizedPhone = phone,
                Email = email,
                IsActive = true
            };
            db.Customers.Add(customer);
        }
        else
        {
            customer.Name = name;
            if (email is not null) customer.Email = email;
        }

        db.CustomerAccountLinks.Add(new CustomerAccountLink
        {
            UserId = user.Id,
            PropertyId = propertyId,
            CustomerId = customer.Id
        });
        db.CustomerAccountTermsAcceptances.Add(new CustomerAccountTermsAcceptance
        {
            UserId = user.Id,
            PropertyId = propertyId,
            TermsVersion = settings.TermsVersion
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (user, null);
    }

    public async Task<ApplicationUser?> FindLoginUserAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var value = identifier.Trim();
        ApplicationUser? user;
        if (value.Contains('@', StringComparison.Ordinal))
        {
            user = await userManager.FindByEmailAsync(value);
        }
        else
        {
            var normalizedPhone = CustomerService.NormalizePhone(value);
            user = await userManager.FindByNameAsync(normalizedPhone);
            user ??= await db.Users.AsNoTracking().SingleOrDefaultAsync(
                x => x.IsCustomerAccount && x.IsActive && x.PhoneNumber == normalizedPhone,
                cancellationToken);
            user ??= await db.CustomerAccountLinks.AsNoTracking()
                .Where(x => x.Customer.NormalizedPhone == normalizedPhone && x.User.IsCustomerAccount && x.User.IsActive)
                .Select(x => x.User)
                .FirstOrDefaultAsync(cancellationToken);
        }
        return user is { IsActive: true, IsCustomerAccount: true } ? user : null;
    }

    public async Task<bool> CustomerAccountExistsAsync(Guid propertyId, string phone, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = CustomerService.NormalizePhone(phone);
        if (normalizedPhone.Length < 8) return false;
        var normalizedUserName = normalizedPhone.ToUpperInvariant();
        return await db.Users.AsNoTracking().AnyAsync(
                   x => x.IsCustomerAccount && x.IsActive &&
                       (x.NormalizedUserName == normalizedUserName || x.PhoneNumber == normalizedPhone),
                   cancellationToken)
               || await db.CustomerAccountLinks.AsNoTracking().AnyAsync(
                   x => x.PropertyId == propertyId && x.Customer.NormalizedPhone == normalizedPhone &&
                       x.User.IsCustomerAccount && x.User.IsActive,
                   cancellationToken);
    }

    public async Task<bool> CustomerProfileExistsAsync(Guid propertyId, string phone, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = CustomerService.NormalizePhone(phone);
        return normalizedPhone.Length >= 8 && await db.Customers.AsNoTracking().AnyAsync(
            x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone,
            cancellationToken);
    }

    public async Task LinkBookingCustomerAsync(Guid userId, Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        if (await HasPropertyLinkAsync(userId, propertyId, cancellationToken)) return;
        var customerId = await db.Bookings.AsNoTracking()
            .Where(x => x.Id == bookingId && x.PropertyId == propertyId)
            .Select(x => (Guid?)x.CustomerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!customerId.HasValue || await db.CustomerAccountLinks.AnyAsync(x => x.CustomerId == customerId.Value, cancellationToken)) return;
        db.CustomerAccountLinks.Add(new CustomerAccountLink { UserId = userId, PropertyId = propertyId, CustomerId = customerId.Value });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerAccountProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsCustomerAccount, cancellationToken);
        if (user is null) return null;
        var customerIds = await db.CustomerAccountLinks.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.CustomerId).ToListAsync(cancellationToken);
        var bookings = await db.Bookings.AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerId))
            .OrderByDescending(x => x.CheckInUtc)
            .Select(x => new CustomerAccountBookingDto(
                x.Id, x.Code, x.Property.Name, x.Room.Name, x.CheckInUtc, x.CheckOutUtc, x.Status.ToString(),
                x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
                db.LoyaltyLedgerEntries.Where(entry => entry.BookingId == x.Id).Sum(entry => entry.Points)))
            .ToListAsync(cancellationToken);
        var loyalty = await db.LoyaltyLedgerEntries.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new LoyaltyEntryDto(x.Id, x.Points, x.Reason, x.Booking != null ? x.Booking.Code : null, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var customer = await db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id)).OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return new CustomerAccountProfileDto(
            user.Id, user.DisplayName, user.PhoneNumber ?? user.UserName ?? string.Empty, user.Email,
            false, user.TwoFactorEnabled, loyalty.Sum(x => x.Points), bookings, loyalty);
    }

    public Task<bool> HasPropertyLinkAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken = default) =>
        db.CustomerAccountLinks.AsNoTracking().AnyAsync(x => x.UserId == userId && x.PropertyId == propertyId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetLinkedPropertyIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.CustomerAccountLinks.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PropertyId)
            .ToListAsync(cancellationToken);

    public async Task CopySavedIdentityDocumentsToBookingAsync(
        Guid userId,
        Guid propertyId,
        Guid bookingId,
        IdentityDocumentStorage storage,
        CancellationToken cancellationToken = default)
    {
        if (!await HasPropertyLinkAsync(userId, propertyId, cancellationToken)) return;
        foreach (var side in new[] { "front", "back" })
        {
            var saved = await storage.ReadAsync(propertyId, userId, side, cancellationToken);
            if (saved is null) continue;
            await using var stream = new MemoryStream(saved.Bytes, writable: false);
            IFormFile file = new FormFile(stream, 0, saved.Bytes.Length, "file", saved.OriginalFileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = saved.ContentType
            };
            await storage.SaveAsync(propertyId, bookingId, side, file, cancellationToken);
        }
    }
}
