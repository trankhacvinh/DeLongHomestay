(function () {
    if (!document.body?.classList.contains('admin-body')) return;

    const sidebar = document.querySelector('.sidebar');
    const nav = sidebar?.querySelector('.sidebar-nav');
    const foot = sidebar?.querySelector('.sidebar-foot');
    if (!sidebar || !nav || !foot) return;

    nav.querySelectorAll('a.side-link').forEach(link => {
        if ((link.textContent || '').trim().toLowerCase() === 'xem website') link.remove();
    });

    if (!nav.querySelector('[data-sidebar-website-top]')) {
        const group = document.createElement('div');
        group.className = 'nav-group sidebar-website-quick';
        group.dataset.sidebarWebsiteTop = '1';
        group.innerHTML = '<a class="side-link sidebar-website-link" href="/" target="_blank" rel="noopener"><svg><use href="#i-external"></use></svg><span>Xem website</span><small>↗</small></a>';
        nav.insertBefore(group, nav.firstElementChild);
    }

    if (!foot.querySelector('[data-sidebar-website-foot]')) {
        const link = document.createElement('a');
        link.className = 'sidebar-foot-website';
        link.dataset.sidebarWebsiteFoot = '1';
        link.href = '/';
        link.target = '_blank';
        link.rel = 'noopener';
        link.innerHTML = '<svg><use href="#i-external"></use></svg><span><strong>Xem website</strong><small>Mở trang khách trong tab mới</small></span><b>↗</b>';
        foot.appendChild(link);
    }
})();
