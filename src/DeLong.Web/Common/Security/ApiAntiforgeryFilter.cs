using Microsoft.AspNetCore.Antiforgery;

namespace DeLong.Web.Common.Security;

public sealed class ApiAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Phiên xác thực không hợp lệ",
                detail: "Thiếu hoặc sai mã xác thực yêu cầu. Vui lòng tải lại trang và thử lại.");
        }
        return await next(context);
    }
}
