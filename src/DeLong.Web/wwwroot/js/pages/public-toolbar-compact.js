(function () {
    if (!document.body?.classList.contains('public-body')) return;

    const state = { queued: false };

    function createGroup(actions, key, label) {
        let group = actions.querySelector(`:scope > [data-toolbar-group="${key}"]`);
        if (group) return group;
        group = document.createElement('div');
        group.className = 'pve-toolbar-group';
        group.dataset.toolbarGroup = key;
        group.innerHTML = `<button type="button" class="pve-toolbar-group-trigger" data-toolbar-group-trigger aria-expanded="false">${label}</button><div class="pve-toolbar-menu" data-toolbar-menu hidden></div>`;
        const close = actions.querySelector(':scope > [data-hide]');
        actions.insertBefore(group, close || null);
        const trigger = group.querySelector('[data-toolbar-group-trigger]');
        trigger.addEventListener('click', event => {
            event.stopPropagation();
            const opening = group.querySelector('[data-toolbar-menu]').hidden;
            closeAll();
            if (opening) {
                group.classList.add('open');
                group.querySelector('[data-toolbar-menu]').hidden = false;
                trigger.setAttribute('aria-expanded', 'true');
            }
        });
        group.querySelector('[data-toolbar-menu]').addEventListener('click', event => {
            if (event.target.closest('button,a')) setTimeout(closeAll, 0);
        });
        return group;
    }

    function closeAll() {
        document.querySelectorAll('.pve-toolbar-group').forEach(group => {
            group.classList.remove('open');
            const menu = group.querySelector('[data-toolbar-menu]');
            const trigger = group.querySelector('[data-toolbar-group-trigger]');
            if (menu) menu.hidden = true;
            trigger?.setAttribute('aria-expanded', 'false');
        });
    }

    function ensureAdminAnchor(actions) {
        let anchor = actions.querySelector(':scope > .pve-toolbar-admin-anchor');
        if (anchor) return anchor;
        const admin = [...actions.querySelectorAll(':scope > a')].find(link => /^\/Admin\/?$/i.test(new URL(link.href, location.origin).pathname));
        if (!admin) return null;
        admin.classList.add('pve-toolbar-admin-anchor');
        anchor = admin;
        return anchor;
    }

    function menuAdd(menu, node) {
        if (!node || node.closest('.pve-toolbar-menu') === menu) return;
        menu.appendChild(node);
    }

    function sync() {
        const bar = document.querySelector('.pve-toolbar');
        const actions = bar?.querySelector('.pve-toolbar-actions');
        if (!bar || !actions) return;
        bar.classList.add('pve-toolbar-compact');

        const adminAnchor = ensureAdminAnchor(actions);
        const content = createGroup(actions, 'content', 'Nội dung');
        const design = createGroup(actions, 'design', 'Thiết kế');
        const admin = createGroup(actions, 'admin', 'Quản trị');
        const contentMenu = content.querySelector('[data-toolbar-menu]');
        const designMenu = design.querySelector('[data-toolbar-menu]');
        const adminMenu = admin.querySelector('[data-toolbar-menu]');

        [...actions.children].forEach(node => {
            if (!(node instanceof HTMLElement)) return;
            if (node.matches('[data-toolbar-group], [data-edit-toggle], [data-add], [data-pml-manage], [data-hide], .pve-toolbar-admin-anchor')) return;

            if (node.matches('[data-gallery], [data-blog], [data-contextual-room], [data-shell-rates-rate-toolbar]')) return menuAdd(contentMenu, node);
            if (node.matches('[data-brand], [data-contextual-header], [data-contextual-footer], [data-shell-rates-toolbar]')) return menuAdd(designMenu, node);
            if (node.tagName === 'A') return menuAdd(adminMenu, node);
            if (node.tagName === 'BUTTON') return menuAdd(contentMenu, node);
        });

        if (adminAnchor && !adminMenu.querySelector('[data-toolbar-admin-clone]')) {
            const clone = adminAnchor.cloneNode(true);
            clone.classList.remove('pve-toolbar-admin-anchor');
            clone.dataset.toolbarAdminClone = '1';
            adminMenu.appendChild(clone);
        }

        content.hidden = !contentMenu.querySelector('button,a');
        design.hidden = !designMenu.querySelector('button,a');
        admin.hidden = !adminMenu.querySelector('button,a');
    }

    function queue() {
        if (state.queued) return;
        state.queued = true;
        requestAnimationFrame(() => {
            state.queued = false;
            sync();
        });
    }

    document.addEventListener('click', event => {
        if (!event.target.closest('.pve-toolbar-group')) closeAll();
    });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') closeAll(); });
    new MutationObserver(queue).observe(document.body, { childList: true, subtree: true });

    setTimeout(sync, 350);
    setTimeout(sync, 900);
})();
