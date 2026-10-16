using System.Security.Cryptography;

namespace DeLong.Web.Features.PublicBooking;

public static class BookingCompletionValidation
{
    public static async Task<string?> ValidateAsync(
        IdentityDocumentStorage storage,
        Guid propertyId,
        Guid bookingId,
        BookingGuestDetailsDto? details,
        CancellationToken cancellationToken = default)
    {
        if (details is null || !details.PolicyAccepted)
            return "Thông tin đặt phòng chưa được lưu đầy đủ. Vui lòng bấm gửi lại.";
        if (!storage.IsConfigured)
            return "Kho CCCD bảo mật chưa sẵn sàng. Vui lòng thử lại.";
        string[] sides = details.GuestCount >= 3
            ? ["front", "back", "second-front", "second-back"] : ["front", "back"];
        foreach (var side in sides)
        {
            IdentityDocumentReadResult? document;
            try { document = await storage.ReadAsync(propertyId, bookingId, side, cancellationToken); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                return "Không thể đọc CCCD đã lưu. Vui lòng bấm gửi lại hoặc liên hệ cơ sở.";
            }
            if (document is null)
                return "CCCD chưa được lưu đầy đủ. Bấm gửi lại để thử tải ảnh lần nữa.";
            CryptographicOperations.ZeroMemory(document.Bytes);
        }
        return null;
    }
}
