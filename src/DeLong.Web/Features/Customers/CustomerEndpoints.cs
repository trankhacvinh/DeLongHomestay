using DeLong.Web.Common.Security;
using DeLong.Web.Common.Operations;
using DeLong.Web.Features.PublicBooking;

namespace DeLong.Web.Features.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/customers")
            .RequireAuthorization("ViewOperations")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Customers");

        group.MapGet("/", async (
            Guid propertyId,
            string? q,
            CustomerService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(propertyId, q, cancellationToken)));

        group.MapGet("/{customerId:guid}", async (
            Guid propertyId,
            Guid customerId,
            CustomerService service,
            CancellationToken cancellationToken) =>
        {
            var customer = await service.GetAsync(propertyId, customerId, cancellationToken);
            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });

        group.MapGet("/{customerId:guid}/profile", async (
            Guid propertyId,
            Guid customerId,
            CustomerService service,
            StoragePaths paths,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var profile = await service.GetProfileAsync(propertyId, customerId, cancellationToken);
            if (profile is null) return Results.NotFound();
            var storage = new IdentityDocumentStorage(paths, configuration);
            var documentBookings = 0;
            foreach (var booking in profile.Bookings)
            {
                if ((await storage.ListAsync(propertyId, booking.Id, cancellationToken)).Count > 0) documentBookings++;
            }
            return Results.Ok(profile with { HasIdentityDocuments = documentBookings > 0, IdentityDocumentBookingCount = documentBookings });
        });

        group.MapPost("/", async (
            Guid propertyId,
            CreateCustomerRequest request,
            CustomerService service,
            CancellationToken cancellationToken) =>
        {
            var (customer, error) = await service.CreateAsync(propertyId, request, cancellationToken);
            return error is not null
                ? Results.Problem(title: "Không thể tạo khách hàng", detail: error, statusCode: 400)
                : Results.Created($"/api/admin/properties/{propertyId}/customers/{customer!.Id}", customer);
        })
        .RequireAuthorization("ManageBookings")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPut("/{customerId:guid}", async (
            Guid propertyId,
            Guid customerId,
            UpdateCustomerRequest request,
            CustomerService service,
            CancellationToken cancellationToken) =>
        {
            var (customer, error) = await service.UpdateAsync(propertyId, customerId, request, cancellationToken);
            if (customer is null && error == "Không tìm thấy khách hàng.") return Results.NotFound();
            return error is not null
                ? Results.Problem(title: "Không thể cập nhật khách hàng", detail: error, statusCode: 400)
                : Results.Ok(customer);
        })
        .RequireAuthorization("ManageBookings")
        .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }
}
