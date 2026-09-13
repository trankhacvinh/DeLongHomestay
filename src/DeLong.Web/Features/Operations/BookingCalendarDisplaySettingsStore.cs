using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeLong.Web.Common.Operations;

namespace DeLong.Web.Features.Operations;

public sealed record BookingCalendarDisplaySettings(
    string RequestedColor,
    string HeldColor,
    string ConfirmedColor,
    string CheckedInColor,
    string UnpaidColor,
    string FlexibleTimeColor,
    string SpecialRequestColor,
    string MixedSlotColor);

public sealed class UpdateBookingCalendarDisplaySettingsRequest
{
    public string? RequestedColor { get; init; }
    public string? HeldColor { get; init; }
    public string? ConfirmedColor { get; init; }
    public string? CheckedInColor { get; init; }
    public string? UnpaidColor { get; init; }
    public string? FlexibleTimeColor { get; init; }
    public string? SpecialRequestColor { get; init; }
    public string? MixedSlotColor { get; init; }
}

public sealed partial class BookingCalendarDisplaySettingsStore(StoragePaths paths)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string root = Path.Combine(paths.DataRoot, "booking-calendar-settings");

    public async Task<BookingCalendarDisplaySettings> GetAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(propertyId);
        if (!File.Exists(path)) return Defaults();

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var stored = await JsonSerializer.DeserializeAsync<BookingCalendarDisplaySettings>(stream, JsonOptions, cancellationToken);
            return Normalize(stored);
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    public async Task<(BookingCalendarDisplaySettings? Settings, string? Error)> SaveAsync(
        Guid propertyId,
        UpdateBookingCalendarDisplaySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var colors = new[]
        {
            request.RequestedColor, request.HeldColor, request.ConfirmedColor, request.CheckedInColor,
            request.UnpaidColor, request.FlexibleTimeColor, request.SpecialRequestColor, request.MixedSlotColor
        };
        if (colors.Any(color => !IsColor(color)))
            return (null, "Tất cả màu lịch booking phải là mã HEX dạng #RRGGBB.");

        var settings = new BookingCalendarDisplaySettings(
            request.RequestedColor!.Trim().ToUpperInvariant(), request.HeldColor!.Trim().ToUpperInvariant(),
            request.ConfirmedColor!.Trim().ToUpperInvariant(), request.CheckedInColor!.Trim().ToUpperInvariant(),
            request.UnpaidColor!.Trim().ToUpperInvariant(), request.FlexibleTimeColor!.Trim().ToUpperInvariant(),
            request.SpecialRequestColor!.Trim().ToUpperInvariant(), request.MixedSlotColor!.Trim().ToUpperInvariant());

        var gate = Gates.GetOrAdd(propertyId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(root);
            var path = PathFor(propertyId);
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                    await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                File.Move(temp, path, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            return (settings, null);
        }
        finally
        {
            gate.Release();
        }
    }

    private BookingCalendarDisplaySettings Normalize(BookingCalendarDisplaySettings? value)
    {
        var defaults = Defaults();
        return value is null ? defaults : new BookingCalendarDisplaySettings(
            NormalizeColor(value.RequestedColor, defaults.RequestedColor), NormalizeColor(value.HeldColor, defaults.HeldColor),
            NormalizeColor(value.ConfirmedColor, defaults.ConfirmedColor), NormalizeColor(value.CheckedInColor, defaults.CheckedInColor),
            NormalizeColor(value.UnpaidColor, defaults.UnpaidColor), NormalizeColor(value.FlexibleTimeColor, defaults.FlexibleTimeColor),
            NormalizeColor(value.SpecialRequestColor, defaults.SpecialRequestColor), NormalizeColor(value.MixedSlotColor, defaults.MixedSlotColor));
    }

    private static BookingCalendarDisplaySettings Defaults() => new(
        "#64748B", "#D39B3C", "#397967", "#176B63", "#C94B4B", "#D6A72C", "#7C5CC4", "#287D9B");

    private string PathFor(Guid propertyId) => Path.Combine(root, propertyId.ToString("N") + ".json");
    private static bool IsColor(string? value) => !string.IsNullOrWhiteSpace(value) && HexColorRegex().IsMatch(value.Trim());
    private static string NormalizeColor(string? value, string fallback) => IsColor(value) ? value!.Trim().ToUpperInvariant() : fallback;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
