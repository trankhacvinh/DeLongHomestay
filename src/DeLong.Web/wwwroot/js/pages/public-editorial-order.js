(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)$/i);
    const isHome = normalizedPath === '/' || !!scopedMatch;
    if (!isHome) return;

    const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
    const publicUrl = `/api/public/site/editorial-placement${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const main = document.querySelector('#main-content');
    if (!main) return;

    const TYPE_SELECTORS = [
        ['.public-cms-hero', 'Hero'],
        ['.public-branch-section', 'BranchGrid'],
        ['.public-cms-room-grid', 'RoomGrid'],
        ['.public-cms-availability', 'AvailabilitySearch'],
        ['.public-cms-feature', 'FeatureGrid'],
        ['.public-story-faq', 'Faq'],
        ['.public-story-location', 'Location'],
        ['.public-story-policies', 'PolicyGrid'],
        ['.public-cms-rich', 'RichText'],
        ['.public-cms-cta', 'Cta']
    ];

    let layoutState = { placement: { galleryAfter: 'end', blogAfter: 'end' }, sections: [] };
    let adminPlacementUrl = null;
    let contextPromise = null;
    let editorContext = null;

    function sectionKind(element) {
        if (element?.classList.contains('public-editorial-gallery')) return 'gallery';
        if (element?.classList.contains('public-editorial-blog')) return 'blog';
        return null;
    }

    function mapSectionIds(data) {
        const queues = new Map();
        (data.sections || []).forEach(section => {
            if (!queues.has(section.type)) queues.set(section.type, []);
            queues.get(section.type).push(section);
        });

        [...main.querySelectorAll(':scope > section')].forEach(element => {
            if (sectionKind(element)) return;
            const pair = TYPE_SELECTORS.find(([selector]) => element.matches(selector));
            if (!pair) return;
            const section = queues.get(pair[1])?.shift();
            if (section) element.dataset.publicLayoutSectionId = section.id;
        });
    }

    function elementForAnchor(anchor) {
        if (!anchor || anchor === 'end') return null;
        if (anchor === 'gallery') return main.querySelector(':scope > .public-editorial-gallery');
        if (anchor === 'blog') return main.querySelector(':scope > .public-editorial-blog');
        if (anchor.startsWith('section:')) {
            const id = anchor.slice('section:'.length);
            return [...main.querySelectorAll(':scope > section[data-public-layout-section-id]')]
                .find(element => String(element.dataset.publicLayoutSectionId).toLowerCase() === id.toLowerCase()) || null;
        }
        return null;
    }

    function firstHomeSection(except) {
        return [...main.querySelectorAll(':scope > section')].find(element => element !== except) || null;
    }

    function applyPlacement(data) {
        layoutState = data || layoutState;
        mapSectionIds(layoutState);
        const placement = layoutState.placement || {};
        const nodes = {
            gallery: main.querySelector(':scope > .public-editorial-gallery'),
            blog: main.querySelector(':scope > .public-editorial-blog')
        };
        const resolving = new Set();
        const resolved = new Set();

        function place(kind) {
            const element = nodes[kind];
            if (!element || resolved.has(kind)) return;
            if (resolving.has(kind)) {
                main.appendChild(element);
                resolved.add(kind);
                return;
            }

            resolving.add(kind);
            const anchor = String(placement[`${kind}After`] || 'end').toLowerCase();
            if (anchor === 'start') {
                const first = firstHomeSection(element);
                if (first) first.before(element); else main.appendChild(element);
            } else if (anchor === 'gallery' || anchor === 'blog') {
                if (anchor !== kind) {
                    place(anchor);
                    const anchorElement = nodes[anchor];
                    if (anchorElement) anchorElement.after(element); else main.appendChild(element);
                } else {
                    main.appendChild(element);
                }
            } else if (anchor.startsWith('section:')) {
                const anchorElement = elementForAnchor(anchor);
                if (anchorElement) anchorElement.after(element); else main.appendChild(element);
            } else {
                main.appendChild(element);
            }
            resolving.delete(kind);
            resolved.add(kind);
        }

        place('gallery');
        place('blog');
    }

    function anchorBefore(element) {
        const sections = [...main.querySelectorAll(':scope > section')];
        const index = sections.indexOf(element);
        for (let cursor = index - 1; cursor >= 0; cursor--) {
            const previous = sections[cursor];
            const kind = sectionKind(previous);
            if (kind) return kind;
            if (previous.dataset.publicLayoutSectionId) return `section:${previous.dataset.publicLayoutSectionId}`;
        }
        return 'start';
    }

    async function resolveAdminPlacementUrl() {
        if (adminPlacementUrl) return adminPlacementUrl;
        if (!contextPromise) {
            const contextUrl = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
            contextPromise = DeLongApi.get(contextUrl);
        }
        editorContext = await contextPromise;
        adminPlacementUrl = editorContext?.scope === 'global'
            ? '/api/admin/site/global/editorial/placement'
            : `/api/admin/properties/${editorContext.propertyId}/editorial/placement`;
        return adminPlacementUrl;
    }

    function toast(message, isError) {
        let node = document.querySelector('.pve-toast');
        if (!node) {
            node = document.createElement('div');
            node.className = 'pve-toast';
            document.body.appendChild(node);
        }
        node.className = `pve-toast ${isError ? 'error' : 'success'}`;
        node.textContent = message;
        node.hidden = false;
        clearTimeout(node._editorialTimer);
        node._editorialTimer = setTimeout(() => { node.hidden = true; }, 2800);
    }

    function reloadPreservingEdit() {
        if (!editorContext) return window.location.reload();
        const suffix = editorContext.scope === 'global' ? 'global' : editorContext.propertyId;
        try {
            sessionStorage.setItem(`delong:pve:editing:${suffix}`, '1');
            sessionStorage.setItem(`delong:pve:scroll:${suffix}`, String(Math.max(0, window.scrollY || 0)));
        } catch { }
        window.location.reload();
    }

    async function moveEditorial(element, delta) {
        const sections = [...main.querySelectorAll(':scope > section')];
        const index = sections.indexOf(element);
        const target = sections[index + delta];
        if (index < 0 || !target) {
            toast(delta < 0 ? 'Khối đã ở trên cùng.' : 'Khối đã ở dưới cùng.');
            return;
        }

        const previousPlacement = { ...(layoutState.placement || { galleryAfter: 'end', blogAfter: 'end' }) };
        if (delta < 0) target.before(element); else target.after(element);

        const gallery = main.querySelector(':scope > .public-editorial-gallery');
        const blog = main.querySelector(':scope > .public-editorial-blog');
        const nextPlacement = {
            galleryAfter: gallery ? anchorBefore(gallery) : previousPlacement.galleryAfter || 'end',
            blogAfter: blog ? anchorBefore(blog) : previousPlacement.blogAfter || 'end'
        };

        try {
            const url = await resolveAdminPlacementUrl();
            const saved = await DeLongApi.put(url, nextPlacement);
            layoutState.placement = saved || nextPlacement;
            reloadPreservingEdit();
        } catch (error) {
            layoutState.placement = previousPlacement;
            applyPlacement(layoutState);
            toast(error.message || 'Không thể lưu vị trí Gallery / Blog.', true);
        }
    }

    function enhanceEditorialControls() {
        main.querySelectorAll(':scope > .public-editorial-gallery, :scope > .public-editorial-blog').forEach(element => {
            const controls = element.querySelector(':scope > .pve-editorial-controls');
            if (!controls || controls.dataset.orderEnhanced === '1') return;
            controls.dataset.orderEnhanced = '1';
            const editButton = controls.querySelector('button');
            const up = document.createElement('button');
            const down = document.createElement('button');
            up.type = down.type = 'button';
            up.textContent = '↑'; down.textContent = '↓';
            up.title = 'Đưa khối lên'; down.title = 'Đưa khối xuống';
            up.dataset.editorialMove = '-1'; down.dataset.editorialMove = '1';
            controls.insertBefore(up, editButton || null);
            controls.insertBefore(down, editButton || null);
            up.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); moveEditorial(element, -1); });
            down.addEventListener('click', event => { event.preventDefault(); event.stopPropagation(); moveEditorial(element, 1); });
        });
    }

    const observer = new MutationObserver(() => enhanceEditorialControls());
    observer.observe(main, { childList: true, subtree: true });

    DeLongApi.get(publicUrl)
        .then(data => {
            applyPlacement(data || layoutState);
            enhanceEditorialControls();
        })
        .catch(() => enhanceEditorialControls());
})();
