from pathlib import Path

# Fix EF projection so the helper method is not translated into SQL.
path = Path('src/DeLong.Web/Features/Site/SiteContentService.cs')
s = path.read_text()
old = '''        var sections = await db.Set<HomeSection>().AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).Select(x => ToDto(x)).ToListAsync(ct);
        return new SiteAdminDto(ToDto(property, settings), sections);
'''
new = '''        var sectionEntities = await db.Set<HomeSection>().AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var sections = sectionEntities.Select(ToDto).ToList();
        return new SiteAdminDto(ToDto(property, settings), sections);
'''
if old not in s:
    raise SystemExit('SiteContentService projection anchor not found')
path.write_text(s.replace(old, new, 1))

# Keep output dimensions before disposing the generated bitmap.
path = Path('src/DeLong.Web/Features/Site/SiteAssetStorage.cs')
s = path.read_text()
old = '''        using (output)
        using (var image = SKImage.FromBitmap(output))
        using (var encoded = image.Encode(format, quality))
        await using (var stream = File.Create(Path.Combine(publicRoot, fileName)))
            encoded.SaveTo(stream);

        return (new StoredSiteAsset($"/uploads/site/{safeProperty}/{fileName}", output.Width, output.Height), null);
'''
new = '''        var outputWidth = output.Width;
        var outputHeight = output.Height;
        using (output)
        using (var image = SKImage.FromBitmap(output))
        using (var encoded = image.Encode(format, quality))
        await using (var stream = File.Create(Path.Combine(publicRoot, fileName)))
            encoded.SaveTo(stream);

        return (new StoredSiteAsset($"/uploads/site/{safeProperty}/{fileName}", outputWidth, outputHeight), null);
'''
if old not in s:
    raise SystemExit('SiteAssetStorage dimensions anchor not found')
path.write_text(s.replace(old, new, 1))

# Format the property-local date/time without interpreting it as the browser's timezone.
path = Path('src/DeLong.Web/wwwroot/js/pages/booking-lookup.js')
s = path.read_text()
old = '''            dateTime(value) {
                if (!value) return '';
                const date = new Date(value);
                return new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
            },
'''
new = '''            dateTime(value) {
                if (!value) return '';
                const match = /^(\\d{4})-(\\d{2})-(\\d{2})T(\\d{2}):(\\d{2})/.exec(value);
                return match ? `${match[4]}:${match[5]} ${match[3]}/${match[2]}/${match[1]}` : value;
            },
'''
if old not in s:
    raise SystemExit('booking lookup dateTime anchor not found')
path.write_text(s.replace(old, new, 1))
