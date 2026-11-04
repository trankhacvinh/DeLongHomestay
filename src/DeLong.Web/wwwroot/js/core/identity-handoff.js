// Finishes a booking's CCCD upload after the guest has already moved on to the payment QR.
// The booking page saves the compressed photos in IndexedDB and navigates straight to payment;
// this script (loaded on every public page) then claims the photos that were already staged
// and uploads the rest in the background, so the guest never waits for a slow evening uplink.
(function () {
    const DB_NAME = 'delong-identity-handoff';
    const STORE = 'jobs';
    const FLAG = 'delong.identityHandoff';
    const LEASE = 'delong.identityHandoff.lease';
    const MAX_AGE_MS = 2 * 60 * 60 * 1000; // staged photos are purged on the server after 2 hours
    const labels = { front: 'mặt trước', back: 'mặt sau', 'second-front': 'mặt trước người thứ hai', 'second-back': 'mặt sau người thứ hai' };

    function storage(action) { try { return action(window.localStorage); } catch { return null; } }

    function openDb() {
        return new Promise((resolve, reject) => {
            if (!window.indexedDB) { reject(new Error('IndexedDB unavailable')); return; }
            const request = window.indexedDB.open(DB_NAME, 1);
            request.onupgradeneeded = () => request.result.createObjectStore(STORE, { keyPath: 'bookingId' });
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error || new Error('IndexedDB open failed'));
        });
    }

    async function withStore(mode, action) {
        const db = await openDb();
        try {
            return await new Promise((resolve, reject) => {
                const tx = db.transaction(STORE, mode);
                const result = action(tx.objectStore(STORE));
                tx.oncomplete = () => resolve(result?.result);
                tx.onerror = tx.onabort = () => reject(tx.error || new Error('IndexedDB write failed'));
            });
        } finally { db.close(); }
    }

    const query = siteSlug => siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : '';
    const csrf = () => document.querySelector('meta[name="csrf-token"]')?.content || '';

    async function postJson(url, body, requestKey) {
        const response = await fetch(url, {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', Accept: 'application/json', 'X-CSRF-TOKEN': csrf(), 'Idempotency-Key': requestKey },
            body: JSON.stringify(body)
        });
        let payload = null; try { payload = await response.json(); } catch { }
        if (!response.ok) { const error = new Error(payload?.detail || payload?.title || `HTTP ${response.status}`); error.status = response.status; throw error; }
        return payload;
    }

    async function uploadSide(job, side, blob) {
        const form = new FormData();
        form.append('file', blob, `${side}.jpg`);
        const response = await fetch(`/api/public/booking-requests/${encodeURIComponent(job.bookingId)}/identity-documents/${side}${query(job.siteSlug)}`, {
            method: 'POST', credentials: 'same-origin',
            headers: { Accept: 'application/json', 'X-CSRF-TOKEN': csrf(), 'Idempotency-Key': job.requestKey },
            body: form
        });
        let payload = null; try { payload = await response.json(); } catch { }
        if (!response.ok || payload?.side !== side) {
            const error = new Error(payload?.detail || `Không thể lưu CCCD ${labels[side] || side}.`); error.status = response.status; throw error;
        }
    }

    async function save(job) {
        await withStore('readwrite', store => store.put({ ...job, createdAt: Date.now() }));
        storage(local => local.setItem(FLAG, '1'));
    }

    async function remove(bookingId) {
        await withStore('readwrite', store => store.delete(bookingId));
    }

    async function finish(job) {
        const staged = job.sides.filter(item => item.staged).map(item => item.side);
        let claimed = [];
        if (staged.length) {
            try {
                const response = await postJson(`/api/public/booking-requests/${encodeURIComponent(job.bookingId)}/identity-staging/claim${query(job.siteSlug)}`,
                    { stagingKey: job.stagingKey, sides: staged }, job.requestKey);
                claimed = Array.isArray(response?.sides) ? response.sides : [];
            } catch (error) { if (!error.status || error.status >= 500) throw error; }
        }
        for (const item of job.sides) {
            if (claimed.includes(item.side)) continue;
            // Not staged, or the staged copy expired before the claim: send the saved photo itself.
            if (!item.blob) continue;
            await uploadSide(job, item.side, item.blob);
            item.blob = null; item.staged = false; claimed.push(item.side);
            await withStore('readwrite', store => store.put(job));
        }
        await postJson(`/api/public/booking-requests/${encodeURIComponent(job.bookingId)}/complete${query(job.siteSlug)}`, {}, job.requestKey).catch(error => {
            // booking_incomplete only means staff will see a missing side; nothing more to retry.
            if (!error.status || error.status >= 500) throw error;
        });
    }

    let running = null;
    function resume() {
        if (running) return running;
        if (storage(local => local.getItem(FLAG)) !== '1') return Promise.resolve();
        // One tab at a time: a second tab waits for the first tab's lease to expire.
        const lease = Number(storage(local => local.getItem(LEASE)) || 0);
        if (lease > Date.now()) return Promise.resolve();
        running = (async () => {
            const renew = () => storage(local => local.setItem(LEASE, String(Date.now() + 60000)));
            renew();
            const timer = setInterval(renew, 20000);
            try {
                const jobs = await withStore('readonly', store => store.getAll());
                for (const job of jobs || []) {
                    if (Date.now() - (job.createdAt || 0) > MAX_AGE_MS) { await remove(job.bookingId); continue; }
                    try { await finish(job); await remove(job.bookingId); }
                    catch (error) {
                        // 4xx other than conflicts: the booking is gone or locked, retrying cannot help.
                        if (error.status >= 400 && error.status < 500) await remove(job.bookingId);
                    }
                }
                const left = await withStore('readonly', store => store.count());
                if (!left) storage(local => local.removeItem(FLAG));
            } catch { /* IndexedDB unavailable: nothing to resume */ }
            finally { clearInterval(timer); storage(local => local.removeItem(LEASE)); running = null; }
        })();
        return running;
    }

    window.DeLongIdentityHandoff = { save, resume, isSupported: () => !!window.indexedDB };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => { void resume(); }, { once: true });
    else void resume();
    window.addEventListener('online', () => { void resume(); });
})();
