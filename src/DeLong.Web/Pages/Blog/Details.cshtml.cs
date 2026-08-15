using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Blog;

public sealed class DetailsModel(
    PublicPropertyResolver propertyResolver,
    PropertyEditorialContentService editorialContentService) : PageModel
{
    public BlogPostDto Post { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string siteSlug, string slug, CancellationToken ct)
    {
        var property = await propertyResolver.ResolveAsync(siteSlug, ct);
        if (property is null) return NotFound();
        var post = await editorialContentService.GetPublicPostAsync(property.Id, slug, ct);
        if (post is null) return NotFound();
        Post = post;
        return Page();
    }
}
