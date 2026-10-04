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
