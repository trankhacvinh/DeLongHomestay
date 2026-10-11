using System.Text.Json;
using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.DataQuality;

public sealed record BookingQualityFact(Guid Id, Guid RoomId, Guid CustomerId, string Code, string RoomName,
    DateTime CheckInUtc, DateTime CheckOutUtc, BookingStatus Status, decimal Total, decimal Paid);
public sealed record QualityFinding(string Fingerprint, string Rule, string Severity, Guid BookingId,
    Guid? RelatedBookingId, string Title, string EvidenceJson);

public static class DataQualityRules
{
    public static IReadOnlyList<QualityFinding> Evaluate(IReadOnlyList<BookingQualityFact> bookings)
    {
        var findings = new List<QualityFinding>();
        foreach (var b in bookings)
        {
            if (b.CheckOutUtc <= b.CheckInUtc || b.Total < 0 || b.Paid < 0 || b.Paid > b.Total)
                findings.Add(Finding("invalid_booking", "High", b, null, "Thời gian hoặc số tiền cần đối soát"));
            if (b.Status == BookingStatus.Completed && b.Total > b.Paid)
                findings.Add(Finding("completed_balance", "Medium", b, null, "Đã hoàn tất nhưng còn công nợ trên hệ thống"));
            if (b.Status is BookingStatus.Cancelled or BookingStatus.NoShow && b.Paid > 0)
                findings.Add(Finding("cancelled_receipt", "Medium", b, null, "Đơn hủy/vắng mặt còn tiền đã thu; kiểm tra phí hủy hoặc hoàn tiền"));
        }
        foreach (var room in bookings.Where(b => b.Status is BookingStatus.Held or BookingStatus.Confirmed or BookingStatus.CheckedIn or BookingStatus.Completed)
                     .GroupBy(b => b.RoomId))
        {
            var active = new List<BookingQualityFact>();
            foreach (var b in room.OrderBy(b => b.CheckInUtc).ThenBy(b => b.Id))
            {
                active.RemoveAll(a => a.CheckOutUtc <= b.CheckInUtc);
                foreach (var a in active)
                    findings.Add(Finding("overlap", "High", a, b,
                        a.CustomerId == b.CustomerId && a.CheckInUtc == b.CheckInUtc && a.CheckOutUtc == b.CheckOutUtc
                            ? "Nghi trùng đơn: cùng khách, phòng và thời gian" : "Hai đơn chiếm phòng trùng thời gian"));
                active.Add(b);
            }
        }
        return findings;
    }

    private static QualityFinding Finding(string rule, string severity, BookingQualityFact a, BookingQualityFact? b, string title)
    {
        if (b is not null && a.Id.CompareTo(b.Id) > 0) (a, b) = (b, a);
        return new($"{rule}:{a.Id:N}:{b?.Id.ToString("N")}", rule, severity, a.Id, b?.Id, title,
            JsonSerializer.Serialize(new { booking = a, relatedBooking = b, balance = a.Total - a.Paid },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
