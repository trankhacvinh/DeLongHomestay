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

    window.addEventListener('delong:properties-changed', () => window.location.reload());

    window.matchMedia('(min-width: 981px)').addEventListener('change', event => {
        if (event.matches) setOpen(false);
    });
})();
