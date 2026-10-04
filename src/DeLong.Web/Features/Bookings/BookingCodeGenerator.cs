using System.Security.Cryptography;

namespace DeLong.Web.Features.Bookings;

public static class BookingCodeGenerator
{
    // Exclude easily confused characters; the existing unique database index remains authoritative.
    public static string Create() => "BK" + RandomNumberGenerator.GetString("23456789ABCDEFGHJKLMNPQRSTUVWXYZ", 8);
}
