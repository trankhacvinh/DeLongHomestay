namespace DeLong.Web.Features.Rooms;

public sealed record RoomImageDto(
    Guid Id,
    string LargeUrl,
    string CardUrl,
    string ThumbnailUrl,
    string? AltText,
    bool IsCover,
    int SortOrder,
    int Width,
    int Height,
    long OriginalBytes);

public sealed record RoomContentDto(
    Guid RoomId,
    string Code,
    string Name,
    string Slug,
    string? ShortDescription,
    string? DescriptionHtml,
    bool IsPublished,
    IReadOnlyList<string> Amenities,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<RoomImageDto> Images);

public sealed class UpdateRoomContentRequest
{
    public string? Slug { get; init; }
    public string? ShortDescription { get; init; }
    public string? DescriptionHtml { get; init; }
    public bool IsPublished { get; init; }
    public IReadOnlyList<string> Amenities { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Highlights { get; init; } = [];
}

public sealed record UpdateRoomImageRequest(string? AltText, bool IsCover);
public sealed record ReorderRoomImagesRequest(IReadOnlyList<Guid> ImageIds);
public sealed record RoomContentError(string Code, string Message);
