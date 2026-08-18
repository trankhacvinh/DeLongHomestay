(function () {
    const root = document.querySelector('[data-seo-center]');
    if (!root) return;
    const search = root.querySelector('[data-seo-search]');
    const cards = [...root.querySelectorAll('[data-seo-page]')];
    if (!search || !cards.length) return;

    function sync() {
        const query = (search.value || '').trim().toLocaleLowerCase('vi');
        cards.forEach(card => {
            const haystack = card.dataset.search || '';
            card.hidden = !!query && !haystack.includes(query);
        });
    }

    search.addEventListener('input', sync);
})();
