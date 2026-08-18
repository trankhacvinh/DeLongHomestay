(function () {
    const root = document.querySelector('[data-seo-center]');
    if (!root) return;

    const search = root.querySelector('[data-seo-search]');
    const filters = [...root.querySelectorAll('[data-seo-filter]')];
    const items = [...root.querySelectorAll('[data-seo-item]')];
    const empty = root.querySelector('[data-seo-no-results]');
    let activeFilter = 'all';

    function matchesFilter(item) {
        if (activeFilter === 'all') return true;
        if (activeFilter === 'issue') return item.dataset.issue === '1';
        return item.dataset.type === activeFilter;
    }

    function sync() {
        const query = (search?.value || '').trim().toLocaleLowerCase('vi');
        let visible = 0;
        items.forEach(item => {
            const haystack = item.dataset.search || '';
            const show = matchesFilter(item) && (!query || haystack.includes(query));
            item.hidden = !show;
            if (show) visible += 1;
        });
        if (empty) empty.hidden = visible > 0 || items.length === 0;
    }

    search?.addEventListener('input', sync);
    filters.forEach(button => button.addEventListener('click', () => {
        activeFilter = button.dataset.seoFilter || 'all';
        filters.forEach(item => item.classList.toggle('active', item === button));
        sync();
    }));

    sync();
})();
