using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Rooms;

public sealed class RoomRateService(AppDbContext db)
{
    public async Task<(RoomRateDto? Rate, RoomRateOperationError? Error)> CreateAsync(Guid propertyId, Guid roomId, CreateRoomRateRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.Name, request.StartTime, request.EndTime, request.Type, request.Price);
        if (validation.Error is not null) return (null, validation.Error);
        if (!await db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Id == roomId && x.IsActive, cancellationToken)) return (null, new("room_not_found", "Không tìm thấy phòng hoặc phòng đã ngừng hoạt động."));

        var rate = new RoomRate
        {
            RoomId = roomId,
            Name = request.Name.Trim(),
            StartTime = validation.Start!.Value,
            EndTime = validation.End!.Value,
            Type = request.Type,
            IsOvernight = request.Type == RoomRateType.Overnight,
            Price = request.Price,
            SortOrder = request.SortOrder,
            IsActive = true
        };
        db.RoomRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(rate), null);
    }

    public async Task<(RoomRateDto? Rate, RoomRateOperationError? Error)> UpdateAsync(Guid propertyId, Guid roomId, Guid rateId, UpdateRoomRateRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request.Name, request.StartTime, request.EndTime, request.Type, request.Price);
        if (validation.Error is not null) return (null, validation.Error);
        var rate = await db.RoomRates.Include(x => x.Room).SingleOrDefaultAsync(x => x.Id == rateId && x.RoomId == roomId && x.Room.PropertyId == propertyId, cancellationToken);
        if (rate is null) return (null, new("not_found", "Không tìm thấy khung giá."));

        rate.Name = request.Name.Trim(); rate.StartTime = validation.Start!.Value; rate.EndTime = validation.End!.Value;
        rate.Type = request.Type; rate.IsOvernight = request.Type == RoomRateType.Overnight; rate.Price = request.Price;
        rate.SortOrder = request.SortOrder; rate.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(rate), null);
    }

    public async Task<bool> ArchiveAsync(Guid propertyId, Guid roomId, Guid rateId, CancellationToken cancellationToken = default)
    {
        var rate = await db.RoomRates.Include(x => x.Room).SingleOrDefaultAsync(x => x.Id == rateId && x.RoomId == roomId && x.Room.PropertyId == propertyId, cancellationToken);
        if (rate is null) return false;
        rate.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (TimeOnly? Start, TimeOnly? End, RoomRateOperationError? Error) Validate(string name, string startTime, string endTime, RoomRateType type, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, null, new("validation", "Tên khung giá là bắt buộc."));
        if (name.Trim().Length > 100) return (null, null, new("validation", "Tên khung giá tối đa 100 ký tự."));
        if (!Enum.IsDefined(type)) return (null, null, new("validation", "Loại giá không hợp lệ."));
        if (!TimeOnly.TryParse(startTime, out var start)) return (null, null, new("validation", "Giờ nhận/bắt đầu không hợp lệ."));
        if (!TimeOnly.TryParse(endTime, out var end)) return (null, null, new("validation", "Giờ trả/kết thúc không hợp lệ."));
        if (price < 0 || price > 1_000_000_000m) return (null, null, new("validation", "Giá phòng không hợp lệ."));
        if (type == RoomRateType.Nightly && price <= 0) return (null, null, new("validation", "Giá lưu trú theo đêm phải lớn hơn 0."));
        return (start, end, null);
    }

    private static RoomRateDto ToDto(RoomRate rate) => new(rate.Id, rate.Name, rate.StartTime.ToString("HH:mm"), rate.EndTime.ToString("HH:mm"), rate.Type, rate.IsOvernight, rate.Price, rate.IsActive, rate.SortOrder);
}
