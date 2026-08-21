(function () {
    if (!document.body?.classList.contains('admin-body')) return;

    const sidebar = document.querySelector('.sidebar');
    const nav = sidebar?.querySelector('.sidebar-nav');
    const foot = sidebar?.querySelector('.sidebar-foot');
    if (!sidebar || !nav || !foot) return;

    nav.querySelectorAll('a.side-link').forEach(link => {
        if ((link.textContent || '').trim().toLowerCase() === 'xem website') link.remove();
    });

    const calendarV1 = [...nav.querySelectorAll('a.side-link')].find(link => {
        try { return new URL(link.href, window.location.origin).pathname.toLowerCase() === '/admin/calendar'; }
        catch { return false; }
    });
    if (calendarV1) {
        const label = calendarV1.querySelector('span');
        if (label) label.textContent = 'Lịch phòng V1';
        calendarV1.dataset.sidebarCalendarV1 = '1';
        calendarV1.classList.toggle('active', window.location.pathname.toLowerCase() === '/admin/calendar');

        if (!nav.querySelector('[data-sidebar-calendar-v2]')) {
            const url = new URL(calendarV1.href, window.location.origin);
            url.pathname = '/Admin/CalendarV2';
            const link = document.createElement('a');
            link.className = 'side-link';
            if (window.location.pathname.toLowerCase() === '/admin/calendarv2') link.classList.add('active');
            link.href = `${url.pathname}${url.search}`;
            link.dataset.sidebarCalendarV2 = '1';
            const icon = calendarV1.querySelector('svg')?.outerHTML || '<svg><use href="#i-calendar"></use></svg>';
            link.innerHTML = `${icon}<span>Lịch phòng V2</span>`;
            calendarV1.after(link);
        }
    }

    if (!nav.querySelector('[data-sidebar-website-top]')) {
        const group = document.createElement('div');
        group.className = 'nav-group sidebar-website-quick';
        group.dataset.sidebarWebsiteTop = '1';
        group.innerHTML = '<a class="side-link sidebar-website-link" href="/" target="_blank" rel="noopener"><svg><use href="#i-external"></use></svg><span>Xem website</span><small>↗</small></a>';
        nav.insertBefore(group, nav.firstElementChild);
    }

    const siteLink = [...nav.querySelectorAll('a.side-link')].find(link => {
        const href = (link.getAttribute('href') || '').replace(/\/$/, '');
        return href === '/Admin/Site' || href === '/Admin/Site/Global' || href === '/Admin/Site/Media';
    });
    const websiteGroup = siteLink?.closest('.nav-group');

    if (websiteGroup && !nav.querySelector('a[href="/Admin/Site/Pages"]')) {
        const link = document.createElement('a');
        link.className = 'side-link';
        if (location.pathname.toLowerCase() === '/admin/site/pages') link.classList.add('active');
        link.href = '/Admin/Site/Pages';
        link.dataset.sidebarCustomPages = '1';
        const icon = siteLink?.querySelector('svg')?.outerHTML || '<svg><use href="#i-article"></use></svg>';
        link.innerHTML = `${icon}<span>Trang nội dung</span>`;
        const mediaLink = [...websiteGroup.querySelectorAll('a.side-link')].find(item => /media library/i.test(item.textContent || ''));
        if (mediaLink) mediaLink.before(link); else websiteGroup.appendChild(link);
    }

    if (websiteGroup && !nav.querySelector('a[href="/Admin/Site/Seo"]')) {
        const link = document.createElement('a');
        link.className = 'side-link';
        if (location.pathname.toLowerCase() === '/admin/site/seo') link.classList.add('active');
        link.href = '/Admin/Site/Seo';
        link.dataset.sidebarSeoCenter = '1';
        link.innerHTML = '<svg><use href="#i-chart"></use></svg><span>SEO & Xuất bản</span>';
        const mediaLink = [...websiteGroup.querySelectorAll('a.side-link')].find(item => /media library/i.test(item.textContent || ''));
        if (mediaLink) mediaLink.before(link); else websiteGroup.appendChild(link);
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
