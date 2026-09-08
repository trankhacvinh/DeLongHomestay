using System.Text.Json;
using System.Text.Json.Nodes;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Rooms;
using DeLong.Web.Features.Vouchers;
using DeLong.Web.Features.Pricing;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.AdminAi;

public sealed partial class AdminAiService
{
    private sealed record PreparedChange(string Kind, Guid Id, Guid RoomId, string Target, JsonElement Before, JsonElement After);
    private sealed record PreparedPayload(IReadOnlyList<PreparedChange> PreparedChanges);
    private sealed record ConfigurationInput(bool AllRooms, string[]? RoomReferences, bool AllRates, string? RateReference,
        RoomRateType? RateType, string? Reference, JsonElement Changes);

    private async Task<(AiOperationEnvelope? Value, string? Error)> PrepareOperationAsync(Guid propertyId, AiOperationEnvelope operation, CancellationToken ct)
    {
        try
        {
            if (operation.Payload.TryGetProperty("preparedChanges", out _))
                return (null, "preparedChanges chỉ được tạo bởi server; hãy gửi selector và changes.");
            if (operation.Summary.Length > 1000) return (null, "Tóm tắt preview tối đa 1.000 ký tự.");
            if (operation.Type == AiProposalType.UpdateRoomRate)
            {
                var legacy = operation.Payload.Deserialize<UpdateRoomRateProposal>(Json);
                if (legacy is null || string.IsNullOrWhiteSpace(legacy.RoomReference) || string.IsNullOrWhiteSpace(legacy.RateReference))
                    return (null, "Cần xác định đúng phòng và khung giá.");
                var selector = new JsonObject
                {
                    ["roomReferences"] = new JsonArray(legacy.RoomReference),
                    ["changes"] = new JsonObject { ["price"] = legacy.Price }
                };
                if (legacy.RateReference.Equals("qua đêm", StringComparison.OrdinalIgnoreCase)) selector["rateType"] = "Overnight";
                else selector["rateReference"] = legacy.RateReference;
                return await PrepareOperationAsync(propertyId, operation with { Type = AiProposalType.ConfigureRoomRates,
                    Payload = JsonSerializer.SerializeToElement(selector, Json) }, ct);
            }
            if (operation.Type == AiProposalType.Batch)
            {
                var batch = operation.Payload.Deserialize<BatchProposal>(Json);
                if (batch?.Operations is null || batch.Operations.Count is < 2 or > 10)
                    return (null, "Batch phải có 2–10 thao tác; dùng allRooms khi cập nhật nhiều phòng.");
                var prepared = new List<BatchOperation>();
                var targets = new HashSet<string>();
                foreach (var item in batch.Operations)
                {
                    if (item.Type == AiProposalType.Batch) return (null, "Không cho phép Batch lồng nhau.");
                    var result = await PrepareOperationAsync(propertyId, new(item.Type, operation.Summary, item.Payload), ct);
                    if (result.Error is not null) return result;
                    if (result.Value!.Payload.TryGetProperty("preparedChanges", out _))
                        foreach (var change in result.Value.Payload.Deserialize<PreparedPayload>(Json)!.PreparedChanges)
                            if (!targets.Add($"{change.Kind}:{change.Id}:{change.RoomId}"))
                                return (null, "Hãy gộp các thay đổi cùng đối tượng vào một thao tác.");
                    prepared.Add(new(result.Value.Type, result.Value.Payload));
                }
                return (operation with { Payload = JsonSerializer.SerializeToElement(new BatchProposal(prepared), Json) }, null);
            }
            if (operation.Type is not (AiProposalType.ConfigureRoomRates or AiProposalType.UpdateRoom or
                AiProposalType.CreateRoomRate or AiProposalType.UpdateVoucher or AiProposalType.UpdateSpecialPricingDay or
                AiProposalType.UpdatePricingSettings))
                return (operation, ValidateProposal(operation));

            var input = operation.Payload.Deserialize<ConfigurationInput>(Json)
                ?? throw new InvalidOperationException("Thiếu thông tin cấu hình.");
            // Accept existing saved integrations that used a flat pricing-settings payload.
            if (operation.Type == AiProposalType.UpdatePricingSettings && input.Changes.ValueKind == JsonValueKind.Undefined)
                input = input with { Changes = operation.Payload };
            var changes = new List<PreparedChange>();
            if (operation.Type == AiProposalType.UpdatePricingSettings)
            {
                var settings = await pricingService.GetSettingsAsync(propertyId, ct);
                var before = JsonSerializer.SerializeToElement(new SavePricingSettingsRequest(settings.ThreeSlotDiscountEnabled,
                    settings.ThreeSlotCount, settings.ThreeSlotDiscountPercent, settings.WeekendDayMask), Json);
                changes.Add(BuildChange("Pricing", propertyId, Guid.Empty, "Quy tắc giá của cơ sở", before, input.Changes,
                    ["threeSlotDiscountEnabled", "threeSlotCount", "threeSlotDiscountPercent", "weekendDayMask"]));
            }
            else if (operation.Type == AiProposalType.UpdateVoucher)
            {
                var vouchers = await db.Vouchers.AsNoTracking().Where(x => x.PropertyId == propertyId && x.Status != VoucherStatus.Archived).ToListAsync(ct);
                var voucher = SingleReference(vouchers, input.Reference, x => x.Id, x => x.Code);
                changes.Add(BuildChange("Voucher", voucher.Id, Guid.Empty, $"Voucher {voucher.Code}", VoucherSnapshot(voucher), input.Changes,
                    ["discountPercent", "appliesTo", "startsAtUtc", "endsAtUtc", "totalUsageLimit", "perCustomerUsageLimit", "status"]));
            }
            else if (operation.Type == AiProposalType.UpdateSpecialPricingDay)
            {
                var days = await db.SpecialPricingDays.AsNoTracking().Where(x => x.PropertyId == propertyId && !x.IsArchived).ToListAsync(ct);
                var day = SingleReference(days, input.Reference, x => x.Id, x => x.Name);
                changes.Add(BuildChange("SpecialDay", day.Id, Guid.Empty, day.Name, DaySnapshot(day), input.Changes,
                    ["startDate", "endDate", "category", "basePriceProfile", "surchargePercent", "bookingMode", "allowThreeSlotCombo", "isActive"]));
            }
            else
            {
                var rooms = await db.Rooms.AsNoTracking().Include(x => x.Rates)
                    .Where(x => x.PropertyId == propertyId && x.IsActive).OrderBy(x => x.Code).ToListAsync(ct);
                if (input.AllRooms && input.RoomReferences is { Length: > 0 })
                    throw new InvalidOperationException("Chọn allRooms hoặc danh sách phòng, không dùng đồng thời.");
                var selected = input.AllRooms ? rooms : (input.RoomReferences ?? [])
                    .Select(reference => SingleReference(rooms, reference, x => x.Id, x => x.Code, x => x.Name)).DistinctBy(x => x.Id).ToList();
                if (selected.Count == 0) throw new InvalidOperationException("Chưa xác định phòng cần cấu hình.");
                foreach (var room in selected)
                {
                    if (operation.Type == AiProposalType.UpdateRoom)
                        changes.Add(BuildChange("Room", room.Id, room.Id, room.Name, RoomSnapshot(room), input.Changes,
                            ["capacity", "sortOrder", "fullDayPricingEnabled", "fullDayPrice", "useWeekdayFullDayPriceOnWeekend", "weekendFullDayPrice"]));
                    else if (operation.Type == AiProposalType.CreateRoomRate)
                    {
                        var before = JsonSerializer.SerializeToElement(new CreateRoomRateRequest(), Json);
                        var change = BuildChange("CreateRate", Guid.Empty, room.Id, room.Name, before, input.Changes,
                            ["name", "startTime", "endTime", "type", "price", "useWeekdayPriceOnWeekend", "weekendPrice", "sortOrder"]);
                        changes.Add(change);
                    }
                    else
                    {
                        var rates = room.Rates.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToList();
                        if ((input.AllRates ? 1 : 0) + (input.RateType.HasValue ? 1 : 0) + (string.IsNullOrWhiteSpace(input.RateReference) ? 0 : 1) != 1)
                            throw new InvalidOperationException("Cần chọn đúng một phạm vi khung giá: allRates, rateType hoặc rateReference.");
                        var selectedRates = input.AllRates ? rates : input.RateType.HasValue
                            ? rates.Where(x => x.Type == input.RateType).ToList()
                            : [SingleReference(rates, input.RateReference, x => x.Id, x => x.Name)];
                        if (selectedRates.Count == 0) throw new InvalidOperationException($"Phòng {room.Name} không có khung giá phù hợp.");
                        foreach (var rate in selectedRates)
                            changes.Add(BuildChange("Rate", rate.Id, room.Id, $"{room.Name} · {rate.Name}", RateSnapshot(rate), input.Changes,
                                ["price", "weekendPrice", "useWeekdayPriceOnWeekend", "startTime", "endTime", "sortOrder"]));
                    }
                }
            }
            if (changes.Count > 500) throw new InvalidOperationException("Một preview hỗ trợ tối đa 500 đối tượng.");
            foreach (var change in changes) ValidateConfiguration(change);
            return (operation with { Payload = JsonSerializer.SerializeToElement(new PreparedPayload(changes), Json) }, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return (null, ex is JsonException ? "Các trường cấu hình không đúng kiểu dữ liệu." : ex.Message);
        }
    }

    private static T SingleReference<T>(IEnumerable<T> values, string? reference, Func<T, Guid> id, params Func<T, string>[] names)
    {
        var text = reference?.Trim() ?? "";
        if (text.Length == 0) throw new InvalidOperationException("Thiếu mã hoặc tên đối tượng.");
        var matches = values.Where(x => id(x).ToString().Equals(text, StringComparison.OrdinalIgnoreCase) ||
            names.Any(name => name(x).Equals(text, StringComparison.OrdinalIgnoreCase))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(matches.Length == 0
            ? $"Không tìm thấy '{text}' trong cơ sở này." : $"Có nhiều đối tượng tên '{text}'; cần dùng mã hoặc ID.");
    }

    private static PreparedChange BuildChange(string kind, Guid id, Guid roomId, string target, JsonElement before,
        JsonElement patch, string[] allowed)
    {
        if (patch.ValueKind != JsonValueKind.Object || !patch.EnumerateObject().Any())
            throw new InvalidOperationException("Chưa có trường cấu hình cần thay đổi.");
        var after = JsonNode.Parse(before.GetRawText())!.AsObject();
        foreach (var field in patch.EnumerateObject())
        {
            if (!allowed.Contains(field.Name, StringComparer.Ordinal))
                throw new InvalidOperationException($"Không cho phép AI sửa trường '{field.Name}'.");
            after[field.Name] = JsonNode.Parse(field.Value.GetRawText());
        }
        if (kind is "Rate" or "CreateRate")
        {
            if (patch.TryGetProperty("weekendPrice", out var weekend) && weekend.ValueKind != JsonValueKind.Null &&
                !patch.TryGetProperty("useWeekdayPriceOnWeekend", out _))
                after["useWeekdayPriceOnWeekend"] = false;
            if (after["useWeekdayPriceOnWeekend"]?.GetValue<bool>() == true) after["weekendPrice"] = null;
        }
        if (kind == "Room")
        {
            if (patch.TryGetProperty("fullDayPrice", out var full) && full.ValueKind != JsonValueKind.Null &&
                !patch.TryGetProperty("fullDayPricingEnabled", out _)) after["fullDayPricingEnabled"] = true;
            if (patch.TryGetProperty("weekendFullDayPrice", out var weekend) && weekend.ValueKind != JsonValueKind.Null &&
                !patch.TryGetProperty("useWeekdayFullDayPriceOnWeekend", out _)) after["useWeekdayFullDayPriceOnWeekend"] = false;
            if (after["useWeekdayFullDayPriceOnWeekend"]?.GetValue<bool>() == true) after["weekendFullDayPrice"] = null;
        }
        return new(kind, id, roomId, target, before, JsonSerializer.SerializeToElement(after, Json));
    }

    private static void ValidateConfiguration(PreparedChange change)
    {
        if (change.Kind is "Rate" or "CreateRate")
        {
            var r = change.After.Deserialize<UpdateRoomRateRequest>(Json)!;
            if (string.IsNullOrWhiteSpace(r.Name) || !TimeOnly.TryParse(r.StartTime, out var start) ||
                !TimeOnly.TryParse(r.EndTime, out var end) || start == end || !Enum.IsDefined(r.Type) ||
                r.Price is < 0 or > 1_000_000_000 || (!r.UseWeekdayPriceOnWeekend && r.WeekendPrice is not (> 0 and <= 1_000_000_000)))
                throw new InvalidOperationException($"Giờ hoặc giá của {change.Target} không hợp lệ.");
        }
        else if (change.Kind == "Room")
        {
            var r = change.After.Deserialize<UpdateRoomRequest>(Json)!;
            if (r.Capacity is < 1 or > 50 || (r.FullDayPricingEnabled == true && r.FullDayPrice is not (> 0 and <= 1_000_000_000)) ||
                (r.FullDayPricingEnabled == true && r.UseWeekdayFullDayPriceOnWeekend == false && r.WeekendFullDayPrice is not (> 0 and <= 1_000_000_000)))
                throw new InvalidOperationException($"Sức chứa hoặc giá cả ngày của {change.Target} không hợp lệ.");
        }
        else if (change.Kind == "Voucher")
        {
            var v = change.After.Deserialize<SaveVoucherRequest>(Json)!;
            if (v.DiscountPercent is < 1 or > 100 || v.EndsAtUtc <= v.StartsAtUtc ||
                v.Status is not (VoucherStatus.Draft or VoucherStatus.Active or VoucherStatus.Paused) ||
                (int)v.AppliesTo is < 1 or > 7 || v.TotalUsageLimit is <= 0 || v.PerCustomerUsageLimit is <= 0)
                throw new InvalidOperationException("Giá trị cấu hình voucher không hợp lệ.");
        }
        else if (change.Kind == "Pricing")
        {
            var p = change.After.Deserialize<SavePricingSettingsRequest>(Json)!;
            if (p.ThreeSlotCount is < 2 or > 20 || p.ThreeSlotDiscountPercent is < 0 or > 100 || p.WeekendDayMask is < 1 or > 127)
                throw new InvalidOperationException("Quy tắc giá không hợp lệ.");
        }
        else if (change.Kind == "SpecialDay")
        {
            var d = change.After.Deserialize<SaveSpecialPricingDayRequest>(Json)!;
            if (d.EndDate < d.StartDate || d.SurchargePercent is < 0 or > 100 ||
                !Enum.IsDefined(d.Category) || !Enum.IsDefined(d.BasePriceProfile) || !Enum.IsDefined(d.BookingMode))
                throw new InvalidOperationException("Ngày đặc biệt không hợp lệ.");
        }
    }

    private async Task<string?> ExecutePreparedAsync(AiChangeProposal proposal, Guid userId, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<PreparedPayload>(proposal.PayloadJson, Json)!;
        foreach (var change in payload.PreparedChanges)
        {
            ValidateConfiguration(change);
            JsonElement current;
            if (change.Kind is "Rate" or "CreateRate")
            {
                var room = await db.Rooms.AsNoTracking().Include(x => x.Rates).SingleOrDefaultAsync(x => x.PropertyId == proposal.PropertyId && x.Id == change.RoomId && x.IsActive, ct);
                if (room is null) return "Phòng không còn hoạt động hoặc không thuộc cơ sở.";
                if (change.Kind == "CreateRate")
                {
                    var request = change.After.Deserialize<CreateRoomRateRequest>(Json)!;
                    if (room.Rates.Any(x => x.IsActive && x.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase))) return "Khung giá cùng tên đã tồn tại.";
                    var (_, error) = await roomRateService.CreateAsync(proposal.PropertyId, room.Id, request, ct);
                    if (error is not null) return error.Message;
                    continue;
                }
                var rate = room.Rates.SingleOrDefault(x => x.Id == change.Id && x.IsActive);
                if (rate is null) return "Khung giá không còn hoạt động.";
                current = RateSnapshot(rate);
            }
            else if (change.Kind == "Room")
            {
                var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == proposal.PropertyId && x.Id == change.Id && x.IsActive, ct);
                if (room is null) return "Không tìm thấy phòng.";
                current = RoomSnapshot(room);
            }
            else if (change.Kind == "Voucher")
            {
                var voucher = await db.Vouchers.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == proposal.PropertyId && x.Id == change.Id, ct);
                if (voucher is null) return "Không tìm thấy voucher.";
                current = VoucherSnapshot(voucher);
            }
            else if (change.Kind == "SpecialDay")
            {
                var day = await db.SpecialPricingDays.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == proposal.PropertyId && x.Id == change.Id && !x.IsArchived, ct);
                if (day is null) return "Không tìm thấy ngày đặc biệt.";
                current = DaySnapshot(day);
            }
            else if (change.Kind == "Pricing")
            {
                var p = await pricingService.GetSettingsAsync(proposal.PropertyId, ct);
                current = JsonSerializer.SerializeToElement(new SavePricingSettingsRequest(p.ThreeSlotDiscountEnabled, p.ThreeSlotCount, p.ThreeSlotDiscountPercent, p.WeekendDayMask), Json);
            }
            else return "Thao tác không được phép.";
            if (!JsonElement.DeepEquals(current, change.Before))
                return $"Cấu hình {change.Target} đã thay đổi sau khi tạo preview. Hãy tạo lại preview.";
            string? failure = change.Kind switch
            {
                "Rate" => (await roomRateService.UpdateAsync(proposal.PropertyId, change.RoomId, change.Id, change.After.Deserialize<UpdateRoomRateRequest>(Json)!, ct)).Error?.Message,
                "Room" => (await roomService.UpdateAsync(proposal.PropertyId, change.Id, change.After.Deserialize<UpdateRoomRequest>(Json)!, ct)).Error,
                "Voucher" => (await voucherService.UpdateAsync(proposal.PropertyId, change.Id, change.After.Deserialize<SaveVoucherRequest>(Json)!, userId, ct)).Error?.Message,
                "SpecialDay" => (await pricingService.SaveSpecialDayAsync(proposal.PropertyId, change.Id, change.After.Deserialize<SaveSpecialPricingDayRequest>(Json)!, userId, ct)).Error?.Message,
                "Pricing" => (await pricingService.SaveSettingsAsync(proposal.PropertyId, change.After.Deserialize<SavePricingSettingsRequest>(Json)!, userId, ct)).Error?.Message,
                _ => "Thao tác không được phép."
            };
            if (failure is not null) return failure;
        }
        return null;
    }

    private static JsonElement RateSnapshot(RoomRate r) => JsonSerializer.SerializeToElement(new UpdateRoomRateRequest
    {
        Name = r.Name, StartTime = r.StartTime.ToString("HH:mm"), EndTime = r.EndTime.ToString("HH:mm"), Type = r.Type,
        Price = r.Price, UseWeekdayPriceOnWeekend = r.UseWeekdayPriceOnWeekend, WeekendPrice = r.WeekendPrice, SortOrder = r.SortOrder, IsActive = r.IsActive
    }, Json);
    private static JsonElement RoomSnapshot(Room r) => JsonSerializer.SerializeToElement(new UpdateRoomRequest(r.Code, r.Name,
        r.Capacity, r.SortOrder, r.IsActive, r.IsPublished, r.FullDayPricingEnabled, r.FullDayPrice, r.UseWeekdayFullDayPriceOnWeekend, r.WeekendFullDayPrice), Json);
    private static JsonElement VoucherSnapshot(Voucher v) => JsonSerializer.SerializeToElement(new SaveVoucherRequest
    {
        Code = v.Code, Description = v.Description, DiscountPercent = v.DiscountPercent, AppliesTo = v.AppliesTo,
        StartsAtUtc = v.StartsAtUtc, EndsAtUtc = v.EndsAtUtc, TotalUsageLimit = v.TotalUsageLimit,
        PerCustomerUsageLimit = v.PerCustomerUsageLimit, CustomerId = v.CustomerId, Status = v.Status
    }, Json);
    private static JsonElement DaySnapshot(SpecialPricingDay d) => JsonSerializer.SerializeToElement(new SaveSpecialPricingDayRequest(
        d.StartDate, d.EndDate, d.Name, d.Category, d.BasePriceProfile, d.SurchargePercent, d.BookingMode, d.AllowThreeSlotCombo, d.Note, d.IsActive), Json);
}
