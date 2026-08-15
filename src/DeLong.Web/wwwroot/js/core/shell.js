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

    window.addEventListener('delong:properties-changed', () => window.location.reload());

    window.matchMedia('(min-width: 981px)').addEventListener('change', event => {
        if (event.matches) setOpen(false);
    });
})();
