using System.Globalization;

namespace DeLong.Web.Common.Media;

public static class MediaUrlVersioner
{
    public static string? WithCropVersion(string? url, double focalX, double focalY)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;

        var version = string.Concat(
            focalX.ToString("0.####", CultureInfo.InvariantCulture),
            "-",
            focalY.ToString("0.####", CultureInfo.InvariantCulture));
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}v={version}";
    }
}
