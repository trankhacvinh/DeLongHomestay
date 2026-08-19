(function () {
    if (!window.DeLongApi) return;

    const calendarRoot = document.getElementById('calendar-page');
    const bookingsRoot = document.getElementById('bookings-page');
    const root = calendarRoot || bookingsRoot;
    if (!root || window.DeLongApi.__bookingGuestDetailsWrapped) return;

    const dataNode = calendarRoot
        ? document.getElementById('calendar-page-data')
        : document.getElementById('bookings-page-data');
    const initial = (() => {
        try { return JSON.parse(dataNode?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = initial.propertyId;
    if (!propertyId) return;

    ensureStyles();

    const bookingByCode = new Map((initial.bookings || []).map(item => [item.code, item]));
    const detailsCache = new Map();
    const detailsLoading = new Map();
    const documentRetryTimers = new Map();
    let sessionKey = '';
    let editState = newEditState();
    let syncing = false;

    const rawGet = window.DeLongApi.get.bind(window.DeLongApi);
    const rawPost = window.DeLongApi.post.bind(window.DeLongApi);
    const rawPut = window.DeLongApi.put.bind(window.DeLongApi);
    const rawDelete = window.DeLongApi.delete.bind(window.DeLongApi);
    const rawPostForm = window.DeLongApi.postForm?.bind(window.DeLongApi);

    function ensureStyles() {
        if (document.querySelector('link[data-admin-booking-guest-details]')) return;
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/css/admin-booking-guest-details.css?v=20260819-3';
        link.dataset.adminBookingGuestDetails = 'true';
        document.head.appendChild(link);
    }

    function newEditState() {
        return {
            email: '',
            guestCount: 1,
            documents: [],
            frontFile: null,
            backFile: null,
            frontPreview: '',
            backPreview: '',
            removeFront: false,
            removeBack: false,
            loadedBookingId: ''
        };
    }

    function resetEditState() {
        revokePreview('front');
        revokePreview('back');
        editState = newEditState();
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch]);
    }

    function endpoint(path) {
        return `/api/admin/properties/${propertyId}${path}`;
    }

    function guestDetailsUrl(bookingId) {
        return endpoint(`/bookings/${bookingId}/guest-details`);
    }

    function identityUrl(bookingId, side) {
        return endpoint(`/bookings/${bookingId}/identity-documents/${side}`);
    }

    function documentUrl(bookingId, side) {
        return `${identityUrl(bookingId, side)}?v=${encodeURIComponent(bookingId)}`;
    }

    function vueVm() {
        return root.__vue_app__?._instance?.proxy || null;
    }

    function resolveLiveBooking(code) {
        const vm = vueVm();
        const selected = vm?.selectedBooking || null;
        if (selected && (!code || selected.code === code)) {
            if (selected.code) bookingByCode.set(selected.code, selected);
            return selected;
        }

        const liveRows = Array.isArray(vm?.bookings) ? vm.bookings : [];
        const live = code ? liveRows.find(item => item.code === code) || null : null;
        if (live?.code) bookingByCode.set(live.code, live);
        return live || (code ? bookingByCode.get(code) || null : null);
    }

    function currentContext() {
        if (calendarRoot) {
            const modal = calendarRoot.querySelector('.booking-editor');
            if (!modal) return null;
            const title = modal.querySelector('.modal-head h2')?.textContent?.trim() || '';
            let mode = '';
            if (title === 'Đặt phòng') mode = 'create';
            else if (title === 'Sửa lượt đặt') mode = 'edit';
            else if (title === 'Chi tiết lượt đặt') mode = 'view';
            else return null;
            const code = modal.querySelector('.modal-head p')?.textContent?.trim() || '';
            const booking = resolveLiveBooking(code);
            return { modal, mode, code, booking };
        }

        const modal = bookingsRoot?.querySelector('.booking-editor');
        if (!modal) return null;
        const code = modal.querySelector('.modal-head h2')?.textContent?.trim() || '';
        const booking = resolveLiveBooking(code);
        return booking ? { modal, mode: 'view', code, booking } : null;
    }

    function currentRoom(modal) {
        if (!calendarRoot || !modal) return null;
        const roomSelect = modal.querySelector('.booking-form-section select');
        const roomId = roomSelect?.value || '';
        const rooms = Array.isArray(vueVm()?.rooms) ? vueVm().rooms : (initial.rooms || []);
        return rooms.find(room => room.id === roomId) || null;
    }

    function currentCapacity(modal) {
        return Math.max(1, Number(currentRoom(modal)?.capacity || 1));
    }

    async function loadDetails(bookingId, force) {
        if (!bookingId) return null;
        if (!force && detailsCache.has(bookingId)) return detailsCache.get(bookingId);
        if (!force && detailsLoading.has(bookingId)) return detailsLoading.get(bookingId);
        const promise = rawGet(guestDetailsUrl(bookingId))
            .then(details => {
                detailsCache.set(bookingId, details);
                return details;
            })
            .finally(() => detailsLoading.delete(bookingId));
        detailsLoading.set(bookingId, promise);
        return promise;
    }

    function applyCleanNote(context, details) {
        if (!context || !details || !Object.prototype.hasOwnProperty.call(details, 'note')) return;
        if (context.mode === 'edit') {
            const textarea = [...context.modal.querySelectorAll('.booking-form-section textarea')]
                .find(node => node.closest('.booking-form-section')?.querySelector('.booking-form-title')?.textContent?.includes('Chi phí'));
            if (textarea && textarea.value.trim().startsWith('[Đặt web]')) {
                textarea.value = details.note || '';
                textarea.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }

        if (context.mode === 'view') {
            const list = context.modal.querySelector('.detail-list, .booking-detail-list');
            if (!list) return;
            const noteRow = [...list.children].find(row => row.querySelector('span')?.textContent?.trim() === 'Ghi chú');
            if (!noteRow) return;
            if (details.note) {
                const strong = noteRow.querySelector('strong');
                if (strong) strong.textContent = details.note;
                noteRow.hidden = false;
            } else {
                noteRow.hidden = true;
            }
        }
    }

    function hydrateEditState(bookingId, details) {
        if (!bookingId || !details) return;
        editState.email = details.customerEmail || '';
        editState.guestCount = Math.max(1, Number(details.guestCount || 1));
        editState.documents = Array.isArray(details.documents) ? details.documents : [];
        editState.loadedBookingId = bookingId;
        editState.removeFront = false;
        editState.removeBack = false;
    }

    function ensureCalendarForm(context) {
        if (!calendarRoot || !context || !['create', 'edit'].includes(context.mode)) return;
        const customerSection = [...context.modal.querySelectorAll('.booking-form-section')]
            .find(section => section.querySelector('.booking-form-title')?.textContent?.includes('Khách hàng'));
        if (!customerSection) return;

        let extra = customerSection.querySelector('[data-admin-booking-extra-fields]');
        if (!extra) {
            extra = document.createElement('div');
            extra.className = 'admin-booking-extra-fields';
            extra.dataset.adminBookingExtraFields = 'true';
            extra.innerHTML = `
                <div class="field"><label>Email</label><input data-admin-booking-email type="email" maxlength="254" autocomplete="email" placeholder="Không bắt buộc với nhân viên" /><small>Khách web bắt buộc email; nhân viên có thể để trống.</small></div>
                <div class="field"><label>Số lượng khách *</label><input data-admin-booking-guests type="number" min="1" step="1" value="1" /><small data-admin-booking-guest-hint></small></div>`;
            customerSection.querySelector('.form-grid')?.insertAdjacentElement('afterend', extra);
            const emailInput = extra.querySelector('[data-admin-booking-email]');
            const guestInput = extra.querySelector('[data-admin-booking-guests]');
            emailInput.addEventListener('input', () => { editState.email = emailInput.value; });
            guestInput.addEventListener('input', () => {
                const max = currentCapacity(context.modal);
                editState.guestCount = Math.min(max, Math.max(1, Number(guestInput.value || 1)));
                guestInput.value = String(editState.guestCount);
                updateGuestHint(context.modal);
            });
        }

        let identity = customerSection.querySelector('[data-admin-booking-identity-editor]');
        if (!identity) {
            identity = document.createElement('div');
            identity.className = 'admin-booking-identity-editor';
            identity.dataset.adminBookingIdentityEditor = 'true';
            identity.innerHTML = `
                <div class="admin-booking-identity-editor-head"><div><strong>CCCD / giấy tờ tùy thân</strong><small>Ảnh được mã hóa trước khi ghi xuống ổ đĩa. Nhân viên có thể bỏ qua.</small></div><span>Không bắt buộc với nhân viên</span></div>
                <div class="admin-booking-id-grid">
                    ${editorCardHtml('front', 'Mặt trước')}
                    ${editorCardHtml('back', 'Mặt sau')}
                </div>`;
            extra.insertAdjacentElement('afterend', identity);
            bindIdentityEditor(identity, context);
        }

        const emailInput = extra.querySelector('[data-admin-booking-email]');
        const guestInput = extra.querySelector('[data-admin-booking-guests]');
        if (document.activeElement !== emailInput) emailInput.value = editState.email || '';
        if (document.activeElement !== guestInput) guestInput.value = String(Math.max(1, editState.guestCount || 1));
        updateGuestHint(context.modal);
        renderIdentityEditor(identity, context);

        const roomSelect = context.modal.querySelector('.booking-form-section select');
        if (roomSelect && !roomSelect.dataset.bookingGuestCapacityBound) {
            roomSelect.dataset.bookingGuestCapacityBound = 'true';
            roomSelect.addEventListener('change', () => {
                const max = currentCapacity(context.modal);
                editState.guestCount = Math.min(max, Math.max(1, editState.guestCount));
                requestAnimationFrame(() => sync());
            });
        }
    }

    function editorCardHtml(side, label) {
        return `
            <div class="admin-booking-id-card" data-admin-id-card="${side}">
                <button type="button" class="admin-booking-id-picker" data-admin-id-pick="${side}">
                    <img data-admin-id-preview="${side}" alt="${label}" hidden />
                    <span class="admin-booking-id-empty" data-admin-id-empty="${side}"><b>${label}</b><small>Chọn ảnh nếu có</small></span>
                </button>
                <button type="button" class="admin-booking-id-remove" data-admin-id-remove="${side}" aria-label="Bỏ ${label}" hidden>×</button>
                <input type="file" data-admin-id-input="${side}" accept="image/jpeg,image/png,image/webp" capture="environment" hidden />
            </div>`;
    }

    function bindIdentityEditor(identity, context) {
        for (const side of ['front', 'back']) {
            const input = identity.querySelector(`[data-admin-id-input="${side}"]`);
            const pick = identity.querySelector(`[data-admin-id-pick="${side}"]`);
            const remove = identity.querySelector(`[data-admin-id-remove="${side}"]`);
            pick.addEventListener('click', () => input.click());
            input.addEventListener('change', () => {
                const file = input.files?.[0] || null;
                if (file && file.size > 8 * 1024 * 1024) {
                    flash('Mỗi ảnh CCCD tối đa 8 MB.', 'error');
                    input.value = '';
                    return;
                }
                setPendingFile(side, file);
                renderIdentityEditor(identity, context);
            });
            remove.addEventListener('click', () => {
                input.value = '';
                setPendingFile(side, null);
                const removeKey = side === 'front' ? 'removeFront' : 'removeBack';
                const hasExisting = editState.documents.some(item => item.side === side);
                editState[removeKey] = hasExisting;
                renderIdentityEditor(identity, context);
            });
        }
    }

    function setPendingFile(side, file) {
        revokePreview(side);
        const fileKey = side === 'front' ? 'frontFile' : 'backFile';
        const previewKey = side === 'front' ? 'frontPreview' : 'backPreview';
        const removeKey = side === 'front' ? 'removeFront' : 'removeBack';
        editState[fileKey] = file || null;
        editState[removeKey] = false;
        if (file) editState[previewKey] = URL.createObjectURL(file);
    }

    function revokePreview(side) {
        const previewKey = side === 'front' ? 'frontPreview' : 'backPreview';
        if (editState?.[previewKey]) URL.revokeObjectURL(editState[previewKey]);
        if (editState) editState[previewKey] = '';
    }

    function renderIdentityEditor(identity, context) {
        if (!identity) return;
        const bookingId = context.booking?.id || editState.loadedBookingId || '';
        for (const side of ['front', 'back']) {
            const fileKey = side === 'front' ? 'frontFile' : 'backFile';
            const previewKey = side === 'front' ? 'frontPreview' : 'backPreview';
            const removeKey = side === 'front' ? 'removeFront' : 'removeBack';
            const card = identity.querySelector(`[data-admin-id-card="${side}"]`);
            const image = identity.querySelector(`[data-admin-id-preview="${side}"]`);
            const empty = identity.querySelector(`[data-admin-id-empty="${side}"]`);
            const remove = identity.querySelector(`[data-admin-id-remove="${side}"]`);
            const existing = editState.documents.find(item => item.side === side);
            let src = '';
            if (editState[fileKey] && editState[previewKey]) src = editState[previewKey];
            else if (existing && !editState[removeKey] && bookingId) src = documentUrl(bookingId, side);

            card.classList.toggle('is-removed', !!editState[removeKey]);
            if (src) {
                image.src = src;
                image.hidden = false;
                empty.hidden = true;
                remove.hidden = false;
            } else {
                image.removeAttribute('src');
                image.hidden = true;
                empty.hidden = false;
                empty.innerHTML = `<b>${side === 'front' ? 'Mặt trước' : 'Mặt sau'}</b><small>${editState[removeKey] ? 'Sẽ bỏ ảnh khi lưu' : 'Chọn ảnh nếu có'}</small>`;
                remove.hidden = !existing && !editState[fileKey];
            }
        }
    }

    function updateGuestHint(modal) {
        const extra = modal.querySelector('[data-admin-booking-extra-fields]');
        if (!extra) return;
        const max = currentCapacity(modal);
        const guestInput = extra.querySelector('[data-admin-booking-guests]');
        const hint = extra.querySelector('[data-admin-booking-guest-hint]');
        guestInput.max = String(max);
        if (Number(guestInput.value || 1) > max) {
            guestInput.value = String(max);
            editState.guestCount = max;
        }
        hint.textContent = `Tối đa ${max} khách theo sức chứa của phòng.`;
    }

    function formatAcceptedAt(value) {
        if (!value) return '';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return '';
        return new Intl.DateTimeFormat('vi-VN', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        }).format(date);
    }

    function renderDetail(context, details, error) {
        if (!context?.modal || context.mode !== 'view') return;
        const list = context.modal.querySelector('.detail-list, .booking-detail-list');
        if (!list) return;
        let panel = context.modal.querySelector('[data-admin-booking-detail-panel]');
        if (!panel) {
            panel = document.createElement('section');
            panel.className = 'admin-booking-detail-panel';
            panel.dataset.adminBookingDetailPanel = 'true';
        }

        const hero = context.modal.querySelector('.booking-detail-hero');
        if (hero) hero.insertAdjacentElement('afterend', panel);
        else list.insertAdjacentElement('beforebegin', panel);

        if (error) {
            const key = `error:${error}`;
            if (panel.dataset.renderKey !== key) {
                panel.dataset.renderKey = key;
                panel.innerHTML = `<div class="admin-booking-details-error"><strong>Không tải được thông tin người đặt.</strong><span>${escapeHtml(error)}</span></div>`;
            }
            return;
        }
        if (!details) {
            if (panel.dataset.renderKey !== 'loading') {
                panel.dataset.renderKey = 'loading';
                panel.innerHTML = '<div class="admin-booking-details-loading">Đang tải email, số khách và CCCD…</div>';
            }
            return;
        }

        const booking = context.booking;
        if (!booking?.id) return;
        const webBooking = String(booking.source || '').toLowerCase() === 'website';
        const acceptedAt = formatAcceptedAt(details.policyAcceptedAtUtc);
        const policyText = details.policyAccepted
            ? `Đã đồng ý Nội quy & Chính sách${details.policyVersion ? ` v${Number(details.policyVersion)}` : ''}${acceptedAt ? ` · ${acceptedAt}` : ''}`
            : (webBooking
                ? 'Booking web cũ chưa có dữ liệu xác nhận chính sách tách riêng.'
                : 'Booking do nhân viên tạo · không yêu cầu xác nhận Nội quy & Chính sách.');
        const documents = Array.isArray(details.documents) ? details.documents : [];
        const sourceLabel = webBooking ? 'Đặt trên website' : (booking.source || 'Nhân viên đặt');
        const renderKey = JSON.stringify({
            id: booking.id,
            phone: booking.customerPhone,
            email: details.customerEmail,
            guests: details.guestCount,
            max: details.maxGuests,
            policy: policyText,
            docs: documents.map(item => item.side).sort()
        });
        if (panel.dataset.renderKey === renderKey) return;
        panel.dataset.renderKey = renderKey;
        panel.innerHTML = `
            <div class="admin-booking-detail-head">
                <div><strong>Thông tin người đặt</strong><small>Thông tin liên hệ, số khách và giấy tờ đã cung cấp</small></div>
                <span>${escapeHtml(sourceLabel)}</span>
            </div>
            <div class="admin-booking-guest-grid">
                <div class="is-primary"><span>Họ tên</span><strong>${escapeHtml(booking.customerName || '—')}</strong></div>
                <div class="is-primary"><span>Số điện thoại</span><strong class="admin-booking-phone">${escapeHtml(booking.customerPhone || '—')}</strong></div>
                <div><span>Email</span><strong>${escapeHtml(details.customerEmail || 'Không cung cấp')}</strong></div>
                <div><span>Số khách</span><strong>${Number(details.guestCount || 1)} khách</strong><small>Tối đa ${Number(details.maxGuests || 1)} khách/phòng</small></div>
            </div>
            <div class="admin-booking-policy-state ${details.policyAccepted ? '' : 'is-staff'}">${escapeHtml(policyText)}</div>
            <div class="admin-booking-id-title"><strong>CCCD / giấy tờ tùy thân</strong><span>${documents.length}/2 ảnh</span></div>
            <div class="admin-booking-detail-id-grid">
                ${detailIdentityHtml(booking.id, 'front', 'Mặt trước', documents)}
                ${detailIdentityHtml(booking.id, 'back', 'Mặt sau', documents)}
            </div>`;

        maybeRetryWebsiteDocuments(context, details);
    }

    function detailIdentityHtml(bookingId, side, label, documents) {
        const document = documents.find(item => item.side === side);
        if (!document) return `<div class="admin-booking-detail-id"><strong>${label}</strong><span class="empty">Chưa có ảnh</span></div>`;
        const url = documentUrl(bookingId, side);
        return `<div class="admin-booking-detail-id"><div class="admin-booking-id-label"><strong>${label}</strong><span>Đã nhận</span></div><a href="${url}" target="_blank" rel="noopener"><img src="${url}" alt="${label}" loading="eager" /></a></div>`;
    }

    function clearDocumentRetries(bookingId) {
        const timers = documentRetryTimers.get(bookingId) || [];
        timers.forEach(timer => clearTimeout(timer));
        documentRetryTimers.delete(bookingId);
    }

    function maybeRetryWebsiteDocuments(context, details) {
        const booking = context.booking;
        if (!booking?.id || String(booking.source || '').toLowerCase() !== 'website') return;
        const documents = Array.isArray(details.documents) ? details.documents : [];
        if (documents.length >= 2) {
            clearDocumentRetries(booking.id);
            return;
        }
        if (documentRetryTimers.has(booking.id)) return;

        const timers = [1200, 3600].map(delay => setTimeout(async () => {
            const latest = currentContext();
            if (latest?.mode !== 'view' || latest.booking?.id !== booking.id) return;
            try {
                const refreshed = await loadDetails(booking.id, true);
                renderDetail(latest, refreshed, null);
                applyCleanNote(latest, refreshed);
                if ((refreshed.documents || []).length >= 2) clearDocumentRetries(booking.id);
            } catch { }
        }, delay));
        documentRetryTimers.set(booking.id, timers);
    }

    function collectSupplement() {
        if (!calendarRoot) return null;
        const context = currentContext();
        if (!context || !['create', 'edit'].includes(context.mode)) return null;
        const extra = context.modal.querySelector('[data-admin-booking-extra-fields]');
        if (!extra) return null;
        const emailInput = extra.querySelector('[data-admin-booking-email]');
        const guestInput = extra.querySelector('[data-admin-booking-guests]');
        const email = emailInput.value.trim();
        const capacity = currentCapacity(context.modal);
        const guestCount = Number(guestInput.value || 0);
        if (email && !emailInput.checkValidity()) throw new Error('Email khách không hợp lệ.');
        if (!Number.isInteger(guestCount) || guestCount < 1 || guestCount > capacity)
            throw new Error(`Phòng này tối đa ${capacity} khách.`);
        editState.email = email;
        editState.guestCount = guestCount;
        return {
            email,
            guestCount,
            frontFile: editState.frontFile,
            backFile: editState.backFile,
            removeFront: editState.removeFront,
            removeBack: editState.removeBack
        };
    }

    async function persistSupplement(booking, supplement) {
        if (!booking?.id || !supplement) return;
        let details;
        try {
            details = await rawPut(guestDetailsUrl(booking.id), {
                customerEmail: supplement.email || null,
                guestCount: supplement.guestCount
            });
            detailsCache.set(booking.id, { ...(detailsCache.get(booking.id) || {}), ...details });
        } catch (error) {
            flash(`Đã lưu lượt đặt nhưng chưa lưu được email/số khách: ${error.message || 'lỗi không xác định'}`, 'error');
            return;
        }

        const failures = [];
        for (const side of ['front', 'back']) {
            const file = side === 'front' ? supplement.frontFile : supplement.backFile;
            const remove = side === 'front' ? supplement.removeFront : supplement.removeBack;
            try {
                if (file) {
                    if (!rawPostForm) throw new Error('Trình duyệt chưa hỗ trợ tải file qua API hiện tại.');
                    const form = new FormData();
                    form.append('file', file, file.name || `${side}.jpg`);
                    await rawPostForm(identityUrl(booking.id, side), form);
                } else if (remove) {
                    await rawDelete(identityUrl(booking.id, side));
                }
            } catch (error) {
                failures.push(`${side === 'front' ? 'mặt trước' : 'mặt sau'}: ${error.message || 'không thể lưu'}`);
            }
        }
        detailsCache.delete(booking.id);
        clearDocumentRetries(booking.id);
        if (failures.length) flash(`Booking đã lưu, nhưng CCCD chưa cập nhật đủ (${failures.join('; ')}).`, 'error');
    }

    function pathOnly(url) {
        try { return new URL(String(url), window.location.origin).pathname.replace(/\/$/, ''); }
        catch { return String(url).split('?')[0].replace(/\/$/, ''); }
    }

    function isBookingCreateUrl(url) {
        return pathOnly(url) === `/api/admin/properties/${propertyId}/bookings`;
    }

    function isBookingUpdateUrl(url) {
        return new RegExp(`^/api/admin/properties/${propertyId}/bookings/[0-9a-f-]{36}$`, 'i').test(pathOnly(url));
    }

    window.DeLongApi.post = async function (url, data, headers) {
        if (!isBookingCreateUrl(url)) return rawPost(url, data, headers);
        const supplement = collectSupplement();
        const result = await rawPost(url, data, headers);
        if (result?.code) bookingByCode.set(result.code, result);
        await persistSupplement(result, supplement);
        resetEditState();
        return result;
    };

    window.DeLongApi.put = async function (url, data) {
        if (!isBookingUpdateUrl(url)) return rawPut(url, data);
        const supplement = collectSupplement();
        const result = await rawPut(url, data);
        if (result?.code) bookingByCode.set(result.code, result);
        await persistSupplement(result, supplement);
        resetEditState();
        return result;
    };
    window.DeLongApi.__bookingGuestDetailsWrapped = true;

    async function sync() {
        if (syncing) return;
        syncing = true;
        try {
            const context = currentContext();
            if (!context) {
                if (sessionKey) {
                    sessionKey = '';
                    resetEditState();
                }
                return;
            }

            const key = `${context.mode}:${context.booking?.id || 'new'}`;
            if (key !== sessionKey) {
                sessionKey = key;
                resetEditState();
                if (context.mode === 'create') {
                    editState.guestCount = 1;
                } else if (context.booking?.id) {
                    renderDetail(context, null, null);
                    try {
                        const details = await loadDetails(context.booking.id, true);
                        if (context.mode === 'edit') hydrateEditState(context.booking.id, details);
                        applyCleanNote(context, details);
                    } catch (error) {
                        if (context.mode === 'view') renderDetail(context, null, error.message || 'Không thể tải thông tin khách.');
                    }
                }
            }

            if (context.mode === 'edit' && context.booking?.id && editState.loadedBookingId !== context.booking.id) {
                try {
                    const details = await loadDetails(context.booking.id, true);
                    hydrateEditState(context.booking.id, details);
                    applyCleanNote(context, details);
                } catch { }
            }

            if (['create', 'edit'].includes(context.mode)) ensureCalendarForm(context);
            if (context.mode === 'view' && context.booking?.id) {
                const details = detailsCache.get(context.booking.id) || null;
                renderDetail(context, details, null);
                applyCleanNote(context, details);
                if (!details && !detailsLoading.has(context.booking.id)) {
                    loadDetails(context.booking.id, true)
                        .then(value => {
                            const latest = currentContext();
                            if (latest?.mode === 'view' && latest.booking?.id === context.booking.id) {
                                renderDetail(latest, value, null);
                                applyCleanNote(latest, value);
                            }
                        })
                        .catch(error => {
                            const latest = currentContext();
                            if (latest?.mode === 'view' && latest.booking?.id === context.booking.id)
                                renderDetail(latest, null, error.message || 'Không thể tải thông tin khách.');
                        });
                }
            }
        } finally {
            syncing = false;
        }
    }

    function flash(message, type) {
        document.querySelector('[data-admin-booking-aux-toast]')?.remove();
        const toast = document.createElement('div');
        toast.className = `admin-booking-aux-toast ${type === 'error' ? 'error' : ''}`;
        toast.dataset.adminBookingAuxToast = 'true';
        toast.textContent = message;
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), type === 'error' ? 5500 : 3000);
    }

    const observer = new MutationObserver(() => requestAnimationFrame(sync));
    observer.observe(root, { childList: true, subtree: true });
    window.addEventListener('beforeunload', () => {
        for (const bookingId of documentRetryTimers.keys()) clearDocumentRetries(bookingId);
        revokePreview('front');
        revokePreview('back');
    });
    sync();
})();
