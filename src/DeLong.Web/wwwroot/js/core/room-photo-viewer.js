(function () {
    const style = document.createElement('link');
    style.rel = 'stylesheet'; style.href = '/css/room-photo-viewer.css?v=20261108-1';
    document.head.appendChild(style);
    const cache = new Map();
    const roomData = new WeakMap();
    let version = 0, abort, gallery, images = [], index = 0;
    let dialogCount = 0, previousOverflow;

    function element(tag, text, className) {
        const node = document.createElement(tag);
        if (text) node.textContent = text;
        if (className) node.className = className;
        return node;
    }
    function dialog(title, trigger) {
        const node = element('dialog', null, 'room-photo-dialog');
        const head = element('header');
        const heading = element('h2', title);
        heading.id = `room-photo-title-${document.querySelectorAll('.room-photo-dialog').length}`;
        node.setAttribute('aria-labelledby', heading.id);
        const close = element('button', 'Đóng'); close.type = 'button';
        close.addEventListener('click', () => node.close());
        head.append(heading, close); node.append(head);
        if (dialogCount++ === 0) previousOverflow = document.documentElement.style.overflow;
        document.documentElement.style.overflow = 'hidden';
        node.addEventListener('close', () => {
            if (--dialogCount === 0) document.documentElement.style.overflow = previousOverflow;
            node.remove();
            if (trigger?.isConnected) trigger.focus({ preventScroll: true });
        }, { once: true });
        node.addEventListener('click', event => { if (event.target === node) node.close(); });
        document.body.appendChild(node); node.showModal();
        return { node, heading };
    }

    async function open(url, trigger) {
        if (!url) return;
        if (gallery?.open) gallery.close();
        const current = ++version;
        abort?.abort(); abort = new AbortController();
        const request = abort;
        const timeout = setTimeout(() => request.abort(), 12000);
        const { node, heading } = dialog('Ảnh phòng', trigger);
        gallery = node;
        const status = element('p', 'Đang tải ảnh phòng…', 'room-photo-status');
        status.setAttribute('role', 'status'); node.append(status);
        node.addEventListener('close', () => { if (version === current) { version++; abort?.abort(); images = []; } }, { once: true });
        try {
            let data = cache.get(url);
            if (!data || data.until < Date.now()) {
                const response = await fetch(url, { signal: abort.signal, credentials: 'same-origin', headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error('Không tải được ảnh phòng. Vui lòng đóng và thử lại.');
                data = { value: await response.json(), until: Date.now() + 60000 };
                if (version !== current || !node.open) return;
                if (cache.size >= 20) cache.delete(cache.keys().next().value);
                cache.set(url, data);
            }
            if (version !== current || !node.open) return;
            heading.textContent = `${data.value.name} · ${data.value.propertyName}`;
            images = data.value.images || []; index = 0;
            if (!images.length) { status.textContent = 'Phòng chưa có hình ảnh.'; return; }
            status.remove();
            const figure = element('figure');
            const image = element('img'); image.decoding = 'async';
            const caption = element('figcaption'); caption.setAttribute('aria-live', 'polite');
            const controls = element('nav');
            const previous = element('button', '‹ Ảnh trước'), next = element('button', 'Ảnh tiếp ›');
            previous.type = next.type = 'button';
            const show = delta => {
                index = (index + delta + images.length) % images.length;
                const photo = images[index];
                image.alt = photo.altText || `${data.value.name} · ảnh ${index + 1}`;
                image.onerror = () => {
                    image.onerror = () => { image.removeAttribute('src'); caption.textContent = `Ảnh ${index + 1}/${images.length} chưa tải được. Có thể chuyển ảnh tiếp theo.`; };
                    if (photo.thumbnailUrl) image.src = photo.thumbnailUrl;
                    else image.onerror();
                };
                image.src = photo.cardUrl || photo.thumbnailUrl;
                caption.textContent = `${index + 1}/${images.length} ảnh`;
            };
            previous.addEventListener('click', () => show(-1)); next.addEventListener('click', () => show(1));
            controls.append(previous, next); controls.hidden = images.length < 2;
            figure.append(image, caption); node.append(figure, controls);
            node.addEventListener('keydown', event => {
                if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); show(event.key === 'ArrowLeft' ? -1 : 1); }
            });
            let start;
            figure.addEventListener('pointerdown', event => { start = { x: event.clientX, y: event.clientY }; });
            figure.addEventListener('pointercancel', () => { start = null; });
            figure.addEventListener('pointerup', event => {
                if (!start) return;
                const dx = event.clientX - start.x, dy = event.clientY - start.y; start = null;
                if (Math.abs(dx) > 45 && Math.abs(dx) > Math.abs(dy)) show(dx < 0 ? 1 : -1);
            });
            show(0);
        } catch (error) { if (version === current && node.open) status.textContent = 'Không tải được ảnh phòng. Vui lòng đóng và thử lại.'; }
        finally { clearTimeout(timeout); }
    }

    function pick(trigger) {
        const select = trigger.parentElement.querySelector('select');
        if (!select) return;
        const dataNode = document.getElementById('calendar-page-data');
        if (!dataNode) return;
        if (!roomData.has(dataNode)) {
            try { roomData.set(dataNode, JSON.parse(dataNode.textContent).rooms || []); }
            catch { roomData.set(dataNode, []); }
        }
        const rooms = new Map(roomData.get(dataNode).map(room => [String(room.id), room]));
        const { node } = dialog('Chọn phòng', trigger);
        node.classList.add('room-photo-picker');
        const search = element('input'); search.type = 'search'; search.placeholder = 'Tìm tên hoặc mã phòng'; search.setAttribute('aria-label', 'Tìm phòng');
        const list = element('div', null, 'room-photo-options');
        const normalize = value => String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('vi-VN');
        const render = () => {
            list.replaceChildren();
            const query = normalize(search.value);
            for (const option of select.options) {
                if (!option.value || !normalize(option.textContent + ' ' + (rooms.get(option.value)?.code || '')).includes(query)) continue;
                const room = rooms.get(option.value);
                const row = element('div', null, 'room-photo-option');
                const choose = element('button'); choose.type = 'button'; choose.disabled = option.disabled;
                choose.setAttribute('aria-pressed', String(select.value === option.value));
                if (room?.coverThumbnailUrl) {
                    const thumb = element('img'); thumb.src = room.coverThumbnailUrl; thumb.alt = ''; thumb.width = thumb.height = 48; thumb.loading = 'lazy'; thumb.decoding = 'async'; choose.append(thumb);
                }
                choose.append(element('span', option.textContent));
                choose.addEventListener('click', () => { select.value = option.value; select.dispatchEvent(new Event('change', { bubbles: true })); node.close(); });
                row.append(choose);
                if (room?.imageCount > 0) {
                    const photos = element('button', `Ảnh (${room.imageCount})`); photos.type = 'button';
                    photos.addEventListener('click', () => open(`/api/admin/properties/${encodeURIComponent(trigger.dataset.propertyId)}/rooms/${encodeURIComponent(option.value)}/photos`, photos));
                    row.append(photos);
                }
                list.append(row);
            }
            if (!list.children.length) list.append(element('p', 'Không tìm thấy phòng phù hợp.'));
        };
        search.addEventListener('input', render); node.append(search, list); render(); search.focus();
    }
    window.DeLongRoomPhotos = { open, pick };
})();
