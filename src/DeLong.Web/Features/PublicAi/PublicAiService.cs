using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.AdminAi;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.Site;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicAi;

public sealed record PublicAiChatRequest(string Message, string? DraftToken = null);
public sealed record PublicAiAction(string Label, string Url);
public sealed record PublicAiDraftPreview(string Room, string Date, IReadOnlyList<string> TimeSlots,
    int GuestCount, decimal RoomAmount, decimal SurchargeAmount, decimal TotalAmount, string? VoucherCode);
public sealed record PublicAiChatResponse(string Message, bool Cached, PublicAiAction? Action = null,
    PublicAiDraftPreview? Draft = null, string? DraftToken = null);

public sealed class PublicAiService(
    AppDbContext db,
    PublicPropertyResolver resolver,
    AiAccessGateway gateway,
    AiKnowledgeSnapshotService knowledge,
    AiResponseCacheService responseCache,
    AdminAiSettingsService settings,
    AiProviderClient provider,
    PublicBookingService publicBooking,
    PublicBookingLookupService bookingLookup,
    PublicAiLookupRateGuard lookupRateGuard,
    PricingService pricingService)
{
    private const string SystemPrompt = """
        Bạn là trợ lý khách hàng của cơ sở lưu trú. Chỉ trả lời nội dung liên quan đến cơ sở, phòng, tiện nghi, giá và chính sách có trong SYSTEM_DATA.
        Khi có LIVE_DATA, đây là nguồn sự thật mới nhất cho đúng ngày khách hỏi; giá trong đó đã gồm quy tắc cuối tuần và phụ thu ngày đặc biệt.
        Không tiết lộ dữ liệu nội bộ, không thực hiện hay tuyên bố đã tạo, đổi hoặc hủy booking. Không đoán dữ liệu còn thiếu.
        Nếu khách hỏi ngoài phạm vi, trả lời ngắn rằng bạn chỉ hỗ trợ thông tin lưu trú và đặt phòng.
        Nếu cần ngày, phòng hoặc số khách nhưng người dùng chưa cung cấp, hãy hỏi đúng một câu ngắn.
        Trả lời tiếng Việt ngắn gọn, không quá 180 từ.
        Nếu khách thể hiện ý định đặt phòng, draft.requested=true và trích xuất roomCode, stayDate yyyy-MM-dd, rateNames, guestCount, voucherCode.
        Nếu CURRENT_DRAFT có dữ liệu, hợp nhất câu trả lời mới vào đó và giữ nguyên các trường khách không yêu cầu đổi.
        Chỉ dùng đúng mã phòng và tên khung trong LIVE_DATA. Thiếu trường nào dùng chuỗi rỗng, mảng rỗng hoặc 0; không tự đoán.
        Khi gợi ý hoặc so sánh và kết luận một phòng cụ thể, đặt suggestedRoomCode bằng đúng mã phòng có trong SYSTEM_DATA; nếu không có một phòng cụ thể thì để chuỗi rỗng.
        Với nhận xét chủ quan như yên tĩnh, lãng mạn hoặc phù hợp một nhu cầu, chỉ kết luận khi mô tả hoặc tiện nghi trong SYSTEM_DATA hỗ trợ; nếu không thì nói chưa đủ dữ liệu.
        Nếu không phải yêu cầu đặt phòng, draft=null.
        """;

    public async Task<bool> IsEnabledAsync(string? siteSlug, CancellationToken ct)
    {
        var property = await resolver.ResolveAsync(siteSlug, ct);
        return property is not null && await db.PropertyAiProfiles.AsNoTracking().AnyAsync(x =>
            x.PropertyId == property.Id && x.IsEnabled && x.IsPublicAiEnabled && x.ProtectedApiKey != string.Empty, ct);
    }

    public async Task<(PublicAiChatResponse? Value, string? Error)> ChatAsync(string? siteSlug, PublicAiChatRequest request, string? clientAddress, CancellationToken ct)
    {
        var text = request.Message?.Trim() ?? string.Empty;
        if (text.Length is < 2 or > 1000) return (null, "Nội dung phải từ 2 đến 1.000 ký tự.");
        var property = await resolver.ResolveAsync(siteSlug, ct);
        if (property is null) return (null, "Không tìm thấy cơ sở.");
        var access = await gateway.AuthorizeAsync(property.Id, AiAudience.Customer, ct);
        if (!access.IsAllowed) return (null, access.Error);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var question = PublicAiQuestionAnalyzer.Analyze(text, today);
        var (storedDraft, storedDraftEntity) = await LoadDraftAsync(property.Id, request.DraftToken, ct);
        if (storedDraft is not null) question = question with { Intent = PublicAiIntent.BookingDraft };
        if (question.Intent == PublicAiIntent.ChangeOrCancel)
        {
            var contact = await db.Set<PropertySiteSettings>().AsNoTracking()
                .Where(x => x.PropertyId == property.Id)
                .Select(x => new { x.Phone, x.Email })
                .SingleOrDefaultAsync(ct);
            var channels = new[]
            {
                string.IsNullOrWhiteSpace(contact?.Phone) ? null : $"điện thoại {contact.Phone}",
                string.IsNullOrWhiteSpace(contact?.Email) ? null : $"email {contact.Email}"
            }.Where(x => x is not null).ToArray();
            var message = channels.Length == 0
                ? "Để đổi hoặc hủy booking, bạn cần liên hệ trực tiếp với cơ sở. Trợ lý không tự thay đổi booking."
                : $"Để đổi hoặc hủy booking, vui lòng liên hệ cơ sở qua {string.Join(" hoặc ", channels)}. Trợ lý không tự thay đổi booking.";
            await RecordLocalUsageAsync(property.Id, access.Profile!, "PublicChatContact", false, ct);
            return (new PublicAiChatResponse(message, false), null);
        }
        if (question.Intent == PublicAiIntent.BookingLookup)
        {
            var lookupUrl = PublicUrlBuilder.BookingLookup(property.SiteSlug);
            if (question.BookingCode is null || question.Phone is null)
            {
                await RecordLocalUsageAsync(property.Id, access.Profile!, "PublicChatLookupClarification", false, ct);
                return (new PublicAiChatResponse(
                    "Để bảo vệ thông tin, bạn cần cung cấp đồng thời mã booking và số điện thoại đã đặt, hoặc mở trang tra cứu bảo mật.",
                    false, new PublicAiAction("Tra cứu booking", lookupUrl)), null);
            }
            if (!lookupRateGuard.TryAcquire(property.Id, clientAddress, DateTime.UtcNow))
                return (null, "Bạn đã tra cứu quá nhiều lần. Vui lòng thử lại sau 10 phút.");
            var booking = await bookingLookup.LookupAsync(siteSlug, question.BookingCode, question.Phone, ct);
            await RecordLocalUsageAsync(property.Id, access.Profile!, "PublicChatBookingLookup", false, ct);
            if (booking is null)
                return (new PublicAiChatResponse(
                    "Không tìm thấy booking phù hợp với mã và số điện thoại đã cung cấp.", false,
                    new PublicAiAction("Mở trang tra cứu", lookupUrl)), null);
            var message = $"Booking {booking.Code}: {booking.StatusLabel}. Phòng {booking.RoomName}, nhận {FormatLocal(booking.CheckInLocal)}, trả {FormatLocal(booking.CheckOutLocal)}. Tổng tiền {booking.TotalAmount:N0}đ, đã thanh toán {booking.PaidAmount:N0}đ, còn lại {booking.Balance:N0}đ.";
            return (new PublicAiChatResponse(message, false, new PublicAiAction("Xem chi tiết booking", lookupUrl)), null);
        }
        if (question.Intent is PublicAiIntent.Availability or PublicAiIntent.Pricing && question.Date is null)
        {
            await RecordLocalUsageAsync(property.Id, access.Profile!, "PublicChatClarification", false, ct);
            return (new PublicAiChatResponse("Bạn muốn kiểm tra cho ngày nào? Vui lòng nhập ngày theo dạng dd/MM/yyyy.", false), null);
        }

        var effectiveDate = question.Date;
        if (!effectiveDate.HasValue && DateOnly.TryParse(storedDraft?.StayDate, out var storedDate)) effectiveDate = storedDate;
        PublicAvailabilityDto? liveAvailability = null;
        if (effectiveDate.HasValue)
        {
            liveAvailability = await publicBooking.GetAvailabilityAsync(siteSlug, effectiveDate.Value, ct);
            if (liveAvailability is null) return (null, "Không đọc được lịch phòng của cơ sở.");
        }

        var snapshot = await db.PropertyAiKnowledgeSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == property.Id, ct);
        if (snapshot is null || snapshot.IsDirty)
        {
            await knowledge.RebuildAsync(property.Id, ct);
            snapshot = await db.PropertyAiKnowledgeSnapshots.AsNoTracking().SingleAsync(x => x.PropertyId == property.Id, ct);
        }

        var parameters = new { question = AiResponseCacheService.NormalizeIntent(text), question.Intent, Date = effectiveDate };
        var cacheKey = AiResponseCacheService.CreateKey(property.Id, AiAudience.Customer, "public-answer", parameters, snapshot.Version);
        var cacheEligible = question.Intent != PublicAiIntent.BookingDraft && storedDraft is null;
        var cached = cacheEligible ? await responseCache.GetAsync(property.Id, AiAudience.Customer, cacheKey, ct) : null;
        if (cached is not null)
        {
            var cachedMessage = JsonSerializer.Deserialize<PublicAiChatResponse>(cached);
            if (cachedMessage is not null)
                await RecordLocalUsageAsync(property.Id, access.Profile!, "PublicChatCache", true, ct);
            return (cachedMessage is null ? null : cachedMessage with { Cached = true }, cachedMessage is null ? "Dữ liệu cache không hợp lệ." : null);
        }

        var stopwatch = Stopwatch.StartNew();
        Guid? providerReservationId = null;
        try
        {
            var input = JsonSerializer.Serialize(new
            {
                systemData = JsonSerializer.Deserialize<object>(snapshot.ContentJson),
                liveData = liveAvailability is null ? null : new
                {
                    liveAvailability.Date,
                    Rooms = liveAvailability.Rooms.Select(room => new
                    {
                        room.Code, room.Name, room.Capacity, room.FullDayPricingEnabled, room.FullDayPrice,
                        Rates = room.Rates.Select(rate => new
                        {
                            rate.Name, rate.StartTime, rate.EndTime, rate.IsOvernight, rate.Price, rate.Available
                        })
                    })
                },
                CURRENT_DRAFT = storedDraft,
                question = text
            });
            var profile = access.Profile!;
            var maxOutputTokens = Math.Min(profile.MaxOutputTokens, 800);
            var reservation = await gateway.ReserveProviderCallAsync(property.Id, AiAudience.Customer,
                AiAccessGateway.EstimateInputTokens(SystemPrompt, input), maxOutputTokens, ct);
            if (!reservation.IsAllowed) return (null, reservation.Error);
            providerReservationId = reservation.ReservationId;
            var result = await provider.GenerateStructuredAsync(profile.Provider, settings.ReadKey(profile), profile.Model, SystemPrompt, input,
                maxOutputTokens, "public_ai_response", PublicAiResponseProtocol.Schema, ct);
            var envelope = PublicAiResponseProtocol.Parse(result.Text);
            if (envelope is null)
            {
                await gateway.CompleteProviderCallAsync(reservation.ReservationId!.Value, new AiUsageRecord
                {
                    PropertyId = property.Id, Audience = AiAudience.Customer, Provider = profile.Provider,
                    Model = profile.Model, Operation = "PublicChat", InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens,
                    CachedInputTokens = result.CachedInputTokens,
                    EstimatedCostUsd = AiAccessGateway.EstimateCost(result.InputTokens, result.OutputTokens, profile),
                    DurationMs = stopwatch.ElapsedMilliseconds, IsSuccess = false, ErrorCode = "invalid_response"
                }, ct);
                providerReservationId = null;
                return (null, "AI chưa tạo được câu trả lời an toàn. Vui lòng thử lại.");
            }
            var mergedDraft = MergeDraft(storedDraft, envelope.Draft);
            var draftToken = request.DraftToken;
            if (mergedDraft is { Requested: true })
                draftToken = await SaveDraftAsync(property.Id, storedDraftEntity, draftToken, mergedDraft, ct);
            var (draft, draftAction, draftError) = await BuildDraftPreviewAsync(property, liveAvailability, mergedDraft, ct);
            var message = draftError ?? envelope.Message.Trim();
            var roomAction = draftAction is null
                ? await BuildSuggestedRoomActionAsync(property, envelope.SuggestedRoomCode, ct)
                : null;
            var value = new PublicAiChatResponse(message, false, draftAction ?? roomAction, draft, draftToken);
            await gateway.CompleteProviderCallAsync(reservation.ReservationId!.Value, new AiUsageRecord { PropertyId = property.Id, Audience = AiAudience.Customer, Provider = profile.Provider,
                Model = profile.Model, Operation = "PublicChat", InputTokens = result.InputTokens, OutputTokens = result.OutputTokens,
                CachedInputTokens = result.CachedInputTokens,
                EstimatedCostUsd = AiAccessGateway.EstimateCost(result.InputTokens, result.OutputTokens, profile),
                DurationMs = stopwatch.ElapsedMilliseconds, IsSuccess = true }, ct);
            providerReservationId = null;
            var liveAnswer = question.Intent is PublicAiIntent.Availability or PublicAiIntent.Pricing;
            if (cacheEligible)
                await responseCache.SetAsync(property.Id, AiAudience.Customer, "public-answer", parameters, JsonSerializer.Serialize(value),
                    liveAnswer ? "availability" : "knowledge", snapshot.Version,
                    liveAnswer ? TimeSpan.FromSeconds(30) : TimeSpan.FromHours(6), ct);
            return (value, null);
        }
        catch (Exception ex) when (ex is AiProviderException or HttpRequestException or JsonException or TaskCanceledException)
        {
            if (providerReservationId.HasValue)
            {
                var profile = access.Profile!;
                await gateway.CompleteProviderCallAsync(providerReservationId.Value, new AiUsageRecord
                {
                    PropertyId = property.Id, Audience = AiAudience.Customer, Provider = profile.Provider,
                    Model = profile.Model, Operation = "PublicChat", DurationMs = stopwatch.ElapsedMilliseconds,
                    IsSuccess = false, ErrorCode = ex is AiProviderException providerError ? providerError.Code : "provider_unavailable"
                }, CancellationToken.None);
            }
            ct.ThrowIfCancellationRequested();
            return (null, "Kết nối trợ lý đang gián đoạn. Vui lòng thử lại sau.");
        }
    }

    private async Task RecordLocalUsageAsync(Guid propertyId, PropertyAiProfile profile, string operation, bool cacheHit, CancellationToken ct)
    {
        db.AiUsageRecords.Add(new AiUsageRecord
        {
            PropertyId = propertyId,
            Audience = AiAudience.Customer,
            Provider = profile.Provider,
            Model = profile.Model,
            Operation = operation,
            IsCacheHit = cacheHit,
            IsSuccess = true
        });
        await db.SaveChangesAsync(ct);
    }

    private static string FormatLocal(string value) =>
        DateTime.TryParse(value, out var date) ? date.ToString("dd/MM/yyyy HH:mm") : value;

    private async Task<(PublicAiDraftExtraction? State, AiBookingDraft? Entity)> LoadDraftAsync(
        Guid propertyId,
        string? token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 32 or > 200) return (null, null);
        var hash = HashToken(token);
        var entity = await db.AiBookingDrafts.SingleOrDefaultAsync(x =>
            x.PropertyId == propertyId && x.TokenHash == hash && x.ExpiresAtUtc > DateTime.UtcNow, ct);
        if (entity is null) return (null, null);
        try { return (JsonSerializer.Deserialize<PublicAiDraftExtraction>(entity.StateJson), entity); }
        catch (JsonException) { return (null, null); }
    }

    private async Task<string> SaveDraftAsync(Guid propertyId, AiBookingDraft? entity, string? token,
        PublicAiDraftExtraction state, CancellationToken ct)
    {
        if (entity is null)
        {
            await db.AiBookingDrafts.Where(x => x.PropertyId == propertyId && x.ExpiresAtUtc <= DateTime.UtcNow)
                .ExecuteDeleteAsync(ct);
            token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            entity = new AiBookingDraft { PropertyId = propertyId, TokenHash = HashToken(token) };
            db.AiBookingDrafts.Add(entity);
        }
        entity.StateJson = JsonSerializer.Serialize(state);
        entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return token!;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static PublicAiDraftExtraction? MergeDraft(PublicAiDraftExtraction? current, PublicAiDraftExtraction? incoming)
    {
        if (incoming is not { Requested: true }) return current;
        if (current is null) return incoming;
        return incoming with
        {
            RoomCode = string.IsNullOrWhiteSpace(incoming.RoomCode) ? current.RoomCode : incoming.RoomCode,
            StayDate = string.IsNullOrWhiteSpace(incoming.StayDate) ? current.StayDate : incoming.StayDate,
            RateNames = incoming.RateNames.Count == 0 ? current.RateNames : incoming.RateNames,
            GuestCount = incoming.GuestCount <= 0 ? current.GuestCount : incoming.GuestCount,
            VoucherCode = string.IsNullOrWhiteSpace(incoming.VoucherCode) ? current.VoucherCode : incoming.VoucherCode
        };
    }

    private async Task<PublicAiAction?> BuildSuggestedRoomActionAsync(
        PublicPropertyContext property,
        string? suggestedRoomCode,
        CancellationToken ct)
    {
        var normalizedCode = suggestedRoomCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode)) return null;

        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.IsActive && x.IsPublished && x.Code.ToUpper() == normalizedCode)
            .Select(x => new { x.Code, x.Slug })
            .SingleOrDefaultAsync(ct);
        if (room is null) return null;

        var roomSlug = string.IsNullOrWhiteSpace(room.Slug) ? room.Code : room.Slug;
        return new PublicAiAction("Xem và chọn phòng", PublicUrlBuilder.Room(property.SiteSlug, roomSlug));
    }

    private async Task<(PublicAiDraftPreview? Draft, PublicAiAction? Action, string? Error)> BuildDraftPreviewAsync(
        PublicPropertyContext property,
        PublicAvailabilityDto? availability,
        PublicAiDraftExtraction? extraction,
        CancellationToken ct)
    {
        if (extraction is not { Requested: true }) return (null, null, null);
        if (!DateOnly.TryParse(extraction.StayDate, out var date))
            return (null, null, "Bạn muốn đặt vào ngày nào? Vui lòng cho biết ngày theo dạng dd/MM/yyyy.");
        availability ??= await publicBooking.GetAvailabilityAsync(property.SiteSlug, date, ct);
        if (availability is null) return (null, null, "Chưa đọc được lịch phòng cho ngày bạn chọn.");

        var room = availability.Rooms.SingleOrDefault(x =>
            string.Equals(x.Code, extraction.RoomCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Name, extraction.RoomCode, StringComparison.OrdinalIgnoreCase));
        if (room is null) return (null, null, "Tôi chưa xác định được phòng. Vui lòng cho biết đúng tên phòng bạn muốn đặt.");
        if (extraction.GuestCount < 1) return (null, null, "Bạn dự định có bao nhiêu khách?");
        if (extraction.GuestCount > room.Capacity) return (null, null, $"Phòng {room.Name} tối đa {room.Capacity} khách. Vui lòng chọn phòng khác hoặc giảm số khách.");
        if (extraction.RateNames.Count == 0) return (null, null, "Bạn muốn đặt khung giờ nào?");

        var pricingRoom = await db.Rooms.AsNoTracking().Where(x => x.PropertyId == property.Id && x.Id == room.Id)
            .Select(x => new
            {
                x.FullDayPricingEnabled, x.FullDayPrice, x.UseWeekdayFullDayPriceOnWeekend, x.WeekendFullDayPrice,
                Rates = x.Rates.Where(rate => rate.IsActive && rate.Type != RoomRateType.Nightly)
                    .OrderBy(rate => rate.SortOrder).ThenBy(rate => rate.StartTime).ThenBy(rate => rate.Name)
                    .Select(rate => new { rate.Id, rate.Name, rate.Price, rate.UseWeekdayPriceOnWeekend, rate.WeekendPrice }).ToList()
            }).SingleOrDefaultAsync(ct);
        if (pricingRoom is null || pricingRoom.Rates.Count == 0) return (null, null, "Phòng này chưa có khung giờ mở đặt.");

        var selected = new List<(int Index, Guid Id, string Name)>();
        foreach (var requestedName in extraction.RateNames)
        {
            var matches = pricingRoom.Rates.Select((rate, index) => (Rate: rate, Index: index))
                .Where(x => string.Equals(x.Rate.Name, requestedName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) return (null, null, $"Không xác định được khung “{requestedName}”. Vui lòng chọn theo tên khung đang hiển thị.");
            selected.Add((matches[0].Index, matches[0].Rate.Id, matches[0].Rate.Name));
        }
        selected = selected.DistinctBy(x => x.Id).OrderBy(x => x.Index).ToList();
        if (PublicSlotSelectionRules.ValidateConsecutive(selected.Select(x => (date, x.Index)).ToList(), pricingRoom.Rates.Count, 1) is { } selectionError)
            return (null, null, selectionError.Message);
        if (selected.Any(x => room.Rates.Single(rate => rate.Id == x.Id).Available == false))
            return (null, null, "Một khung giờ vừa hết chỗ. Vui lòng chọn khung khác.");

        var inputs = selected.Select(x =>
        {
            var rate = pricingRoom.Rates[x.Index];
            return new PricingSelectionInput(date, x.Index, rate.Id, rate.Name, rate.Price,
                rate.UseWeekdayPriceOnWeekend, rate.WeekendPrice);
        }).ToList();
        var (quote, quoteError) = await pricingService.CalculateAsync(property.Id, inputs, pricingRoom.Rates.Count,
            pricingRoom.FullDayPricingEnabled, pricingRoom.FullDayPrice, pricingRoom.UseWeekdayFullDayPriceOnWeekend,
            pricingRoom.WeekendFullDayPrice, ct);
        if (quoteError is not null) return (null, null, quoteError.Message);

        var slots = selected.Select(x => room.Rates.Single(rate => rate.Id == x.Id))
            .Select(x => $"{x.Name} ({x.StartTime}–{x.EndTime})").ToList();
        var slotQuery = string.Join(',', selected.Select(x => $"{date:yyyy-MM-dd}:{x.Id}"));
        var voucherCode = string.IsNullOrWhiteSpace(extraction.VoucherCode) ? null : extraction.VoucherCode.Trim().ToUpperInvariant();
        var voucherQuery = voucherCode is null ? string.Empty : $"&voucher={Uri.EscapeDataString(voucherCode)}";
        var url = $"{PublicUrlBuilder.Booking(property.SiteSlug)}?date={date:yyyy-MM-dd}&room={Uri.EscapeDataString(room.Code)}&slots={Uri.EscapeDataString(slotQuery)}{voucherQuery}";
        var preview = new PublicAiDraftPreview(room.Name, date.ToString("dd/MM/yyyy"), slots, extraction.GuestCount,
            quote!.RoomAmount, quote.SpecialSurchargeAmount, quote.TotalBeforeVoucher, voucherCode);
        return (preview, new PublicAiAction("Tiếp tục đặt phòng", url), null);
    }
}
