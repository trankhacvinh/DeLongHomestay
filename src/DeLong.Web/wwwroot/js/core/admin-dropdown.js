// Minimal dropdown for the admin topbar (no Bootstrap JS: Vue owns modal/DOM state on pages).
(() => {
    'use strict';
    const roots = document.querySelectorAll('[data-dl-dropdown]');
    if (!roots.length) return;

    const setOpen = (root, open) => {
        root.classList.toggle('open', open);
        root.querySelector('[data-dl-dropdown-toggle]')?.setAttribute('aria-expanded', open ? 'true' : 'false');
    };
    const closeAll = except => roots.forEach(root => { if (root !== except) setOpen(root, false); });

    roots.forEach(root => {
        root.querySelector('[data-dl-dropdown-toggle]')?.addEventListener('click', event => {
            event.stopPropagation();
            const open = !root.classList.contains('open');
            closeAll(root);
            setOpen(root, open);
        });
    });
    document.addEventListener('click', event => {
        roots.forEach(root => { if (!root.contains(event.target)) setOpen(root, false); });
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const openRoot = [...roots].find(root => root.classList.contains('open'));
        if (!openRoot) return;
        setOpen(openRoot, false);
        openRoot.querySelector('[data-dl-dropdown-toggle]')?.focus();
    });
})();
