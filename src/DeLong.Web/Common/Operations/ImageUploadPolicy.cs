namespace DeLong.Web.Common.Operations;

public static class ImageUploadPolicy
{
    public const int MaxSourceMegabytes = 60;
    public const long MaxSourceBytes = MaxSourceMegabytes * 1024L * 1024L;
    public const long MaxPixels = 80_000_000;
}
