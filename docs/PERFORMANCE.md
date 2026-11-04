# Performance & cache operations

## FusionCache

Public read models use FusionCache L1 memory caching. Booking availability, booking creation, lookup and other transactional data are deliberately **not cached**. Public content entries use a short default TTL and the `public-content` tag.

Cache behavior is controlled through application configuration:

```json
"Performance": {
  "PublicCacheEnabled": true,
  "PublicCacheSeconds": 900,
  "PublicCacheWarmupSeconds": 15
}
```

`PublicCacheEnabled` defaults to `true`. When it is `false`, FusionCache is not registered for public reads and the EF cache invalidation interceptor is not attached; public services query PostgreSQL directly. Changing this setting requires an application restart, but no database migration. In production the recommended emergency override is an environment variable:

```text
Performance__PublicCacheEnabled=false
```

`PublicCacheSeconds` controls the L1 TTL while cache is enabled and is clamped to 1–3600 seconds. It used to be 30 s: on this low-traffic site most guests arrived after the entries had expired, and measured home page TTFB was ~2 s on a cold cache versus ~150 ms warm (2026-11-02). Admin edits invalidate the tag immediately (and once more 5 s later, so a public load that was in flight during the save cannot keep pre-edit data), so a long TTL does not delay edits.

`PublicCacheWarmupSeconds` (default 15, 0 disables) runs `PublicCacheWarmupWorker`, which reads the theme, global sections, contacts, active properties, global room catalog and editorial showcase at startup and on that interval. Fresh entries cost a memory lookup; expired or invalidated ones are reloaded by the worker instead of by a guest.

An EF Core `SaveChangesInterceptor` watches public-facing content entities (properties, site sections/settings, gallery/blog and room/rate/media content). After a successful write it calls `RemoveByTag("public-content")`, so admin changes invalidate public cache immediately instead of waiting for TTL expiration. The coarse tag is intentional: edits are rare, correctness is more important than preserving individual entries, and warming the small public catalog is cheap.

This is L1-only for now. Do not add Redis/L2 or a backplane until the application is actually deployed as multiple instances or measurements justify it. Availability/booking data must remain outside this cache.

## HTTP payloads and static files

Brotli/Gzip response compression is enabled for HTTPS text/JSON/SVG responses. Versioned static assets (`?v=...`) get a one-year immutable browser cache. Room upload URLs are content-addressed by generated media names and get a 30-day cache. Other unversioned assets get a conservative one-hour cache. Site assets whose files are overwritten (`cover.webp`, `logo.webp`, `og.webp`, favicon) now persist a cache-busting `?v=` URL when uploaded.

## What to measure

Use the existing slow-request warning (`Operations:SlowRequestThresholdMs`) before adding more infrastructure. Watch homepage/rooms/blog DB query frequency, p95 request duration, generated image response bytes and process memory. If multiple app instances are introduced, add FusionCache L2 + backplane together so invalidation remains coherent across nodes.

## Public booking on mobile

- Vue 3 is self-hosted at `wwwroot/vendor/vue/vue.global.prod.js` (versioned, immutable cache) instead of `cdn.jsdelivr.net`. A slow or unreachable third-party CDN blocked the booking page from becoming interactive, intermittently and mostly on mobile networks.
- Client-side CCCD compression stays on JPEG at max 1024px (quality 0.86 → 0.8 → 0.74 → 0.68 until ≤100 KB). It was 1600px/250 KB until 2026-11-04: server logs showed booking create+payment under 0.4 s at night, while the guest waited ~8 s for the second photo on a ~30 KB/s evening 4G uplink, so upload size is what guests feel. The owner then chose smaller still: 1024px keeps every CCCD line readable when the card fills the frame (~12 px/mm). Do not reintroduce `canvas.toBlob('image/webp')` here: Chrome's WebP encoder takes seconds per 2000px photo on phones, while Safari has no WebP encoder and skipped that work, which made booking slow only on Chrome. The server already re-encodes identity images to WebP (`IdentityDocumentStorage.OptimizeImage`).
- CCCD photos upload in the background as soon as the guest picks them (`POST /api/public/identity-staging/{side}` with a per-page `X-Identity-Staging-Key`), one at a time. On submit the booking claims them (`POST /api/public/booking-requests/{id}/identity-staging/claim`), so the guest does not wait for the upload after pressing submit. Sides that were not staged in time fall back to the normal per-booking upload, also one at a time (parallel uploads starved each other on slow uplinks and tripped the 30s no-progress timer). Staged files are encrypted like booking documents under `DataRoot/private/identity-staging` and purged after 2 hours.
- Submit never waits for CCCD (since 2026-11-04, owner: "khách cần nhập form nhanh và thanh toán nhanh"). After the booking is created, `public-booking-core-v2.js` saves the job (booking id, request key, staging key, compressed photo blobs) to IndexedDB `delong-identity-handoff` and returns, so the browser goes straight to the QR. `js/core/identity-handoff.js`, loaded on every public page, claims staged sides, uploads the rest from the saved blobs, then calls `/complete`; failed jobs retry on the next page load or `online` event for 2 hours. Navigation aborts any upload still running on the booking page, which is why the blobs are kept. Without IndexedDB (rare private modes) the old blocking path still runs. Every IndexedDB step is capped (2.5 s for the save on submit, 4 s otherwise) and photos are stored as ArrayBuffer, not Blob: on 2026-11-04 an iPhone Chrome guest's second booking hung on "Đang lưu thông tin đặt phòng…" after #78, and WebKit can leave IndexedDB requests pending forever.
