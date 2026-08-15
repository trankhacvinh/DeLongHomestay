using DeLong.Web.Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace DeLong.Web.Pages.Admin;

public sealed record PropertyChoiceVm(Guid Id, string Code, string Name, string Url);

public sealed class SelectPropertyModel(CurrentPropertyService currentPropertyService) : PageModel
{
    public IReadOnlyList<PropertyChoiceVm> Properties { get; private set; } = [];
    public string ReturnUrl { get; private set; } = "/Admin";

    public async Task<IActionResult> OnGetAsync(string? returnUrl, CancellationToken cancellationToken)
    {
        var accessible = await currentPropertyService.GetAccessibleAsync(User, cancellationToken);
        if (accessible.Count == 0) return Forbid();

        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Admin";

        if (accessible.Count == 1)
        {
            await currentPropertyService.ResolveAsync(User, accessible[0].Id, cancellationToken);
            return LocalRedirect(WithProperty(ReturnUrl, accessible[0].Id));
        }

        Properties = accessible
            .Select(property => new PropertyChoiceVm(
                property.Id,
                property.Code,
                property.Name,
                WithProperty(ReturnUrl, property.Id)))
            .ToList();

        return Page();
    }

    private static string WithProperty(string returnUrl, Guid propertyId)
    {
        var fragmentIndex = returnUrl.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? returnUrl[fragmentIndex..] : string.Empty;
        var withoutFragment = fragmentIndex >= 0 ? returnUrl[..fragmentIndex] : returnUrl;
        var queryIndex = withoutFragment.IndexOf('?');
        var path = queryIndex >= 0 ? withoutFragment[..queryIndex] : withoutFragment;
        var rawQuery = queryIndex >= 0 ? withoutFragment[queryIndex..] : string.Empty;

        var values = new List<KeyValuePair<string, string?>>();
        foreach (var pair in QueryHelpers.ParseQuery(rawQuery))
        {
            if (string.Equals(pair.Key, "propertyId", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var value in pair.Value)
                values.Add(new KeyValuePair<string, string?>(pair.Key, value));
        }
        values.Add(new KeyValuePair<string, string?>("propertyId", propertyId.ToString()));

        return path + QueryString.Create(values) + fragment;
    }
}
