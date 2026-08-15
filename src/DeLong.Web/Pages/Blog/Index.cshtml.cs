using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Blog;

public sealed class IndexModel(
    PublicPropertyResolver propertyResolver,
    PropertyEditorialContentService editorialContentService) : PageModel
{
    public string? SiteSlug { get; private set; }
    public string Title { get; private set; } = "Blog De Long Homestay";
    public IReadOnlyList<BlogPostDto> Posts { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? siteSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(siteSlug))
        {
            Posts = await editorialContentService.GetGlobalPublicPostsAsync(ct);
            return Page();
        }

        var property = await propertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return NotFound();
        SiteSlug = property.SiteSlug;
        Title = $"Blog {property.Name}";
        Posts = await editorialContentService.GetPublicPostsAsync(property.Id, ct);
        return Page();
    }
}
