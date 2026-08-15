(function () {
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

    const userChip = document.querySelector('.admin-topbar .user-chip');
    if (userChip && !document.querySelector('.topbar-password-link')) {
        const passwordLink = document.createElement('a');
        const returnUrl = `${window.location.pathname}${window.location.search}`;
        passwordLink.className = 'btn btn-ghost btn-sm topbar-password-link';
        passwordLink.href = `/Account/ChangePassword?returnUrl=${encodeURIComponent(returnUrl)}`;
        passwordLink.title = 'Đổi mật khẩu';
        passwordLink.setAttribute('aria-label', 'Đổi mật khẩu');
        passwordLink.innerHTML = '<svg aria-hidden="true"><use href="#i-settings"></use></svg><span>Đổi mật khẩu</span>';
        userChip.insertAdjacentElement('afterend', passwordLink);
    }

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

                            if (property.siteSlug !== data.siteSlug) {
                                button.addEventListener('click', () => {
                                    const dateInput = bookingRoot.querySelector('input[type="date"]');
                                    const currentDate = dateInput?.value || data.date || '';
                                    const query = new URLSearchParams();
                                    if (currentDate) query.set('date', currentDate);
                                    const suffix = query.toString();
                                    window.location.assign(`/h/${encodeURIComponent(property.siteSlug)}/booking${suffix ? `?${suffix}` : ''}`);
                                });
                            }
                        });

                        const roomList = step.querySelector('.public-booking-room-list');
                        if (roomList) roomList.insertAdjacentElement('beforebegin', choices);
                        else step.querySelector('.public-step-head')?.insertAdjacentElement('afterend', choices);
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
