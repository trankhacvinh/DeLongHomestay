// Room photo sheet shared by the public calendar, the public booking page and the admin booking form.
// Loaded on demand by room-photo-entry.js, so pages pay nothing until a guest or staff member opens it.
// One dialog holds both views: the room list (with thumbnails) and a swipeable album, so choosing a
// room never stacks a second popup on top of the first.
(function () {
    const style = document.createElement('link');
    style.rel = 'stylesheet'; style.href = '/css/room-photo-viewer.css?v=20261108-3';
    document.head.appendChild(style);

    const cache = new Map();
    const wide = window.matchMedia('(min-width: 900px)');
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    let current = null, version = 0, abort = null, lockCount = 0, previousOverflow = '';

    const el = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    };
    const button = (className, text, label) => {
        const node = el('button', className, text);
        node.type = 'button';
        if (label) node.setAttribute('aria-label', label);
        return node;
    };
    const normalize = value => String(value || '').toLocaleLowerCase('vi-VN').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\u0111/g, 'd');
    // Card and thumb crops sit next to each other in /uploads/rooms/<room>/<image>/.
    const thumbOf = url => /\/card\.webp(\?|$)/.test(url || '') ? url.replace(/\/card\.webp(?=\?|$)/, '/thumb.webp') : url;
    const cameraIcon = () => {
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('aria-hidden', 'true');
        svg.innerHTML = '<path d="M4 8h3l2-3h6l2 3h3v11H4z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><circle cx="12" cy="13" r="3.5" fill="none" stroke="currentColor" stroke-width="1.8"/>';
        return svg;
    };

    function lock() { if (lockCount++ === 0) { previousOverflow = document.documentElement.style.overflow; document.documentElement.style.overflow = 'hidden'; } }
    function unlock() { if (lockCount > 0 && --lockCount === 0) document.documentElement.style.overflow = previousOverflow; }

    // ---- dialog shell -------------------------------------------------------------------------
    function shell(trigger) {
        if (current?.dialog.open) current.dialog.close();
        const dialog = el('dialog', 'rp');
        const sheet = el('div', 'rp-sheet');
        const head = el('header', 'rp-head');
        const back = button('rp-back', '‹', 'Quay lại danh sách phòng'); back.hidden = true;
        const titles = el('div', 'rp-titles');
        const title = el('h2', null, 'Ảnh phòng');
        title.id = `rp-title-${Date.now().toString(36)}`;
        const subtitle = el('small'); subtitle.hidden = true;
        titles.append(title, subtitle);
        const count = el('span', 'rp-count'); count.hidden = true; count.setAttribute('aria-live', 'polite');
        const close = button('rp-close', '×', 'Đóng');
        head.append(back, titles, count, close);
        const body = el('div', 'rp-body');
        const foot = el('footer', 'rp-foot'); foot.hidden = true;
        sheet.append(head, body, foot);
        dialog.append(sheet);
        dialog.setAttribute('aria-labelledby', title.id);
        close.addEventListener('click', () => dialog.close());
        dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
        const state = { dialog, sheet, head, back, title, subtitle, count, body, foot, trigger, cleanup: [] };
        dialog.addEventListener('close', () => {
            state.cleanup.forEach(fn => fn());
            if (current === state) { current = null; version++; abort?.abort(); }
            unlock();
            dialog.remove();
            if (trigger?.isConnected) trigger.focus({ preventScroll: true });
        }, { once: true });
        document.body.appendChild(dialog);
        lock();
        dialog.showModal();
        current = state;
        return state;
    }
    function setHeader(state, title, subtitle) {
        state.title.textContent = title || 'Ảnh phòng';
        state.subtitle.textContent = subtitle || '';
        state.subtitle.hidden = !subtitle;
    }
    function setFooter(state, options) {
        state.foot.replaceChildren();
        state.foot.hidden = !options?.onChoose;
        if (!options?.onChoose) return;
        const choose = button('rp-choose', options.chooseLabel || 'Chọn phòng này');
        if (options.chooseDisabled) { choose.disabled = true; choose.textContent = options.chooseDisabled; }
        choose.addEventListener('click', () => { state.dialog.close(); options.onChoose(); });
        state.foot.append(choose);
    }

    // ---- album data ---------------------------------------------------------------------------
    async function resolve(source, signal) {
        if (source && typeof source === 'object') return source;
        const cached = cache.get(source);
        if (cached && cached.until > Date.now()) return cached.value;
        const timer = setTimeout(() => abort?.abort(), 12000);
        try {
            const response = await fetch(source, { signal, credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error('load');
            const value = await response.json();
            if (cache.size >= 30) cache.delete(cache.keys().next().value);
            cache.set(source, { value, until: Date.now() + 60000 });
            return value;
        } finally { clearTimeout(timer); }
    }
    function status(text, retry) {
        const box = el('div', 'rp-status');
        box.setAttribute('role', 'status');
        box.append(el('p', null, text));
        if (retry) { const again = button('rp-retry', 'Thử lại'); again.addEventListener('click', retry); box.append(again); }
        return box;
    }

    // ---- album view ---------------------------------------------------------------------------
    // Native scroll-snap does the swiping (momentum, rubber band, works with a mouse wheel too).
    // Only the visible photo and its neighbours get a src, so a 20-photo room costs ~3 images.
    function renderAlbum(state, data) {
        const images = (data.images || []).filter(image => image && (image.cardUrl || image.largeUrl || image.thumbnailUrl));
        state.body.replaceChildren();
        if (!images.length) { state.count.hidden = true; state.body.append(status('Phòng chưa có hình ảnh.')); return; }
        const album = el('div', 'rp-album');
        const stage = el('div', 'rp-stage');
        const track = el('div', 'rp-track');
        track.tabIndex = 0;
        track.setAttribute('aria-roledescription', 'carousel');
        track.setAttribute('aria-label', `Ảnh ${data.name || 'phòng'}`);
        const useLarge = wide.matches;
        const slides = images.map((image, index) => {
            const slide = el('figure', 'rp-slide');
            slide.setAttribute('aria-roledescription', 'slide');
            slide.setAttribute('aria-label', `${index + 1}/${images.length}`);
            const img = el('img');
            img.alt = image.altText || `${data.name || 'Phòng'} · ảnh ${index + 1}`;
            img.decoding = 'async';
            img.draggable = false;
            img.dataset.src = (useLarge && image.largeUrl) || image.cardUrl || image.largeUrl || image.thumbnailUrl;
            img.dataset.fallback = image.thumbnailUrl || thumbOf(image.cardUrl) || '';
            img.addEventListener('load', () => slide.classList.add('is-loaded'));
            img.addEventListener('error', () => {
                if (img.dataset.fallback && img.src !== new URL(img.dataset.fallback, location.href).href) { img.src = img.dataset.fallback; return; }
                slide.classList.add('is-broken');
            });
            slide.append(img);
            track.append(slide);
            return slide;
        });
        stage.append(track);
        let prev = null, next = null;
        if (images.length > 1) {
            prev = button('rp-nav rp-prev', '‹', 'Ảnh trước');
            next = button('rp-nav rp-next', '›', 'Ảnh tiếp');
            stage.append(prev, next);
        }
        album.append(stage);

        let dots = null, thumbs = null;
        if (images.length > 1 && images.length <= 12) {
            dots = el('div', 'rp-dots');
            dots.setAttribute('aria-hidden', 'true');
            images.forEach(() => dots.append(el('i')));
            album.append(dots);
        }
        if (images.length > 1) {
            thumbs = el('div', 'rp-thumbs');
            images.forEach((image, index) => {
                const thumb = button('rp-thumb', null, `Xem ảnh ${index + 1}`);
                const img = el('img');
                img.alt = ''; img.loading = 'lazy'; img.decoding = 'async';
                img.src = image.thumbnailUrl || thumbOf(image.cardUrl) || image.cardUrl;
                thumb.append(img);
                thumb.addEventListener('click', () => go(index));
                thumbs.append(thumb);
            });
            album.append(thumbs);
        }
        state.body.append(album);

        let index = -1;
        const load = i => {
            const img = slides[i]?.querySelector('img');
            if (img && !img.getAttribute('src')) img.src = img.dataset.src;
        };
        const mark = i => {
            if (i === index) return;
            index = i;
            [i, i + 1, i - 1].forEach(load);
            state.count.hidden = images.length < 2;
            state.count.textContent = `${i + 1}/${images.length}`;
            if (prev) { prev.disabled = i === 0; next.disabled = i === images.length - 1; }
            dots?.querySelectorAll('i').forEach((dot, n) => dot.classList.toggle('on', n === i));
            if (thumbs) {
                thumbs.querySelectorAll('.rp-thumb').forEach((thumb, n) => thumb.setAttribute('aria-current', String(n === i)));
                const active = thumbs.children[i];
                if (active) thumbs.scrollTo({ left: active.offsetLeft - (thumbs.clientWidth - active.offsetWidth) / 2, behavior: reducedMotion.matches ? 'auto' : 'smooth' });
            }
        };
        const go = i => {
            const target = Math.max(0, Math.min(images.length - 1, i));
            load(target);
            track.scrollTo({ left: target * track.clientWidth, behavior: reducedMotion.matches ? 'auto' : 'smooth' });
            mark(target);
        };
        let frame = 0;
        track.addEventListener('scroll', () => {
            if (frame) return;
            frame = requestAnimationFrame(() => { frame = 0; if (track.clientWidth) mark(Math.round(track.scrollLeft / track.clientWidth)); });
        }, { passive: true });
        prev?.addEventListener('click', () => go(index - 1));
        next?.addEventListener('click', () => go(index + 1));
        const onKey = event => {
            if (!state.body.contains(album)) return;
            if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
                if (event.target.matches?.('input, textarea')) return;
                event.preventDefault();
                go(index + (event.key === 'ArrowLeft' ? -1 : 1));
            }
        };
        state.dialog.addEventListener('keydown', onKey);
        state.cleanup.push(() => state.dialog.removeEventListener('keydown', onKey));
        mark(0);
        return { go };
    }

    async function showAlbum(state, source, options) {
        const mine = ++version;
        abort?.abort(); abort = new AbortController();
        const signal = abort.signal;
        state.count.hidden = true;
        setHeader(state, options.title || 'Ảnh phòng', options.subtitle);
        setFooter(state, options);
        state.sheet.classList.add('is-album');
        state.body.replaceChildren(status('Đang tải ảnh phòng…'));
        try {
            const data = await resolve(source, signal);
            if (mine !== version || !state.dialog.open) return;
            const name = data.name || options.title;
            setHeader(state, name, options.subtitle || data.propertyName);
            renderAlbum(state, data);
        } catch {
            if (mine !== version || !state.dialog.open) return;
            state.count.hidden = true;
            state.body.replaceChildren(status('Không tải được ảnh phòng. Vui lòng thử lại.', () => showAlbum(state, source, options)));
        }
    }

    // Album only: opened from a room's photo, a calendar column head or a "Xem ảnh" button.
    function open(source, trigger, options = {}) {
        if (!source) return;
        const state = shell(trigger);
        showAlbum(state, source, options);
    }

    // ---- room list view -----------------------------------------------------------------------
    // options: { title, groups: [{ name, rooms: [{ id, name, meta, thumbUrl, imageCount, disabled, note, photos }] }],
    //            selectedId, onSelect(room), chooseLabel }
    function list(options, trigger) {
        const state = shell(trigger);
        state.sheet.classList.add('rp-picker');
        const all = options.groups.flatMap(group => group.rooms);
        const showSearch = all.length > 8;
        const search = el('input', 'rp-search');
        search.type = 'search';
        search.placeholder = 'Tìm tên hoặc mã phòng';
        search.setAttribute('aria-label', 'Tìm phòng');
        search.autocomplete = 'off';
        let scrollTop = 0;
        const choose = room => { state.dialog.close(); options.onSelect(room); };

        const renderList = () => {
            state.sheet.classList.remove('is-album');
            version++; abort?.abort();
            state.back.hidden = true;
            state.count.hidden = true;
            setHeader(state, options.title || 'Chọn phòng', options.subtitle);
            setFooter(state, null);
            const wrap = el('div', 'rp-list');
            if (showSearch) wrap.append(search);
            const items = el('div', 'rp-groups');
            const query = normalize(search.value);
            let shown = 0;
            options.groups.forEach(group => {
                const rooms = group.rooms.filter(room => !query || normalize(`${room.name} ${room.code || ''} ${group.name || ''}`).includes(query));
                if (!rooms.length) return;
                const section = el('section', 'rp-group');
                if (options.groups.length > 1) {
                    const label = el('h3');
                    label.append(el('span', null, group.name || 'Cơ sở'), el('small', null, `${rooms.length} phòng`));
                    section.append(label);
                }
                const ul = el('ul');
                rooms.forEach(room => {
                    shown++;
                    const selected = String(room.id) === String(options.selectedId);
                    const li = el('li', 'rp-item');
                    li.classList.toggle('is-selected', selected);
                    li.classList.toggle('is-disabled', !!room.disabled);
                    const hasPhotos = room.imageCount > 0;
                    const photo = hasPhotos ? button('rp-item-photo', null, `Xem ${room.imageCount} ảnh ${room.name}`) : el('span', 'rp-item-photo');
                    if (!hasPhotos) photo.setAttribute('aria-hidden', 'true');
                    if (room.thumbUrl) {
                        const img = el('img'); img.alt = ''; img.loading = 'lazy'; img.decoding = 'async'; img.src = room.thumbUrl;
                        img.addEventListener('error', () => img.remove(), { once: true });
                        photo.append(img);
                    } else {
                        photo.classList.add('is-empty');
                        photo.append(cameraIcon());
                    }
                    if (hasPhotos) photo.append(el('em', null, String(room.imageCount)));
                    if (hasPhotos) photo.addEventListener('click', () => {
                        scrollTop = items.scrollTop;
                        state.back.hidden = false;
                        showAlbum(state, room.photos, {
                            subtitle: room.meta,
                            onChoose: () => options.onSelect(room),
                            chooseLabel: selected ? 'Giữ phòng này' : (options.chooseLabel || 'Chọn phòng này'),
                            chooseDisabled: room.disabled ? (room.note || 'Phòng đang tạm khóa') : ''
                        });
                    });
                    const main = button('rp-item-main');
                    main.disabled = !!room.disabled;
                    main.setAttribute('aria-pressed', String(selected));
                    const copy = el('span', 'rp-item-copy');
                    copy.append(el('strong', null, room.name));
                    if (room.meta) copy.append(el('small', null, room.meta));
                    if (room.note) copy.append(el('small', 'rp-item-note', room.note));
                    main.append(copy);
                    if (selected) main.append(el('span', 'rp-item-check', '✓'));
                    main.addEventListener('click', () => choose(room));
                    li.append(photo, main);
                    ul.append(li);
                });
                section.append(ul);
                items.append(section);
            });
            if (!shown) items.append(status('Không tìm thấy phòng phù hợp.'));
            wrap.append(items);
            state.body.replaceChildren(wrap);
            items.scrollTop = scrollTop;
            if (!scrollTop) items.querySelector('.is-selected')?.scrollIntoView({ block: 'center' });
        };
        search.addEventListener('input', () => { scrollTop = 0; renderList(); search.focus(); });
        state.back.addEventListener('click', renderList);
        renderList();
        if (showSearch && wide.matches) search.focus();
    }

    // ---- admin booking form: the native <select> stays the source of truth (Vue v-model) ------
    let pageRooms = null;
    function pick(trigger) {
        const field = trigger.closest('[data-room-photo-field]') || trigger.parentElement;
        const select = field.querySelector('select');
        if (!select) return;
        if (!pageRooms) {
            try { pageRooms = JSON.parse(document.getElementById('calendar-page-data')?.textContent || '{}').rooms || []; }
            catch { pageRooms = []; }
        }
        const byId = new Map(pageRooms.map(room => [String(room.id), room]));
        const propertyId = encodeURIComponent(trigger.dataset.propertyId || '');
        const rooms = [...select.options].filter(option => option.value).map(option => {
            const room = byId.get(option.value) || {};
            const name = room.name || option.textContent.replace(/\s·\sĐang khóa$/, '').trim();
            const meta = [room.code, room.capacity ? `${room.capacity} người` : ''].filter(Boolean).join(' · ');
            return {
                id: option.value,
                name,
                code: room.code,
                meta,
                thumbUrl: room.coverThumbnailUrl || '',
                imageCount: Number(room.imageCount || 0),
                disabled: option.disabled,
                note: room.isBookingLocked ? 'Đang khóa đặt phòng' : '',
                photos: `/api/admin/properties/${propertyId}/rooms/${encodeURIComponent(option.value)}/photos`
            };
        });
        list({
            title: 'Chọn phòng',
            groups: [{ name: '', rooms }],
            selectedId: select.value,
            onSelect: room => {
                if (select.value === String(room.id)) return;
                select.value = String(room.id);
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }
        }, trigger);
    }

    window.DeLongRoomPhotos = { open, list, pick, thumbOf };
})();
