(function () {
    const triggers = Array.from(document.querySelectorAll('[data-gallery-open]'));
    const dialog = document.querySelector('[data-gallery-lightbox]');
    if (!triggers.length || !dialog) return;
    const image = dialog.querySelector('[data-gallery-lightbox-image]');
    const caption = dialog.querySelector('[data-gallery-lightbox-caption]');
    let index = 0;
    function show(next) {
        index = (next + triggers.length) % triggers.length;
        const trigger = triggers[index];
        image.src = trigger.dataset.src || '';
        image.alt = trigger.dataset.alt || '';
        caption.textContent = trigger.dataset.caption || trigger.dataset.alt || '';
        if (!dialog.open) dialog.showModal();
    }
    triggers.forEach((trigger, i) => trigger.addEventListener('click', () => show(i)));
    dialog.querySelector('[data-gallery-close]')?.addEventListener('click', () => dialog.close());
    dialog.querySelector('[data-gallery-prev]')?.addEventListener('click', () => show(index - 1));
    dialog.querySelector('[data-gallery-next]')?.addEventListener('click', () => show(index + 1));
    dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
    dialog.addEventListener('keydown', event => { if (event.key === 'ArrowLeft') { event.preventDefault(); show(index - 1); } if (event.key === 'ArrowRight') { event.preventDefault(); show(index + 1); } });
})();
