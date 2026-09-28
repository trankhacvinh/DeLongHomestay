
namespace DeLong.Web.Features.Site;

public static class PublicUrlBuilder
{
    public static string Home() => "/";

    public static string PropertyHome(string siteSlug) => Home();

    public static string BookingHome(string? siteSlug = null) => "/#lich-phong";

    public static string Rooms(string? siteSlug = null) => BookingHome(siteSlug);

    public static string Room(string siteSlug, string roomSlug) => BookingHome(siteSlug);

    public static string Booking(string siteSlug, string? date = null, string? room = null, Guid? rate = null, bool embedded = false)
    {
        var query = new List<string>();
        Add(query, "date", date);
        Add(query, "room", room);
        if (rate.HasValue) Add(query, "rate", rate.Value.ToString());
        if (embedded) Add(query, "embed", "1");
        return WithQuery($"/h/{Segment(siteSlug)}/booking", query);
    }

    public static string GlobalBooking(string? siteSlug = null, string? date = null)
    {
        var query = new List<string>();
        Add(query, "site", siteSlug);
        Add(query, "date", date);
        return WithQuery("/booking", query);
    }

    public static string BookingLookup(string siteSlug) =>
        WithQuery("/booking/lookup", [$"siteSlug={Uri.EscapeDataString(siteSlug.Trim())}"]);

    private static string Segment(string value) => Uri.EscapeDataString(value.Trim());

    private static void Add(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}");
    }

    private static string WithQuery(string path, IReadOnlyCollection<string> query) =>
        query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
}
