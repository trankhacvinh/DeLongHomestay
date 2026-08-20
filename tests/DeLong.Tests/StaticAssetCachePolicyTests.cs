using DeLong.Web.Common.Caching;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeLong.Tests;

public sealed class StaticAssetCachePolicyTests
{
    [Fact]
    public void Development_never_caches_versioned_assets()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/js/core/api.js";
        context.Request.QueryString = new QueryString("?v=abc123");

        StaticAssetCachePolicy.Apply(context, isDevelopment: true);

        Assert.Equal(StaticAssetCachePolicy.DevelopmentCacheControl, context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
        Assert.Equal("0", context.Response.Headers.Expires.ToString());
        Assert.Equal(StaticAssetCachePolicy.DevelopmentPolicyHeader, context.Response.Headers["X-DeLong-Cache-Policy"].ToString());
    }

    [Fact]
    public void Production_keeps_immutable_cache_for_versioned_assets()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/css/site.css";
        context.Request.QueryString = new QueryString("?v=abc123");

        StaticAssetCachePolicy.Apply(context, isDevelopment: false);

        Assert.Equal("public,max-age=31536000,immutable", context.Response.Headers.CacheControl.ToString());
        Assert.False(context.Response.Headers.ContainsKey("X-DeLong-Cache-Policy"));
    }

    [Fact]
    public void Production_keeps_long_cache_for_room_media()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/rooms/room-1/card.webp";

        StaticAssetCachePolicy.Apply(context, isDevelopment: false);

        Assert.Equal("public,max-age=2592000", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Production_uses_short_cache_for_unversioned_static_assets()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/js/pages/custom.js";

        StaticAssetCachePolicy.Apply(context, isDevelopment: false);

        Assert.Equal("public,max-age=3600", context.Response.Headers.CacheControl.ToString());
    }
}
