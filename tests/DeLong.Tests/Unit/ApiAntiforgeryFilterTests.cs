using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class ApiAntiforgeryFilterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invalid_antiforgery_returns_400_without_running_mutation(bool valid)
    {
        var filter = new ApiAntiforgeryFilter(new TestAntiforgery(valid));
        var called = false;
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        var result = await filter.InvokeAsync(context, _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });
        Assert.Equal(valid, called);
        Assert.Equal(valid ? 200 : 400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    private sealed class TestAntiforgery(bool valid) : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new("request", "cookie", "form", "header");
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => GetAndStoreTokens(httpContext);
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(valid);
        public Task ValidateRequestAsync(HttpContext httpContext) => valid ? Task.CompletedTask : Task.FromException(new AntiforgeryValidationException("Invalid token"));
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
