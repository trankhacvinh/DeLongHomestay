(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const path = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = path.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const endpoint = `/api/public/site-shell${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    function keyForAnchor(anchor) {
        const href = anchor?.getAttribute('href');
        if (!href) return null;
        let url;
        try { url = new URL(href, window.location.origin); }
        catch { return null; }
        const pathname = url.pathname.replace(/\/+$/, '') || '/';
        if (/\/booking\/lookup$/i.test(pathname)) return 'lookup';
        if (/\/booking$/i.test(pathname)) return 'booking';
        if (url.hash === '#co-so') return 'branches';
        if (/\/rooms$/i.test(pathname)) return 'rooms';
        if (pathname === '/' || /^\/h\/[^/]+$/i.test(pathname)) return 'home';
        return null;
    }

    function resolveUrl(value) {
        const url = String(value || '').trim();
        if (!url.startsWith('@')) return url || '#';
        const prefix = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
        switch (url.toLowerCase()) {
            case '@home': return prefix || '/';
            case '@rooms': return prefix ? `${prefix}/rooms` : '/rooms';
            case '@branches': return '/#co-so';
            case '@booking': return prefix ? `${prefix}/booking` : '/booking';
            case '@lookup': return prefix ? `${prefix}/booking/lookup` : '/booking/lookup';
            default: return '#';
        }
    }

    function legacyItems(shell) {
        const order = Array.isArray(shell.navigationOrder) ? shell.navigationOrder : ['home', 'rooms', 'branches', 'booking', 'lookup'];
        const labels = {
            home: shell.homeLabel || 'Trang chủ',
            rooms: shell.roomsLabel || 'Phòng',
            branches: shell.branchesLabel || 'Cơ sở',
            booking: shell.bookingLabel || 'Đặt phòng',
            lookup: shell.lookupLabel || 'Tra cứu'
        };
        const urls = { home: '@home', rooms: '@rooms', branches: '@branches', booking: '@booking', lookup: '@lookup' };
        return order.map(id => ({
            id,
            label: labels[id] || id,
            url: urls[id] || '#',
            isVisible: id === 'home' ? shell.showHome !== false : id === 'rooms' ? shell.showRooms !== false : id === 'branches' ? shell.showBranches !== false : true,
            openInNewTab: false,
            isSystem: true
        }));
    }

    function navigationItems(shell) {
        return Array.isArray(shell.navigationItems) ? shell.navigationItems : legacyItems(shell);
    }

    function applyNav(container, shell) {
        if (!container) return;
        const existingAnchors = [...container.children].filter(x => x.tagName === 'A');
        const branchesAvailable = existingAnchors.some(anchor => keyForAnchor(anchor) === 'branches') || !!document.querySelector('#co-so');
        existingAnchors.forEach(anchor => anchor.remove());

        navigationItems(shell).forEach(item => {
            if (!item || item.isVisible === false) return;
            if (item.id === 'branches' && String(item.url || '').toLowerCase() === '@branches' && !branchesAvailable) return;
            const anchor = document.createElement('a');
            anchor.href = resolveUrl(item.url);
            anchor.textContent = item.label || 'Liên kết';
            anchor.dataset.shellNavId = item.id || '';
            if (item.openInNewTab) {
                anchor.target = '_blank';
                anchor.rel = 'noopener noreferrer';
            }
            container.appendChild(anchor);
        });
    }

    function applyFooter(shell) {
        const footer = document.querySelector('.public-hospitality-footer');
        if (!footer) return;

        const intro = footer.querySelector('.public-footer-brand > p');
        if (intro) intro.textContent = shell.footerIntro || intro.textContent;
        const book = footer.querySelector('.public-footer-book');
        if (book) {
            book.textContent = shell.footerBookingText || book.textContent;
            book.href = resolveUrl(shell.footerBookingUrl || '@booking');
        }

        const columns = [...footer.querySelectorAll('.public-footer-column:not(.public-footer-contact)')];
        const explore = columns[0];
        if (explore) {
            const title = explore.querySelector(':scope > strong');
            if (title) title.textContent = shell.footerExploreTitle || title.textContent;
            applyNav(explore, shell);
        }

        const branches = columns[1];
        if (branches) {
            const title = branches.querySelector(':scope > strong');
            if (title) title.textContent = shell.footerBranchesTitle || title.textContent;
            branches.hidden = shell.showBranches === false;
        }

        const contact = footer.querySelector('.public-footer-contact');
        if (contact) {
            const title = contact.querySelector(':scope > strong');
            if (title) title.textContent = shell.footerContactTitle || title.textContent;
            contact.hidden = shell.showFooterContact === false;
        }

        const bottom = footer.querySelector('.public-footer-bottom');
        const bottomItems = bottom ? [...bottom.children] : [];
        if (bottomItems[1]) bottomItems[1].textContent = shell.footerBottomText || bottomItems[1].textContent;
    }

    function applyShell(shell) {
        if (!shell) return;
        applyNav(document.querySelector('.public-site-links'), shell);
        const cta = document.querySelector('.public-site-actions .public-btn-primary');
        if (cta) {
            cta.textContent = shell.headerCtaText || shell.bookingLabel || cta.textContent;
            cta.href = resolveUrl(shell.headerCtaUrl || '@booking');
        }
        applyFooter(shell);
        document.documentElement.dataset.publicShellReady = 'true';
    }

    DeLongApi.get(endpoint).then(applyShell).catch(() => {
        // Keep server-rendered shell when configuration cannot be loaded.
    });
})();
