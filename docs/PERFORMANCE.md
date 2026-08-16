# Performance & cache operations

## FusionCache

Public read models use FusionCache L1 memory caching. Booking availability, booking creation, lookup and other transactional data are deliberately **not cached**. Public content entries use a short default TTL and the `public-content` tag.

Cache behavior is controlled through application configuration:

```json
"Performance": {
  "PublicCacheEnabled": true,
  "PublicCacheSeconds": 30
}
```

`PublicCacheEnabled` defaults to `true`. When it is `false`, FusionCache is not registered for public reads and the EF cache invalidation interceptor is not attached; public services query PostgreSQL directly. Changing this setting requires an application restart, but no database migration. In production the recommended emergency override is an environment variable:

```text
Performance__PublicCacheEnabled=false
```

`PublicCacheSeconds` controls the L1 TTL while cache is enabled and is clamped to 1–3600 seconds.

An EF Core `SaveChangesInterceptor` watches public-facing content entities (properties, site sections/settings, gallery/blog and room/rate/media content). After a successful write it calls `RemoveByTag("public-content")`, so admin changes invalidate public cache immediately instead of waiting for TTL expiration. The coarse tag is intentional: edits are rare, correctness is more important than preserving individual entries, and warming the small public catalog is cheap.

This is L1-only for now. Do not add Redis/L2 or a backplane until the application is actually deployed as multiple instances or measurements justify it. Availability/booking data must remain outside this cache.

## HTTP payloads and static files

Brotli/Gzip response compression is enabled for HTTPS text/JSON/SVG responses. Versioned static assets (`?v=...`) get a one-year immutable browser cache. Room upload URLs are content-addressed by generated media names and get a 30-day cache. Other unversioned assets get a conservative one-hour cache. Site assets whose files are overwritten (`cover.webp`, `logo.webp`, `og.webp`, favicon) now persist a cache-busting `?v=` URL when uploaded.

## What to measure

Use the existing slow-request warning (`Operations:SlowRequestThresholdMs`) before adding more infrastructure. Watch homepage/rooms/blog DB query frequency, p95 request duration, generated image response bytes and process memory. If multiple app instances are introduced, add FusionCache L2 + backplane together so invalidation remains coherent across nodes.
