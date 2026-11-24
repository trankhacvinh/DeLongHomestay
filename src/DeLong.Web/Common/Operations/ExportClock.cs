namespace DeLong.Web.Common.Operations;

public static class ExportClock
{
    // The VPS runs in UTC, so DateTime.Now printed "exported at" 7 hours early. Excel exports
    // show the time in Vietnam, where every branch is.
    public static DateTime VietnamNow()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return DateTime.UtcNow.AddHours(7);
        }
    }
}
