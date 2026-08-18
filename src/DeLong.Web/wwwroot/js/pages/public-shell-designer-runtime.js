(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const path = location.pathname.replace(/\/+$/, '') || '/';
    const scoped = path.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';
    const designerEndpoint = `/api/public/site-designer${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const shellEndpoint = `/api/public/site-shell${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;

    const resolveUrl = value => {
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
    };

    function shellItems(shell) {
        if (Array.isArray(shell?.navigationItems)) return shell.navigationItems;
        const order = Array.isArray(shell?.navigationOrder) ? shell.navigationOrder : ['home', 'rooms', 'branches', 'booking', 'lookup'];
        const labels = {
            home: shell?.homeLabel || 'Trang chủ', rooms: shell?.roomsLabel || 'Phòng', branches: shell?.branchesLabel || 'Cơ sở',
            booking: shell?.bookingLabel || 'Đặt phòng', lookup: shell?.lookupLabel || 'Tra cứu'
        };
        const urls = { home: '@home', rooms: '@rooms', branches: '@branches', booking: '@booking', lookup: '@lookup' };
        return order.map(id => ({ id, label: labels[id] || id, url: urls[id] || '#', isVisible: true, openInNewTab: false }));
    }

    function toolbarOffset() {
        const toolbar = document.querySelector('.pve-toolbar:not([hidden]), .pve-context-toolbar:not([hidden])');
        if (!toolbar || document.body.classList.contains('pve-toolbar-hidden')) return 0;
        const style = getComputedStyle(toolbar);
        if (style.position !== 'fixed' && style.position !== 'sticky') return 0;
        return Math.max(0, Math.round(toolbar.getBoundingClientRect().height));
    }

    function applyHeader(config) {
        const header = document.querySelector('.public-site-header');
        if (!header || !config) return;
        header.classList.add('psd-header-managed');
        header.classList.toggle('psd-header-sticky', config.sticky !== false);
        header.classList.toggle('psd-header-static', config.sticky === false);
        const vars = {
            '--psd-header-bg': config.background || '#fbfaf6',
            '--psd-header-text': config.textColor || '#172422',
            '--psd-header-border': config.borderColor || '#d5e0dc',
            '--psd-header-sticky-bg': config.stickyBackground || config.background || '#fbfaf6',
            '--psd-header-sticky-text': config.stickyTextColor || config.textColor || '#172422',
            '--psd-header-sticky-border': config.stickyBorderColor || config.borderColor || '#d5e0dc'
        };
        Object.entries(vars).forEach(([key, value]) => header.style.setProperty(key, value));
        header.dataset.psdShadow = config.shadow ? '1' : '0';
        header.dataset.psdBlur = config.blur === false ? '0' : '1';
        header.dataset.psdStickyShadow = config.stickyShadow ? '1' : '0';
        header.dataset.psdStickyBlur = config.stickyBlur === false ? '0' : '1';

        const sync = () => {
            header.style.setProperty('--psd-header-top', `${config.sticky === false ? 0 : toolbarOffset()}px`);
            header.classList.toggle('psd-is-stuck', config.sticky !== false && window.scrollY > 18);
        };
        sync();
        addEventListener('scroll', sync, { passive: true });
        addEventListener('resize', sync);
        new MutationObserver(sync).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'hidden'] });
    }

    function snapshotFooter() {
        const footer = document.querySelector('.public-hospitality-footer');
        const grid = footer?.querySelector('.public-hospitality-footer-grid');
        const brand = grid?.querySelector('.public-footer-brand-link')?.cloneNode(true) || null;
        const branchLinks = [...(grid?.querySelectorAll('.public-footer-column a') || [])]
            .filter(anchor => /^\/h\/[^/?#]+\/?$/i.test(new URL(anchor.getAttribute('href') || '', location.origin).pathname))
            .map(anchor => ({ label: anchor.textContent?.trim() || 'Cơ sở', url: anchor.getAttribute('href') || '#' }));
        const contact = grid?.querySelector('.public-footer-contact');
        const contactItems = contact ? [...contact.children].filter(node => node.tagName !== 'STRONG').map(node => ({
            tag: node.tagName.toLowerCase(),
            text: node.textContent?.trim() || '',
            href: node.tagName === 'A' ? node.getAttribute('href') || '' : ''
        })) : [];
        return { footer, grid, brand, branchLinks, contactItems };
    }

    function appendLink(parent, label, url, newTab) {
        const anchor = document.createElement('a');
        anchor.href = resolveUrl(url);
        anchor.textContent = label || 'Liên kết';
        if (newTab) { anchor.target = '_blank'; anchor.rel = 'noopener noreferrer'; }
        parent.appendChild(anchor);
    }

    function renderElement(element, column, sources, shell) {
        if (!element) return;
        const wrap = document.createElement('div');
        wrap.className = `psd-footer-element psd-footer-element-${element.type || 'text'} psd-align-${element.align || 'left'}`;
        wrap.dataset.footerElementId = element.id || '';
        const type = String(element.type || '').toLowerCase();

        if (type === 'brand') {
            if (sources.brand) wrap.appendChild(sources.brand.cloneNode(true));
        } else if (type === 'heading') {
            const heading = document.createElement(element.variant === 'h2' ? 'h2' : element.variant === 'h4' ? 'h4' : 'h3');
            heading.textContent = element.text || '';
            wrap.appendChild(heading);
        } else if (type === 'text') {
            const text = document.createElement('p');
            text.textContent = element.text || '';
            wrap.appendChild(text);
        } else if (type === 'image') {
            if (element.imageUrl) {
                const img = document.createElement('img');
                img.src = element.imageUrl; img.alt = element.altText || ''; img.loading = 'lazy';
                if (element.url) {
                    const link = document.createElement('a'); link.href = resolveUrl(element.url); link.appendChild(img); wrap.appendChild(link);
                } else wrap.appendChild(img);
            }
        } else if (type === 'button') {
            const link = document.createElement('a');
            link.className = `psd-footer-button psd-footer-button-${element.variant || 'default'}`;
            link.href = resolveUrl(element.url || '@booking');
            link.textContent = element.text || 'Xem thêm';
            wrap.appendChild(link);
        } else if (type === 'links') {
            const list = document.createElement('div'); list.className = 'psd-footer-link-list';
            (element.links || []).forEach(item => appendLink(list, item.label, item.url, item.openInNewTab));
            wrap.appendChild(list);
        } else if (type === 'menu') {
            const list = document.createElement('div'); list.className = 'psd-footer-link-list';
            shellItems(shell).filter(item => item?.isVisible !== false).forEach(item => {
                if (item.id === 'branches' && String(item.url || '').toLowerCase() === '@branches' && sources.branchLinks.length === 0) return;
                appendLink(list, item.label, item.url, item.openInNewTab);
            });
            wrap.appendChild(list);
        } else if (type === 'branches') {
            const list = document.createElement('div'); list.className = 'psd-footer-link-list';
            sources.branchLinks.forEach(item => appendLink(list, item.label, item.url, false));
            if (sources.branchLinks.length) wrap.appendChild(list);
        } else if (type === 'contact') {
            const list = document.createElement('div'); list.className = 'psd-footer-contact-list';
            sources.contactItems.forEach(item => {
                const node = document.createElement(item.tag === 'a' ? 'a' : 'span');
                node.textContent = item.text;
                if (item.tag === 'a') node.href = item.href;
                list.appendChild(node);
            });
            if (sources.contactItems.length) wrap.appendChild(list);
        } else if (type === 'divider') {
            wrap.appendChild(document.createElement('hr'));
        } else if (type === 'spacer') {
            wrap.classList.add(`psd-spacer-${element.variant || 'md'}`);
            wrap.setAttribute('aria-hidden', 'true');
        }
        if (wrap.childNodes.length || type === 'spacer') column.appendChild(wrap);
    }

    function renderFooter(designer, shell) {
        if (!designer?.footerBuilderEnabled || !Array.isArray(designer.footerRows) || designer.footerRows.length === 0) return;
        const sources = snapshotFooter();
        if (!sources.footer || !sources.grid) return;
        const builder = document.createElement('div');
        builder.className = 'psd-footer-builder';
        builder.setAttribute('data-psd-footer-builder', '1');

        designer.footerRows.forEach(row => {
            const section = document.createElement('section');
            section.className = `psd-footer-row psd-footer-padding-${row.padding || 'md'} psd-footer-gap-${row.gap || 'md'}`;
            section.dataset.footerRowId = row.id || '';
            section.style.setProperty('--psd-footer-row-bg', row.background || 'transparent');
            section.style.setProperty('--psd-footer-row-text', row.textColor || '#d7e3df');
            const inner = document.createElement('div');
            inner.className = 'public-container psd-footer-row-grid';
            (row.columns || []).forEach(columnData => {
                const column = document.createElement('div');
                column.className = 'psd-footer-column';
                column.style.setProperty('--psd-footer-span', Math.max(1, Math.min(12, Number(columnData.span) || 12)));
                column.dataset.footerColumnId = columnData.id || '';
                (columnData.elements || []).forEach(element => renderElement(element, column, sources, shell));
                inner.appendChild(column);
            });
            section.appendChild(inner);
            builder.appendChild(section);
        });

        sources.grid.replaceWith(builder);
        sources.footer.classList.add('psd-footer-custom');
    }

    Promise.all([DeLongApi.get(designerEndpoint), DeLongApi.get(shellEndpoint).catch(() => null)])
        .then(([designer, shell]) => {
            applyHeader(designer?.header);
            renderFooter(designer, shell);
            document.documentElement.dataset.publicShellDesignerReady = 'true';
        })
        .catch(() => {
            // Keep the original server-rendered shell if designer settings cannot be loaded.
        });
})();
