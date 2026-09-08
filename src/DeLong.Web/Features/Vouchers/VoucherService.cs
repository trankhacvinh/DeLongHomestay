using System.Globalization;
using System.Net.Mail;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Operations;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Customers;
using DeLong.Web.Features.Notifications;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DeLong.Web.Features.Vouchers;

public sealed class VoucherService(
    AppDbContext db,
    AuditService auditService,
    StoragePaths storagePaths,
    IConfiguration configuration,
    PricingService? pricingService = null)
{
    public async Task<IReadOnlyList<VoucherDto>> GetAllAsync(
        Guid propertyId,
        VoucherStatus? status,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = db.Vouchers.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{value}%") ||
                                     (x.Description != null && EF.Functions.ILike(x.Description, $"%{value}%")) ||
                                     (x.Customer != null && (EF.Functions.ILike(x.Customer.Name, $"%{value}%") ||
                                                              EF.Functions.ILike(x.Customer.Phone, $"%{value}%"))));
        }
        if (fromUtc.HasValue) query = query.Where(x => x.EndsAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.StartsAtUtc < toUtc.Value);

        return await Project(query.OrderByDescending(x => x.CreatedAtUtc)).ToListAsync(cancellationToken);
    }

    public Task<VoucherDto?> GetAsync(Guid propertyId, Guid voucherId, CancellationToken cancellationToken = default) =>
        Project(db.Vouchers.AsNoTracking().Where(x => x.PropertyId == propertyId && x.Id == voucherId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VoucherRedemptionDto>> GetHistoryAsync(
        Guid propertyId,
        Guid voucherId,
        CancellationToken cancellationToken = default) =>
        await db.VoucherRedemptions.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.VoucherId == voucherId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new VoucherRedemptionDto(
                x.Id, x.BookingId, x.Booking.Code, x.Booking.Status, x.CustomerId, x.Customer.Name, x.Customer.Phone,
                x.VoucherCode, x.DiscountPercent, x.EligibleRoomAmount, x.DiscountAmount,
                x.BookingScope, x.Status, x.ReservedAtUtc, x.RedeemedAtUtc, x.ReleasedAtUtc,
                x.RestoredAtUtc, x.ResolutionReason))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VoucherEmailDeliveryDto>> GetEmailHistoryAsync(
        Guid propertyId,
        Guid voucherId,
        CancellationToken cancellationToken = default) =>
        await db.VoucherEmailDeliveries.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.VoucherId == voucherId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new VoucherEmailDeliveryDto(
                x.Id, x.RecipientEmail, x.CreatedAtUtc, x.SentAtUtc, x.AttemptCount, x.LastError))
            .ToListAsync(cancellationToken);

    public async Task<(VoucherDto? Voucher, VoucherOperationError? Error)> CreateAsync(
        Guid propertyId,
        SaveVoucherRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateSaveAsync(propertyId, request, null, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);

        var voucher = new Voucher
        {
            PropertyId = propertyId,
            Code = validation.Code,
            NormalizedCode = NormalizeCode(validation.Code),
            Description = Clean(request.Description),
            DiscountPercent = request.DiscountPercent,
            AppliesTo = request.AppliesTo,
            StartsAtUtc = EnsureUtc(request.StartsAtUtc),
            EndsAtUtc = EnsureUtc(request.EndsAtUtc),
            TotalUsageLimit = request.TotalUsageLimit,
            PerCustomerUsageLimit = request.PerCustomerUsageLimit,
            CustomerId = request.CustomerId,
            Status = request.Status,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        db.Vouchers.Add(voucher);
        auditService.Add(propertyId, "Voucher", voucher.Id, "Created", actorUserId, after: Snapshot(voucher));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return (null, new("voucher_code_exists", "Mã voucher đã tồn tại trong cơ sở này."));
        }
        return (await GetAsync(propertyId, voucher.Id, cancellationToken), null);
    }

    public async Task<(VoucherDto? Voucher, VoucherOperationError? Error)> UpdateAsync(
        Guid propertyId,
        Guid voucherId,
        SaveVoucherRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var voucher = await db.Vouchers.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == voucherId, cancellationToken);
        if (voucher is null) return (null, new("voucher_not_found", "Không tìm thấy voucher."));
        if (voucher.Status == VoucherStatus.Archived)
            return (null, new("voucher_archived", "Voucher đã lưu trữ nên không thể sửa."));

        var validation = await ValidateSaveAsync(propertyId, request, voucherId, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);
        var before = Snapshot(voucher);
        voucher.Code = validation.Code;
        voucher.NormalizedCode = NormalizeCode(validation.Code);
        voucher.Description = Clean(request.Description);
        voucher.DiscountPercent = request.DiscountPercent;
        voucher.AppliesTo = request.AppliesTo;
        voucher.StartsAtUtc = EnsureUtc(request.StartsAtUtc);
        voucher.EndsAtUtc = EnsureUtc(request.EndsAtUtc);
        voucher.TotalUsageLimit = request.TotalUsageLimit;
        voucher.PerCustomerUsageLimit = request.PerCustomerUsageLimit;
        voucher.CustomerId = request.CustomerId;
        voucher.Status = request.Status;
        voucher.UpdatedByUserId = actorUserId;
        auditService.Add(propertyId, "Voucher", voucher.Id, "Updated", actorUserId, before, Snapshot(voucher));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return (null, new("voucher_code_exists", "Mã voucher đã tồn tại trong cơ sở này."));
        }
        return (await GetAsync(propertyId, voucher.Id, cancellationToken), null);
    }

    public async Task<VoucherOperationError?> ArchiveAsync(
        Guid propertyId,
        Guid voucherId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var voucher = await db.Vouchers.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == voucherId, cancellationToken);
        if (voucher is null) return new("voucher_not_found", "Không tìm thấy voucher.");
        if (voucher.Status == VoucherStatus.Archived) return null;
        var before = Snapshot(voucher);
        voucher.Status = VoucherStatus.Archived;
        voucher.UpdatedByUserId = actorUserId;
        auditService.Add(propertyId, "Voucher", voucher.Id, "Archived", actorUserId, before, Snapshot(voucher));
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<(VoucherApplicationResult? Result, VoucherOperationError? Error)> ReserveForBookingAsync(
        Guid propertyId,
        Guid bookingId,
        string rawCode,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.VoucherRedemptions.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId)
            .Select(x => new VoucherApplicationResult(
                x.VoucherId, x.VoucherCode, x.DiscountPercent, x.EligibleRoomAmount,
                x.DiscountAmount, x.Booking.TotalAmount, x.BookingScope))
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null) return (existing, null);

        IDbContextTransaction? ownedTransaction = null;
        if (db.Database.CurrentTransaction is null)
            ownedTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var normalizedCode = NormalizeCode(rawCode);
            var voucher = await db.Vouchers
                .FromSqlInterpolated($"SELECT * FROM vouchers WHERE property_id = {propertyId} AND normalized_code = {normalizedCode} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (voucher is null) return await RollbackErrorAsync(ownedTransaction, "voucher_not_found", "Mã voucher không tồn tại.", cancellationToken);

            var booking = await db.Bookings
                .Include(x => x.RateSegments).ThenInclude(x => x.RoomRate)
                .Include(x => x.RoomRate)
                .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
            if (booking is null) return await RollbackErrorAsync(ownedTransaction, "booking_not_found", "Không tìm thấy booking.", cancellationToken);
            if (booking.Type == BookingType.MultiDay)
                return await RollbackErrorAsync(ownedTransaction, "voucher_booking_type_invalid", "Voucher chưa áp dụng cho đặt phòng nhiều ngày theo giá đêm.", cancellationToken);
            if (booking.DiscountAmount != 0)
                return await RollbackErrorAsync(ownedTransaction, "voucher_stacking_not_allowed", "Booking đã có khoản giảm giá khác.", cancellationToken);

            var scope = ResolveBookingScope(booking);
            var error = await ValidateVoucherAsync(voucher, booking.CustomerId, scope, DateTime.UtcNow, cancellationToken);
            if (error is not null) return await RollbackErrorAsync(ownedTransaction, error.Code, error.Message, cancellationToken);

            var discount = CalculateDiscount(booking.RoomAmount, voucher.DiscountPercent);
            booking.DiscountAmount = discount;
            var redemption = new VoucherRedemption
            {
                PropertyId = propertyId,
                Voucher = voucher,
                Booking = booking,
                CustomerId = booking.CustomerId,
                UserId = userId,
                VoucherCode = voucher.Code,
                DiscountPercent = voucher.DiscountPercent,
                EligibleRoomAmount = booking.RoomAmount,
                DiscountAmount = discount,
                BookingScope = scope,
                Status = VoucherRedemptionStatus.Reserved,
                ReservedAtUtc = DateTime.UtcNow
            };
            db.VoucherRedemptions.Add(redemption);
            await db.SaveChangesAsync(cancellationToken);
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
            return (new VoucherApplicationResult(
                voucher.Id, voucher.Code, voucher.DiscountPercent, booking.RoomAmount,
                discount, booking.TotalAmount, scope), null);
        }
        catch
        {
            if (ownedTransaction is not null) await ownedTransaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null) await ownedTransaction.DisposeAsync();
        }
    }

    public async Task MarkRedeemedAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var redemption = await db.VoucherRedemptions.SingleOrDefaultAsync(
            x => x.BookingId == bookingId, cancellationToken);
        if (redemption is null || redemption.Status != VoucherRedemptionStatus.Reserved) return;
        redemption.Status = VoucherRedemptionStatus.Redeemed;
        redemption.RedeemedAtUtc = DateTime.UtcNow;
    }

    public async Task ReleaseReservedAsync(Guid bookingId, string reason, CancellationToken cancellationToken = default)
    {
        var redemption = await db.VoucherRedemptions.SingleOrDefaultAsync(
            x => x.BookingId == bookingId, cancellationToken);
        if (redemption is null || redemption.Status != VoucherRedemptionStatus.Reserved) return;
        redemption.Status = VoucherRedemptionStatus.Released;
        redemption.ReleasedAtUtc = DateTime.UtcNow;
        redemption.ResolutionReason = Truncate(reason, 1000);
    }

    public async Task<VoucherOperationError?> RestoreAsync(
        Guid propertyId,
        Guid redemptionId,
        string reason,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return new("restore_reason_required", "Vui lòng nhập lý do hoàn lại lượt voucher.");
        var redemption = await db.VoucherRedemptions
            .Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == redemptionId, cancellationToken);
        if (redemption is null) return new("redemption_not_found", "Không tìm thấy lượt sử dụng voucher.");
        if (redemption.Status != VoucherRedemptionStatus.Redeemed)
            return new("redemption_not_redeemed", "Chỉ lượt voucher đã sử dụng mới có thể hoàn lại.");
        if (redemption.Booking.Status != BookingStatus.Cancelled)
            return new("booking_not_cancelled", "Chỉ hoàn lượt khi booking đã bị hủy.");

        redemption.Status = VoucherRedemptionStatus.ManuallyRestored;
        redemption.RestoredAtUtc = DateTime.UtcNow;
        redemption.RestoredByUserId = actorUserId;
        redemption.ResolutionReason = Truncate(reason.Trim(), 1000);
        auditService.Add(propertyId, "Voucher", redemption.VoucherId, "RedemptionRestored", actorUserId,
            after: new { redemption.Id, redemption.BookingId, redemption.ResolutionReason });
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<(PublicVoucherPreviewResult? Result, VoucherOperationError? Error)> PreviewAsync(
        Guid propertyId,
        PublicVoucherPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(request.VoucherCode);
        if (normalizedCode.Length == 0) return (null, new("voucher_code_required", "Vui lòng nhập mã voucher."));
        if (request.Slots.Count == 0 || request.Slots.Count > 60)
            return (null, new("voucher_selection_required", "Vui lòng chọn khung giờ trước khi áp dụng voucher."));

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == request.RoomId && x.IsActive && x.IsPublished)
            .Select(x => new
            {
                x.FullDayPricingEnabled,
                x.FullDayPrice,
                x.UseWeekdayFullDayPriceOnWeekend,
                x.WeekendFullDayPrice,
                Rates = x.Rates.Where(r => r.IsActive && r.Type != RoomRateType.Nightly)
                    .OrderBy(r => r.SortOrder).ThenBy(r => r.StartTime).ThenBy(r => r.Name)
                    .Select(r => new { r.Id, r.Name, r.Type, r.Price, r.UseWeekdayPriceOnWeekend, r.WeekendPrice }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (room is null || room.Rates.Count == 0)
            return (null, new("room_not_found", "Phòng hoặc khung giờ không còn mở đặt online."));

        var rateIndexes = room.Rates.Select((rate, index) => (rate.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var selected = new List<(DateOnly Date, int RateIndex, Guid RateId, string RateName, RoomRateType Type, decimal Price, bool UseWeekdayPriceOnWeekend, decimal? WeekendPrice)>();
        foreach (var slot in request.Slots)
        {
            if (!DateOnly.TryParseExact(slot.StayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                !rateIndexes.TryGetValue(slot.RateId, out var rateIndex))
                return (null, new("voucher_selection_invalid", "Một khung giờ đã chọn không còn hợp lệ."));
            var rate = room.Rates[rateIndex];
            selected.Add((date, rateIndex, rate.Id, rate.Name, rate.Type, rate.Price, rate.UseWeekdayPriceOnWeekend, rate.WeekendPrice));
        }
        if (selected.Select(x => (x.Date, x.RateId)).Distinct().Count() != selected.Count)
            return (null, new("voucher_selection_invalid", "Danh sách khung giờ bị trùng."));
        selected = selected.OrderBy(x => x.Date).ThenBy(x => x.RateIndex).ToList();

        var property = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == propertyId, cancellationToken);
        var policy = await new BookingPolicyStore(storagePaths, configuration).GetAsync(propertyId, cancellationToken);
        var selectionError = PublicSlotSelectionRules.ValidateConsecutive(
            selected.Select(x => (x.Date, x.RateIndex)).ToList(), room.Rates.Count, policy.PublicMaxConsecutiveSlotDays);
        if (selectionError is not null) return (null, new(selectionError.Code, selectionError.Message));

        decimal roomAmount;
        decimal specialSurchargeAmount;
        IReadOnlyList<string> pricingRules;
        if (pricingService is not null)
        {
            var (calculated, pricingError) = await pricingService.CalculateAsync(propertyId,
                selected.Select(x => new PricingSelectionInput(x.Date, x.RateIndex, x.RateId, x.RateName, x.Price, x.UseWeekdayPriceOnWeekend, x.WeekendPrice)).ToList(),
                room.Rates.Count, room.FullDayPricingEnabled, room.FullDayPrice,
                room.UseWeekdayFullDayPriceOnWeekend, room.WeekendFullDayPrice, cancellationToken);
            if (pricingError is not null) return (null, new(pricingError.Code, pricingError.Message));
            roomAmount = calculated!.RoomAmount;
            specialSurchargeAmount = calculated.SpecialSurchargeAmount;
            pricingRules = calculated.Segments.Select(x => x.PricingRule).ToList();
        }
        else
        {
            var legacyPricing = PublicSlotPricingCalculator.Calculate(
                selected.Select(x => new PublicSlotPricingItem(x.Date, x.RateIndex, x.Price)).ToList(),
                room.Rates.Count, room.FullDayPricingEnabled, room.FullDayPrice, policy.MultiSlotDiscountTiers);
            roomAmount = legacyPricing.Sum(x => x.AppliedAmount);
            specialSurchargeAmount = 0;
            pricingRules = legacyPricing.Select(x => x.PricingRule).ToList();
        }
        var scope = ResolveSelectionScope(selected.Select(x => x.Type).ToList(), pricingRules);
        var customerId = await ResolveCustomerIdAsync(propertyId, request.CustomerPhone, cancellationToken);
        var voucher = await db.Vouchers.AsNoTracking().SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.NormalizedCode == normalizedCode, cancellationToken);
        if (voucher is null) return (null, new("voucher_not_found", "Mã voucher không tồn tại."));
        var validation = await ValidateVoucherAsync(voucher, customerId, scope, DateTime.UtcNow, cancellationToken);
        if (validation is not null) return (null, validation);
        var discount = CalculateDiscount(roomAmount, voucher.DiscountPercent);
        return (new PublicVoucherPreviewResult(
            voucher.Code, voucher.DiscountPercent, roomAmount + specialSurchargeAmount, discount,
            roomAmount + specialSurchargeAmount - discount, scope), null);
    }

    public async Task<(VoucherEmailDeliveryDto? Delivery, VoucherOperationError? Error)> QueueEmailAsync(
        Guid propertyId,
        Guid voucherId,
        SendVoucherEmailRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var voucher = await db.Vouchers.AsNoTracking()
            .Include(x => x.Property).Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == voucherId, cancellationToken);
        if (voucher is null) return (null, new("voucher_not_found", "Không tìm thấy voucher."));
        var recipient = Clean(request.RecipientEmail) ?? Clean(voucher.Customer?.Email);
        if (recipient is null || !IsEmail(recipient))
            return (null, new("recipient_email_invalid", "Vui lòng nhập email khách hợp lệ."));

        var settings = await db.PropertyNotificationSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.SmtpFromEmail))
            return (null, new("smtp_not_configured", "Cơ sở chưa cấu hình SMTP để gửi voucher."));
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(voucher.Property.TimeZoneId);
        var data = new VoucherEmailTemplateData(
            voucher.Property.Name, voucher.Code, Clean(request.CustomerName) ?? voucher.Customer?.Name ?? "Quý khách",
            voucher.DiscountPercent,
            TimeZoneInfo.ConvertTimeFromUtc(voucher.StartsAtUtc, timeZone),
            TimeZoneInfo.ConvertTimeFromUtc(voucher.EndsAtUtc, timeZone),
            ApplicabilityText(voucher.AppliesTo));
        var subject = VoucherEmailTemplateRenderer.Render(
            settings.VoucherEmailSubjectTemplate, VoucherEmailTemplateRenderer.DefaultSubject, data, false);
        var bodyHtml = VoucherEmailTemplateRenderer.Render(
            settings.VoucherEmailBodyTemplate, VoucherEmailTemplateRenderer.DefaultBody, data, true);
        var delivery = new VoucherEmailDelivery
        {
            PropertyId = propertyId,
            VoucherId = voucherId,
            CustomerId = voucher.CustomerId,
            RequestedByUserId = actorUserId,
            RecipientEmail = recipient,
            Subject = subject,
            BodyHtml = bodyHtml,
            BodyText = NotificationEmailTemplateRenderer.ToPlainText(bodyHtml),
            NextAttemptAtUtc = DateTime.UtcNow
        };
        db.VoucherEmailDeliveries.Add(delivery);
        await db.SaveChangesAsync(cancellationToken);
        return (new VoucherEmailDeliveryDto(
            delivery.Id, delivery.RecipientEmail, delivery.CreatedAtUtc,
            delivery.SentAtUtc, delivery.AttemptCount, delivery.LastError), null);
    }

    private async Task<VoucherOperationError?> ValidateVoucherAsync(
        Voucher voucher,
        Guid customerId,
        VoucherApplicability scope,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (voucher.Status != VoucherStatus.Active)
            return new("voucher_inactive", voucher.Status == VoucherStatus.Paused ? "Voucher đang tạm dừng." : "Voucher chưa hoạt động.");
        if (nowUtc < voucher.StartsAtUtc) return new("voucher_not_started", "Voucher chưa đến thời gian sử dụng.");
        if (nowUtc >= voucher.EndsAtUtc) return new("voucher_expired", "Voucher đã hết hạn.");
        if (scope == VoucherApplicability.None || (voucher.AppliesTo & scope) != scope)
            return new("voucher_booking_type_invalid", "Voucher không áp dụng cho toàn bộ loại khung giờ đã chọn.");
        if (voucher.CustomerId.HasValue && voucher.CustomerId.Value != customerId)
            return new("voucher_customer_mismatch", "Voucher này được dành riêng cho một khách hàng khác.");

        if (voucher.TotalUsageLimit.HasValue)
        {
            var used = await db.VoucherRedemptions.AsNoTracking().CountAsync(
                x => x.VoucherId == voucher.Id &&
                     (x.Status == VoucherRedemptionStatus.Reserved || x.Status == VoucherRedemptionStatus.Redeemed),
                cancellationToken);
            if (used >= voucher.TotalUsageLimit.Value) return new("voucher_usage_exhausted", "Voucher đã hết lượt sử dụng.");
        }
        if (voucher.PerCustomerUsageLimit.HasValue)
        {
            var usedByCustomer = await db.VoucherRedemptions.AsNoTracking().CountAsync(
                x => x.VoucherId == voucher.Id && x.CustomerId == customerId &&
                     (x.Status == VoucherRedemptionStatus.Reserved || x.Status == VoucherRedemptionStatus.Redeemed),
                cancellationToken);
            if (usedByCustomer >= voucher.PerCustomerUsageLimit.Value)
                return new("voucher_customer_limit_reached", "Bạn đã sử dụng hết số lượt cho phép của voucher này.");
        }
        return null;
    }

    private async Task<(string Code, VoucherOperationError? Error)> ValidateSaveAsync(
        Guid propertyId,
        SaveVoucherRequest request,
        Guid? existingId,
        CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(request.Code) ? GenerateCode() : request.Code.Trim().ToUpperInvariant();
        if (!IsValidCode(code)) return (code, new("voucher_code_invalid", "Mã voucher phải có 4–50 ký tự chữ, số, dấu gạch ngang hoặc gạch dưới."));
        if (request.DiscountPercent is <= 0 or > 100) return (code, new("voucher_discount_invalid", "Phần trăm giảm phải từ 1 đến 100."));
        var validFlags = VoucherApplicability.TimeSlot | VoucherApplicability.Overnight | VoucherApplicability.FullDay;
        if (request.AppliesTo == VoucherApplicability.None || (request.AppliesTo & ~validFlags) != 0)
            return (code, new("voucher_scope_invalid", "Vui lòng chọn ít nhất một loại đặt phòng hợp lệ."));
        if (EnsureUtc(request.EndsAtUtc) <= EnsureUtc(request.StartsAtUtc))
            return (code, new("voucher_validity_invalid", "Thời gian kết thúc phải sau thời gian bắt đầu."));
        if (request.TotalUsageLimit is <= 0 || request.PerCustomerUsageLimit is <= 0)
            return (code, new("voucher_usage_limit_invalid", "Giới hạn lượt phải lớn hơn 0 hoặc để trống."));
        if (request.TotalUsageLimit.HasValue && request.PerCustomerUsageLimit > request.TotalUsageLimit)
            return (code, new("voucher_usage_limit_invalid", "Giới hạn mỗi khách không thể lớn hơn tổng lượt voucher."));
        if (request.Status == VoucherStatus.Archived)
            return (code, new("voucher_status_invalid", "Hãy dùng thao tác lưu trữ thay vì tạo/sửa trực tiếp trạng thái này."));
        if (request.CustomerId.HasValue && !await db.Customers.AsNoTracking().AnyAsync(
                x => x.PropertyId == propertyId && x.Id == request.CustomerId && x.IsActive, cancellationToken))
            return (code, new("voucher_customer_not_found", "Không tìm thấy khách hàng được chỉ định trong cơ sở."));
        if (await db.Vouchers.AsNoTracking().AnyAsync(x =>
                x.PropertyId == propertyId && x.NormalizedCode == NormalizeCode(code) &&
                (!existingId.HasValue || x.Id != existingId.Value), cancellationToken))
            return (code, new("voucher_code_exists", "Mã voucher đã tồn tại trong cơ sở này."));
        return (code, null);
    }

    private async Task<Guid> ResolveCustomerIdAsync(Guid propertyId, string rawPhone, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerService.NormalizePhone(rawPhone);
        if (normalizedPhone.Length < 8) return Guid.Empty;
        return await db.Customers.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone)
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static VoucherApplicability ResolveBookingScope(Booking booking)
    {
        if (booking.RateSegments.Count == 0)
            return booking.RoomRate?.Type == RoomRateType.Overnight
                ? VoucherApplicability.Overnight
                : VoucherApplicability.TimeSlot;
        return ResolveSelectionScope(
            booking.RateSegments.OrderBy(x => x.SortOrder).Select(x => x.RoomRate?.Type ?? RoomRateType.TimeSlot).ToList(),
            booking.RateSegments.OrderBy(x => x.SortOrder).Select(x => x.PricingRule).ToList());
    }

    private static VoucherApplicability ResolveSelectionScope(
        IReadOnlyList<RoomRateType> rateTypes,
        IReadOnlyList<string> pricingRules)
    {
        var scope = VoucherApplicability.None;
        for (var index = 0; index < rateTypes.Count; index++)
        {
            if (string.Equals(pricingRules[index], "full-day", StringComparison.OrdinalIgnoreCase))
                scope |= VoucherApplicability.FullDay;
            else if (rateTypes[index] == RoomRateType.Overnight)
                scope |= VoucherApplicability.Overnight;
            else
                scope |= VoucherApplicability.TimeSlot;
        }
        return scope;
    }

    private static IQueryable<VoucherDto> Project(IQueryable<Voucher> query) =>
        query.Select(x => new VoucherDto(
            x.Id, x.PropertyId, x.Code, x.Description, x.DiscountPercent, x.AppliesTo,
            x.StartsAtUtc, x.EndsAtUtc, x.TotalUsageLimit, x.PerCustomerUsageLimit,
            x.CustomerId, x.Customer == null ? null : x.Customer.Name,
            x.Customer == null ? null : x.Customer.Phone,
            x.Customer == null ? null : x.Customer.Email,
            x.Status,
            x.Redemptions.Count(r => r.Status == VoucherRedemptionStatus.Reserved),
            x.Redemptions.Count(r => r.Status == VoucherRedemptionStatus.Redeemed),
            x.TotalUsageLimit.HasValue
                ? x.TotalUsageLimit.Value - x.Redemptions.Count(r =>
                    r.Status == VoucherRedemptionStatus.Reserved || r.Status == VoucherRedemptionStatus.Redeemed) > 0
                    ? x.TotalUsageLimit.Value - x.Redemptions.Count(r =>
                        r.Status == VoucherRedemptionStatus.Reserved || r.Status == VoucherRedemptionStatus.Redeemed)
                    : 0
                : null,
            x.CreatedAtUtc, x.UpdatedAtUtc));

    private static async Task<(VoucherApplicationResult?, VoucherOperationError?)> RollbackErrorAsync(
        IDbContextTransaction? transaction,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
        return (null, new VoucherOperationError(code, message));
    }

    private static decimal CalculateDiscount(decimal roomAmount, decimal percent) =>
        Math.Min(roomAmount, decimal.Round(roomAmount * percent / 100m, 0, MidpointRounding.AwayFromZero));

    public static string NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(ch => !char.IsWhiteSpace(ch))).ToUpperInvariant();

    private static bool IsValidCode(string value) =>
        value.Length is >= 4 and <= 50 && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    private static string GenerateCode() => $"DL-{Guid.CreateVersion7().ToString("N")[..10].ToUpperInvariant()}";
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    private static bool IsEmail(string value)
    {
        try { return string.Equals(new MailAddress(value).Address, value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    public static string ApplicabilityText(VoucherApplicability value)
    {
        var items = new List<string>();
        if (value.HasFlag(VoucherApplicability.TimeSlot)) items.Add("khung giờ");
        if (value.HasFlag(VoucherApplicability.Overnight)) items.Add("qua đêm");
        if (value.HasFlag(VoucherApplicability.FullDay)) items.Add("cả ngày");
        return string.Join(", ", items);
    }

    private static object Snapshot(Voucher voucher) => new
    {
        voucher.Id, voucher.Code, voucher.Description, voucher.DiscountPercent,
        AppliesTo = voucher.AppliesTo.ToString(), voucher.StartsAtUtc, voucher.EndsAtUtc,
        voucher.TotalUsageLimit, voucher.PerCustomerUsageLimit, voucher.CustomerId,
        Status = voucher.Status.ToString()
    };
}
