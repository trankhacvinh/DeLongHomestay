(function () {
    const dialog = document.querySelector('#public-faq-dialog');
    if (!dialog) return;
    let trigger = null;
    let previousOverflow = '';
    document.querySelectorAll('[data-public-faq-open]').forEach(button => {
        button.addEventListener('click', () => {
            if (dialog.open) return;
            trigger = button;
            previousOverflow = document.body.style.overflow;
            dialog.showModal();
            document.body.style.overflow = 'hidden';
        });
    });
    dialog.querySelector('[data-public-faq-close]').addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    });
    // Search: filter questions and answers as the guest types (accent-insensitive).
    const search = dialog.querySelector('[data-public-faq-search]');
    const empty = dialog.querySelector('[data-public-faq-empty]');
    const items = [...dialog.querySelectorAll('.public-faq-dialog-content details')];
    const fold = text => (text || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
    const haystacks = items.map(item => fold(item.textContent));
    search?.addEventListener('input', () => {
        const query = fold(search.value.trim());
        let shown = 0;
        items.forEach((item, index) => {
            const match = !query || haystacks[index].includes(query);
            item.hidden = !match;
            if (match) shown += 1;
        });
        if (empty) empty.hidden = shown > 0;
    });
    dialog.querySelectorAll('[data-public-faq-close-link]').forEach(link => link.addEventListener('click', () => dialog.close()));
    dialog.addEventListener('close', () => {
        document.body.style.overflow = previousOverflow;
        trigger?.focus();
    });
})();
