(function () {
    const localHostnames = new Set(['localhost', '127.0.0.1', '::1']);
    const legacyWorkerReloadKey = 'delong.legacy-service-worker-cleared';

    async function clearLegacyLocalServiceWorkers() {
        if (!localHostnames.has(window.location.hostname.toLowerCase()) || !('serviceWorker' in navigator)) return;

        try {
            const registrations = await navigator.serviceWorker.getRegistrations();
            const hasController = navigator.serviceWorker.controller !== null;
            if (registrations.length === 0 && !hasController) {
                sessionStorage.removeItem(legacyWorkerReloadKey);
                return;
            }

            await Promise.all(registrations.map(registration => registration.unregister()));
            if ('caches' in window) {
                const cacheNames = await caches.keys();
                await Promise.all(cacheNames.map(cacheName => caches.delete(cacheName)));
            }

            if (hasController && sessionStorage.getItem(legacyWorkerReloadKey) !== 'true') {
                sessionStorage.setItem(legacyWorkerReloadKey, 'true');
                window.location.reload();
            }
        } catch (error) {
            console.warn('Không thể dọn Service Worker cũ trên localhost.', error);
        }
    }

    void clearLegacyLocalServiceWorkers();

    const body = document.body;
    const toggles = document.querySelectorAll('[data-sidebar-toggle]');
    const overlay = document.querySelector('[data-sidebar-overlay]');

    const setOpen = (open) => {
        body.classList.toggle('sidebar-open', open);
        toggles.forEach(button => button.setAttribute('aria-expanded', open ? 'true' : 'false'));
    };

    toggles.forEach(button => button.addEventListener('click', () => setOpen(!body.classList.contains('sidebar-open'))));
    overlay?.addEventListener('click', () => setOpen(false));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') setOpen(false);
    });

    // const userChip = document.querySelector('.admin-topbar .user-chip');
    // if (userChip && !document.querySelector('.topbar-password-link')) {
    //     const passwordLink = document.createElement('a');
    //     const returnUrl = `${window.location.pathname}${window.location.search}`;
    //     passwordLink.className = 'btn btn-ghost btn-sm topbar-password-link';
    //     passwordLink.href = `/Account/ChangePassword?returnUrl=${encodeURIComponent(returnUrl)}`;
    //     passwordLink.title = 'Đổi mật khẩu';
    //     passwordLink.setAttribute('aria-label', 'Đổi mật khẩu');
    //     passwordLink.innerHTML = '<svg aria-hidden="true"><use href="#i-settings"></use></svg><span>Đổi mật khẩu</span>';
    //     userChip.insertAdjacentElement('afterend', passwordLink);
    // }

    const propertySwitcher = document.querySelector('[data-property-switcher]');
    propertySwitcher?.addEventListener('change', () => {
        const url = new URL(window.location.href);
        url.searchParams.set('propertyId', propertySwitcher.value);
        window.location.assign(`${url.pathname}${url.search}${url.hash}`);
    });

    // Room content is rendered by Vue, but its legacy template used an unscoped /rooms/{slug}
    // preview. Rewrite the generated link and visual slug prefix to the property's public site.
    const roomContentData = document.getElementById('room-content-data');
    const roomContentRoot = document.getElementById('room-content-page');
    if (roomContentData && roomContentRoot) {
        let publicBasePath = '';
        try {
            publicBasePath = JSON.parse(roomContentData.textContent || '{}').publicBasePath || '';
        } catch {
            publicBasePath = '';
        }

        if (publicBasePath) {
            const syncRoomPublicLinks = () => {
                const preview = roomContentRoot.querySelector('.room-publish-card .public-text-link');
                if (preview) {
                    const legacyHref = preview.getAttribute('href') || '';
                    const slug = legacyHref.startsWith('/rooms/') ? legacyHref.slice('/rooms/'.length) : '';
                    if (slug) preview.setAttribute('href', `${publicBasePath}/rooms/${slug}`);
                }

                const slugPrefix = roomContentRoot.querySelector('.slug-input-wrap > span');
                if (slugPrefix && slugPrefix.textContent !== `${publicBasePath}/rooms/`) {
                    slugPrefix.textContent = `${publicBasePath}/rooms/`;
                }
            };

            syncRoomPublicLinks();
            const observer = new MutationObserver(syncRoomPublicLinks);
            observer.observe(roomContentRoot, { childList: true, subtree: true, attributes: true, attributeFilter: ['href'] });
        }
    }

    // Website CMS has a server-rendered legacy root preview link. Point it at the current
    // property's canonical scoped route instead of always opening DELONG at '/'.
    const siteCmsData = document.getElementById('site-cms-data');
    const siteCmsRoot = document.getElementById('site-cms');
    if (siteCmsData && siteCmsRoot) {
        try {
            const siteData = JSON.parse(siteCmsData.textContent || '{}');
            const publicBasePath = siteData.publicBasePath || '';
            if (publicBasePath) {
                const preview = siteCmsRoot.querySelector('.site-page-head a[href="/"]');
                if (preview) preview.setAttribute('href', publicBasePath);
            }
        } catch {
            // Keep the original link if page data is malformed; Vue will report its own error.
        }
    }

    if (body.classList.contains('public-body')) {
        const isScopedPropertyPage = /^\/h\/[^/]+(?:\/|$)/i.test(window.location.pathname);
        if (!isScopedPropertyPage) {
            fetch('/api/public/global-branding', { headers: { Accept: 'application/json' } })
                .then(response => response.ok ? response.json() : null)
                .then(branding => {
                    if (!branding) return;
                    document.querySelectorAll('.public-site-brand').forEach(brand => {
                        const currentMedia = brand.querySelector('.public-site-brand-logo, .brand-mark');
                        if (branding.logoUrl && currentMedia) {
                            if (currentMedia.tagName === 'IMG') {
                                currentMedia.src = branding.logoUrl;
                                currentMedia.alt = branding.siteName || 'De Long Homestay';
                            } else {
                                const image = document.createElement('img');
                                image.className = 'public-site-brand-logo';
                                image.src = branding.logoUrl;
                                image.alt = branding.siteName || 'De Long Homestay';
                                currentMedia.replaceWith(image);
                            }
                        }

                        const copy = [...brand.children].find(element => element.tagName === 'SPAN' && !element.classList.contains('brand-mark'));
                        const name = copy?.querySelector('strong');
                        const tagline = copy?.querySelector('small');
                        if (name && branding.siteName) name.textContent = branding.siteName;
                        if (tagline) tagline.textContent = branding.tagline || '';
                    });

                    if (branding.faviconUrl) {
                        let favicon = document.querySelector('link[rel~="icon"]');
                        if (!favicon) {
                            favicon = document.createElement('link');
                            favicon.rel = 'icon';
                            document.head.appendChild(favicon);
                        }
                        favicon.href = branding.faviconUrl;
                    }

                    const ogSiteName = document.querySelector('meta[property="og:site_name"]');
                    if (ogSiteName && branding.siteName) ogSiteName.setAttribute('content', branding.siteName);
                })
                .catch(() => {
                    // Keep the server-rendered fallback if branding cannot be loaded.
                });
        }

        if (!document.querySelector('link[data-public-actions]')) {
            const stylesheet = document.createElement('link');
            stylesheet.rel = 'stylesheet';
            stylesheet.href = '/css/public-actions.css?v=20260815-2';
            stylesheet.dataset.publicActions = 'true';
            document.head.appendChild(stylesheet);
        }

        // Use one clear public CTA label. Availability forms keep their more specific
        // “Xem phòng trống” wording, while links that start the booking journey say “Đặt phòng”.
        document.querySelectorAll('a.public-btn[href*="/booking"]').forEach(link => {
            if (/kiểm tra phòng/i.test((link.textContent || '').trim())) {
                link.textContent = 'Đặt phòng';
                link.setAttribute('aria-label', 'Đặt phòng');
            }
        });

        const getBookingTarget = card => {
            const detailLink = card.querySelector('a[href*="/rooms/"]');
            if (!detailLink) return null;
            let url;
            try {
                url = new URL(detailLink.getAttribute('href') || '', window.location.origin);
            } catch {
                return null;
            }
            const match = url.pathname.match(/^\/h\/([^/]+)\/rooms\/([^/]+)\/?$/i);
            if (!match) return null;

            const siteSlug = decodeURIComponent(match[1]);
            const roomSlug = decodeURIComponent(match[2]);
            const badgeCode = card.querySelector('.public-room-code-badge')?.textContent?.trim();
            const posterMeta = card.querySelector('.public-room-poster small')?.textContent?.trim() || '';
            const posterCode = posterMeta.includes('·') ? posterMeta.split('·').pop().trim() : '';
            const roomKey = badgeCode || posterCode || roomSlug;
            return `/h/${encodeURIComponent(siteSlug)}/booking?room=${encodeURIComponent(roomKey)}`;
        };

        document.querySelectorAll('.public-room-card, .public-room-card-v2').forEach(card => {
            if (card.querySelector('.public-room-book-now')) return;
            const target = getBookingTarget(card);
            if (!target) return;

            const button = document.createElement('a');
            button.className = 'public-room-book-now';
            button.href = target;
            button.textContent = 'Đặt ngay';
            button.setAttribute('aria-label', 'Đặt ngay phòng này');

            const footer = card.querySelector('.public-room-card-v2-footer');
            const bodyTarget = card.querySelector('.public-room-body');
            if (footer) footer.appendChild(button);
            else if (bodyTarget) bodyTarget.appendChild(button);
        });

        // Property selection belongs to step 02 "Chọn phòng": choosing a branch and choosing
        // a room are one decision. Use the property's own configured cover image; never borrow
        // a room photo as a branch image.
        const bookingRoot = document.getElementById('public-booking-page');
        const bookingData = document.getElementById('public-booking-data');
        if (bookingRoot && bookingData && !bookingRoot.querySelector('.public-booking-property-choices')) {
            try {
                const data = JSON.parse(bookingData.textContent || '{}');
                const properties = Array.isArray(data.properties) ? data.properties : [];
                if (properties.length > 1) {
                    const step = [...bookingRoot.querySelectorAll('.public-step-card')]
                        .find(card => (card.querySelector('.public-step-head h2')?.textContent || '').trim() === 'Chọn phòng');
                    if (step) {
                        const choices = document.createElement('div');
                        choices.className = 'public-booking-property-choices';
                        choices.setAttribute('aria-label', 'Chọn cơ sở');

                        properties.forEach(property => {
                            const button = document.createElement('button');
                            button.type = 'button';
                            button.className = `public-booking-property-choice${property.siteSlug === data.siteSlug ? ' active' : ''}`;
                            button.dataset.siteSlug = property.siteSlug;
                            button.setAttribute('aria-pressed', property.siteSlug === data.siteSlug ? 'true' : 'false');
                            button.setAttribute('aria-label', `Chọn cơ sở ${property.siteName}`);

                            const media = document.createElement('span');
                            media.className = 'public-booking-property-media';
                            if (property.coverCardUrl) {
                                const image = document.createElement('img');
                                image.src = property.coverCardUrl;
                                image.alt = property.siteName || 'Cơ sở';
                                image.loading = 'lazy';
                                media.appendChild(image);
                            } else {
                                const fallback = document.createElement('span');
                                fallback.className = 'public-booking-property-fallback';
                                fallback.textContent = (property.siteName || 'CS')
                                    .split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase() || 'CS';
                                media.appendChild(fallback);
                            }

                            const copy = document.createElement('span');
                            copy.className = 'public-booking-property-choice-copy';
                            const name = document.createElement('strong');
                            name.textContent = property.siteName;
                            const count = document.createElement('small');
                            count.textContent = `${property.roomCount} phòng`;
                            copy.append(name, count);

                            const state = document.createElement('span');
                            state.className = 'public-booking-property-state';
                            state.textContent = property.siteSlug === data.siteSlug ? 'Đang chọn' : 'Chọn';

                            button.append(media, copy, state);
                            choices.appendChild(button);
                        });

                        // Vue mounts after shell.js and rebuilds the booking root's children. A
                        // delegated handler on the stable root survives that mount; listeners on
                        // the pre-mount buttons do not.
                        bookingRoot.addEventListener('click', event => {
                            if (!(event.target instanceof Element)) return;
                            const button = event.target.closest('.public-booking-property-choice[data-site-slug]');
                            if (!button || !bookingRoot.contains(button)) return;
                            const targetSlug = button.dataset.siteSlug;
                            if (!targetSlug || targetSlug === data.siteSlug) return;

                            const dateInput = bookingRoot.querySelector('input[type="date"]');
                            const currentDate = dateInput?.value || data.date || '';
                            const query = new URLSearchParams();
                            if (currentDate) query.set('date', currentDate);
                            const suffix = query.toString();
                            window.location.assign(`/h/${encodeURIComponent(targetSlug)}/booking${suffix ? `?${suffix}` : ''}`);
                        });

                        // Keep the branch chooser before Vue's loading / error / room-list
                        // v-if/v-else-if chain. Inserting it directly before the room list breaks
                        // directive adjacency and causes Vue compiler error 30 when 2+ properties exist.
                        step.querySelector('.public-step-head')?.insertAdjacentElement('afterend', choices);
                    }
                }
            } catch {
                // Booking remains usable if optional property-card data is malformed.
            }
        }
    }

    window.addEventListener('delong:properties-changed', () => window.location.reload());

    window.matchMedia('(min-width: 981px)').addEventListener('change', event => {
        if (event.matches) setOpen(false);
    });
})();
