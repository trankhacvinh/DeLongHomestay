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
    dialog.addEventListener('close', () => {
        document.body.style.overflow = previousOverflow;
        trigger?.focus();
    });
})();
