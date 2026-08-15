namespace DeLong.Web.Domain.Entities;

public sealed class PropertySiteSettings : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string? SiteName { get; set; }
    public string? Tagline { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FacebookUrl { get; set; }
    public string? ZaloUrl { get; set; }
    public string? GoogleMapsUrl { get; set; }

    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? OgImageUrl { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? CanonicalBaseUrl { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? GoogleSiteVerification { get; set; }
    public bool RobotsIndex { get; set; } = true;

    public string? CustomCss { get; set; }
    public string? CustomJs { get; set; }
}
