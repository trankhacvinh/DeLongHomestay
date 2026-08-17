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

    function label(shell, key) {
        return ({
            home: shell.homeLabel,
            rooms: shell.roomsLabel,
            branches: shell.branchesLabel,
            booking: shell.bookingLabel,
            lookup: shell.lookupLabel
        })[key] || key;
    }

    function visible(shell, key) {
        if (key === 'home') return shell.showHome !== false;
        if (key === 'rooms') return shell.showRooms !== false;
        if (key === 'branches') return shell.showBranches !== false;
        return true;
    }

    function applyNav(container, shell) {
        if (!container) return;
        const anchors = [...container.children].filter(x => x.tagName === 'A');
        const byKey = new Map();
        anchors.forEach(anchor => {
            const key = keyForAnchor(anchor);
            if (!key) return;
            if (!byKey.has(key)) byKey.set(key, anchor);
            anchor.textContent = label(shell, key);
            anchor.hidden = !visible(shell, key);
        });

        const order = Array.isArray(shell.navigationOrder) ? shell.navigationOrder : ['home', 'rooms', 'branches', 'booking', 'lookup'];
        order.forEach(key => {
            const anchor = byKey.get(key);
            if (anchor) container.appendChild(anchor);
        });
    }

    function applyFooter(shell) {
        const footer = document.querySelector('.public-hospitality-footer');
        if (!footer) return;

        const intro = footer.querySelector('.public-footer-brand > p');
        if (intro) intro.textContent = shell.footerIntro || intro.textContent;
        const book = footer.querySelector('.public-footer-book');
        if (book) book.textContent = shell.footerBookingText || book.textContent;

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
        if (cta) cta.textContent = shell.headerCtaText || shell.bookingLabel || cta.textContent;
        applyFooter(shell);
        document.documentElement.dataset.publicShellReady = 'true';
    }

    DeLongApi.get(endpoint).then(applyShell).catch(() => {
        // Keep server-rendered shell when configuration cannot be loaded.
    });
})();
