(function () {
    for (const link of document.querySelectorAll('[data-room-gallery]')) {
        const image = link.querySelector('img');
        if (!image) continue;
        let urls;
        try { urls = JSON.parse(link.dataset.roomGallery); } catch { continue; }
        if (!Array.isArray(urls)) continue;
        urls = [...new Set([image.getAttribute('src'), ...urls].filter(Boolean))];
        if (urls.length < 2) continue;

        const wrapper = document.createElement('div');
        wrapper.className = 'public-room-gallery';
        link.before(wrapper);
        wrapper.append(link);
        link.style.touchAction = 'pan-y';
        image.draggable = false;
        let index = 0;
        let pointer = null;
        let suppressClick = false;
        const counter = document.createElement('span');
        counter.className = 'public-room-gallery-count';
        counter.setAttribute('aria-live', 'polite');
        const hint = document.createElement('span');
        hint.className = 'public-room-gallery-hint';
        hint.textContent = 'Vuốt để xem ảnh';
        wrapper.append(counter, hint);

        function show(offset) {
            index = (index + offset + urls.length) % urls.length;
            image.src = urls[index];
            counter.textContent = `${index + 1}/${urls.length} ảnh`;
        }
        for (const [offset, label, glyph] of [[-1, 'Ảnh trước', '‹'], [1, 'Ảnh tiếp theo', '›']]) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = `public-room-gallery-nav ${offset < 0 ? 'prev' : 'next'}`;
            button.setAttribute('aria-label', label);
            button.textContent = glyph;
            button.addEventListener('click', () => show(offset));
            wrapper.append(button);
        }
        link.addEventListener('pointerdown', event => {
            if (!event.isPrimary || event.button !== 0) return;
            suppressClick = false;
            pointer = { id: event.pointerId, x: event.clientX, y: event.clientY };
        });
        link.addEventListener('pointermove', event => {
            if (!pointer || pointer.id !== event.pointerId) return;
            const dx = event.clientX - pointer.x;
            const dy = event.clientY - pointer.y;
            if (Math.abs(dx) > 12 && Math.abs(dx) > Math.abs(dy)) {
                suppressClick = true;
                if (!link.hasPointerCapture(event.pointerId)) link.setPointerCapture(event.pointerId);
            }
        });
        link.addEventListener('pointerup', event => {
            if (!pointer || pointer.id !== event.pointerId) return;
            const dx = event.clientX - pointer.x;
            const dy = event.clientY - pointer.y;
            if (Math.abs(dx) >= 40 && Math.abs(dx) > Math.abs(dy) * 1.3) show(dx < 0 ? 1 : -1);
            pointer = null;
        });
        link.addEventListener('pointercancel', () => { pointer = null; suppressClick = false; });
        link.addEventListener('click', event => {
            if (!suppressClick) return;
            event.preventDefault();
            event.stopImmediatePropagation();
            suppressClick = false;
        }, true);
        link.addEventListener('dragstart', event => event.preventDefault());
        show(0);
    }
})();

// Room lightbox: clicking a room photo opens every photo of that room full size
// (cards carry data-room-lightbox = JSON list of large image URLs). Swipes keep working as before.
(function () {
    const cards = Array.from(document.querySelectorAll('[data-room-lightbox]')).filter(card => card.dataset?.roomLightbox);
    if (!cards.length || !document.body) return;
    const dialog = document.createElement('dialog');
    dialog.className = 'public-gallery-lightbox public-room-lightbox';
    dialog.setAttribute('aria-label', 'Ảnh phòng');
    dialog.innerHTML = '<button class="public-gallery-lightbox-close" type="button" data-close aria-label="Đóng">×</button><button class="public-gallery-lightbox-nav prev" type="button" data-prev aria-label="Ảnh trước">‹</button><figure><img alt="" /><figcaption></figcaption></figure><button class="public-gallery-lightbox-nav next" type="button" data-next aria-label="Ảnh tiếp theo">›</button>';
    document.body.appendChild(dialog);
    const image = dialog.querySelector('img');
    const caption = dialog.querySelector('figcaption');
    let photos = [];
    let title = '';
    let index = 0;

    function show(next) {
        if (!photos.length) return;
        index = (next + photos.length) % photos.length;
        image.src = photos[index];
        image.alt = `${title} – ảnh ${index + 1}`;
        caption.textContent = `${title} · ${index + 1}/${photos.length}`;
        dialog.querySelector('[data-prev]').hidden = photos.length < 2;
        dialog.querySelector('[data-next]').hidden = photos.length < 2;
        if (!dialog.open) dialog.showModal();
    }

    cards.forEach(card => {
        let list;
        try { list = JSON.parse(card.dataset.roomLightbox || '[]'); } catch { list = []; }
        if (!Array.isArray(list) || !list.length) return;
        const art = card.querySelector('.public-room-art');
        if (!art) return;
        art.classList.add('has-lightbox');
        art.setAttribute('aria-label', `Xem ảnh phòng ${card.dataset.roomLightboxTitle || ''}`.trim());
        art.addEventListener('click', event => {
            if (event.defaultPrevented) return; // a swipe already handled this pointer gesture
            event.preventDefault();
            const current = art.querySelector('img')?.getAttribute('src') || '';
            const counter = card.querySelector('.public-room-gallery-count')?.textContent || '';
            const shown = Number.parseInt(counter, 10);
            photos = list;
            title = card.dataset.roomLightboxTitle || '';
            const byName = list.findIndex(url => current && url.split('?')[0].split('/').pop() === current.split('?')[0].split('/').pop());
            show(byName >= 0 ? byName : (Number.isFinite(shown) ? shown - 1 : 0));
        });
    });

    dialog.querySelector('[data-close]').addEventListener('click', () => dialog.close());
    dialog.querySelector('[data-prev]').addEventListener('click', () => show(index - 1));
    dialog.querySelector('[data-next]').addEventListener('click', () => show(index + 1));
    dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
    dialog.addEventListener('keydown', event => {
        if (event.key === 'ArrowLeft') { event.preventDefault(); show(index - 1); }
        if (event.key === 'ArrowRight') { event.preventDefault(); show(index + 1); }
    });
    let startX = null;
    dialog.addEventListener('pointerdown', event => { startX = event.clientX; });
    dialog.addEventListener('pointerup', event => {
        if (startX === null) return;
        const dx = event.clientX - startX;
        startX = null;
        if (Math.abs(dx) > 50) show(index + (dx < 0 ? 1 : -1));
    });
})();
