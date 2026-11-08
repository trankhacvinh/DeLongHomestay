using System.Text.Json;
using DeLong.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

public enum BookingCreationOrigin
{
    Unknown = 0,
    Customer = 1,
    Staff = 2
}

internal static class BookingCreationOriginResolver
{
    public static async Task<Dictionary<Guid, BookingCreationOrigin>> LoadAsync(
        AppDbContext db,
        Guid propertyId,
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken)
    {
        if (bookingIds.Count == 0) return [];
        var ids = bookingIds.ToArray();
        var creations = await db.AuditLogs.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.EntityType == "Booking" && ids.Contains(x.EntityId) &&
                (x.Action == "Created" || x.Action == "Imported"))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new { x.EntityId, x.ActorUserId, x.Action, x.AfterJson })
            .ToListAsync(cancellationToken);

        return creations.GroupBy(x => x.EntityId).ToDictionary(group => group.Key, group =>
        {
            var creation = group.First();
            return Resolve(creation.ActorUserId, creation.Action, creation.AfterJson);
        });
    }

    private static BookingCreationOrigin Resolve(Guid? actorUserId, string action, string? snapshot)
    {
        if (actorUserId.HasValue && actorUserId.Value != Guid.Empty) return BookingCreationOrigin.Staff;
        if (action != "Created" || string.IsNullOrWhiteSpace(snapshot)) return BookingCreationOrigin.Unknown;

        try
        {
            using var document = JsonDocument.Parse(snapshot);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return BookingCreationOrigin.Unknown;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("source", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    string.Equals(property.Value.GetString()?.Trim(), "Website", StringComparison.OrdinalIgnoreCase))
                    return BookingCreationOrigin.Customer;
            }
        }
        catch (JsonException)
        {
            return BookingCreationOrigin.Unknown;
        }

        return BookingCreationOrigin.Unknown;
    }
}
