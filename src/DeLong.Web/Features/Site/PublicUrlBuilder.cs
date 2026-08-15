
namespace DeLong.Web.Features.Site;

public static class PublicUrlBuilder
{
    public static string Home() => "/";

    public static string PropertyHome(string siteSlug) =>
        $"/h/{Segment(siteSlug)}";

    public static string Rooms(string? siteSlug = null) =>
        string.IsNullOrWhiteSpace(siteSlug) ? "/rooms" : $"{PropertyHome(siteSlug)}/rooms";

    public static string Room(string siteSlug, string roomSlug) =>
        $"{Rooms(siteSlug)}/{Segment(roomSlug)}";

    public static string Booking(string siteSlug, string? date = null, string? room = null, Guid? rate = null)
    {
        var query = new List<string>();
        Add(query, "date", date);
        Add(query, "room", room);
        if (rate.HasValue) Add(query, "rate", rate.Value.ToString());
        return WithQuery($"{PropertyHome(siteSlug)}/booking", query);
    }

    public static string GlobalBooking(string? siteSlug = null, string? date = null)
    {
        var query = new List<string>();
        Add(query, "site", siteSlug);
        Add(query, "date", date);
        return WithQuery("/booking", query);
    }

    public static string BookingLookup(string siteSlug) =>
        $"{PropertyHome(siteSlug)}/booking/lookup";

    private static string Segment(string value) => Uri.EscapeDataString(value.Trim());

    private static void Add(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}");
    }

    private static string WithQuery(string path, IReadOnlyCollection<string> query) =>
        query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
}
