(function () {
    const calendarRoot = document.getElementById('calendar-page');
    const bookingsRoot = document.getElementById('bookings-page');
    const root = calendarRoot || bookingsRoot;
    if (!root || !window.DeLongApi || window.DeLongApi.__bookingLiveV2Bound) return;

    window.DeLongApi.__bookingLiveV2Bound = true;
    window.DeLongApi.__bookingGuestDetailsWrapped = true;
    root.dataset.bookingLiveV2 = 'ready';

    const dataNode = calendarRoot
        ? document.getElementById('calendar-page-data')
        : document.getElementById('bookings-page-data');
    const initial = (() => {
        try { return JSON.parse(dataNode?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = initial.propertyId;
    if (!propertyId) return;

    const rawGet = window.DeLongApi.get.bind(window.DeLongApi);
    const rawPost = window.DeLongApi.post.bind(window.DeLongApi);
    const rawPut = window.DeLongApi.put.bind(window.DeLongApi);
    const rawDelete = window.DeLongApi.delete.bind(window.DeLongApi);
    const rawPostForm = window.DeLongApi.postForm?.bind(window.DeLongApi);

    const bookingByCode = new Map();
    const detailsCache = new Map();
    const detailsLoading = new Map();
    const documentRetryTimers = new Map();
    const refreshTimers = new Set();
    const previewUrls = new Set();
    let refreshInFlight = false;
    let refreshQueued = false;
    let pollTimer = null;
    let syncQueued = false;
    let lastEditKey = '';

    for (const booking of (initial.bookings || [])) {
        if (booking?.code) bookingByCode.set(booking.code, booking);
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, ch => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        })[ch]);
    }

    function vm() {
        return root.__vue_app__?._instance?.proxy || null;
    }

    function addDays(key, amount) {
        const [year, month, day] = String(key || '').split('-').map(Number);
        const date = new Date(Date.UTC(year, month - 1, day));
        date.setUTCDate(date.getUTCDate() + amount);
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
    }

    function guestDetailsUrl(bookingId) {
        return `/api/admin/properties/${propertyId}/bookings/${bookingId}/guest-details`;
    }

    function identityUrl(bookingId, side) {
        return `/api/admin/properties/${propertyId}/bookings/${bookingId}/identity-documents/${side}`;
    }

    function documentUrl(bookingId, side) {
        return `${identityUrl(bookingId, side)}?v=${Date.now()}`;
    }

    function bookingsUrl() {
        if (!calendarRoot) return `/api/admin/properties/${propertyId}/bookings`;
        const startDate = initial.startDate;
        if (!startDate) return `/api/admin/properties/${propertyId}/bookings`;
        const utcOffset = initial.utcOffset || '+07:00';
        const query = new URLSearchParams({
            from: `${startDate}T00:00:00${utcOffset}`,
            to: `${addDays(startDate, 7)}T00:00:00${utcOffset}`
        });
        return `/api/admin/properties/${propertyId}/bookings?${query}`;
    }

    function currentModalContext() {
        const modal = root.querySelector('.booking-editor');
        if (!modal) return null;

        if (calendarRoot) {
            const title = modal.querySelector('.modal-head h2')?.textContent?.trim() || '';
            if (title === 'Chi tiết lượt đặt') {
                const code = modal.querySelector('.modal-head p')?.textContent?.trim() || '';
                return { modal, mode: 'view', code, booking: bookingByCode.get(code) || vm()?.selectedBooking || null };
            }
            if (title === 'Sửa lượt đặt') {
                const code = modal.querySelector('.modal-head p')?.textContent?.trim() || '';
                return { modal, mode: 'edit', code, booking: bookingByCode.get(code) || vm()?.selectedBooking || null };
            }
            if (title === 'Đặt phòng') return { modal, mode: 'create', code: '', booking: null };
            return null;
        }

        const code = modal.querySelector('.modal-head h2')?.textContent?.trim() || '';
        if (!code) return null;
        return { modal, mode: 'view', code, booking: bookingByCode.get(code) || vm()?.selectedBooking || null };
    }

    async function loadDetails(bookingId, force) {
        if (!bookingId) return null;
        if (!force && detailsCache.has(bookingId)) return detailsCache.get(bookingId);
        if (detailsLoading.has(bookingId)) return detailsLoading.get(bookingId);
        const promise = rawGet(guestDetailsUrl(bookingId))
            .then(value => {
                detailsCache.set(bookingId, value);
                return value;
            })
            .finally(() => detailsLoading.delete(bookingId));
        detailsLoading.set(bookingId, promise);
        return promise;
    }

    function formatAcceptedAt(value) {
        if (!value) return '';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return '';
        return new Intl.DateTimeFormat('vi-VN', {
            day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
        }).format(date);
    }

    function renderGuestDetail(context, details, error) {
        if (!context?.modal || context.mode !== 'view') return;
        if (context.modal.querySelector('[data-native-booking-guest-details]')) return;
        const booking = context.booking;
        if (!booking?.id) return;

        let panel = context.modal.querySelector('[data-admin-booking-detail-panel]');
        if (!panel) {
            panel = document.createElement('section');
            panel.className = 'admin-booking-detail-panel';
            panel.dataset.adminBookingDetailPanel = 'true';
        }

        const hero = context.modal.querySelector('.booking-detail-hero');
        const list = context.modal.querySelector('.detail-list, .booking-detail-list');
        if (hero) hero.insertAdjacentElement('afterend', panel);
        else if (list) list.insertAdjacentElement('beforebegin', panel);
        else return;

        if (error) {
            const key = `error:${error}`;
            if (panel.dataset.renderKey === key) return;
            panel.dataset.renderKey = key;
            panel.innerHTML = `<div class="admin-booking-details-error"><strong>Không tải được thông tin người đặt.</strong><span>${escapeHtml(error)}</span></div>`;
            return;
        }
        if (!details) {
            if (panel.dataset.renderKey === 'loading') return;
            panel.dataset.renderKey = 'loading';
            panel.innerHTML = '<div class="admin-booking-details-loading">Đang tải email, số khách và CCCD…</div>';
            return;
        }

        const documents = Array.isArray(details.documents) ? details.documents : [];
        const webBooking = String(booking.source || '').toLowerCase() === 'website';
        const acceptedAt = formatAcceptedAt(details.policyAcceptedAtUtc);
        const policyText = details.policyAccepted
            ? `Đã đồng ý Nội quy & Chính sách${details.policyVersion ? ` v${Number(details.policyVersion)}` : ''}${acceptedAt ? ` · ${acceptedAt}` : ''}`
            : (webBooking
                ? 'Booking web chưa đồng bộ đủ dữ liệu xác nhận chính sách.'
                : 'Booking do nhân viên tạo · không yêu cầu xác nhận Nội quy & Chính sách.');
        const renderKey = JSON.stringify({
            id: booking.id,
            name: booking.customerName,
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
                <div><strong>Thông tin người đặt</strong><small>Email, số khách và giấy tờ khách đã cung cấp</small></div>
                <span>${webBooking ? 'Đặt trên website' : escapeHtml(booking.source || 'Nhân viên đặt')}</span>
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
                ${identityDetailHtml(booking.id, 'front', 'Mặt trước', documents)}
                ${identityDetailHtml(booking.id, 'back', 'Mặt sau', documents)}
            </div>`;

        if (webBooking && (!details.policyAccepted || documents.length < 2)) scheduleDocumentRetry(booking.id);
        else clearDocumentRetries(booking.id);
    }

    function identityDetailHtml(bookingId, side, label, documents) {
        const found = documents.find(item => item.side === side);
        if (!found) return `<div class="admin-booking-detail-id"><strong>${label}</strong><span class="empty">Chưa nhận được ảnh</span></div>`;
        const url = documentUrl(bookingId, side);
        return `<div class="admin-booking-detail-id"><div class="admin-booking-id-label"><strong>${label}</strong><span>Đã nhận</span></div><a href="${url}" target="_blank" rel="noopener"><img src="${url}" alt="${label}" loading="eager" /></a></div>`;
    }

    function clearDocumentRetries(bookingId) {
        const timers = documentRetryTimers.get(bookingId) || [];
        timers.forEach(clearTimeout);
        documentRetryTimers.delete(bookingId);
    }

    function scheduleDocumentRetry(bookingId) {
        if (!bookingId || documentRetryTimers.has(bookingId)) return;
        const timers = [1200, 3600].map(delay => setTimeout(async () => {
            const context = currentModalContext();
            if (context?.mode !== 'view' || context.booking?.id !== bookingId) return;
            try {
                const details = await loadDetails(bookingId, true);
                renderGuestDetail(context, details, null);
            } catch { }
        }, delay));
        documentRetryTimers.set(bookingId, timers);
    }

    function currentCapacity(modal) {
        const roomSelect = modal.querySelector('.booking-form-section select');
        const roomId = roomSelect?.value || '';
        const room = (initial.rooms || []).find(item => item.id === roomId);
        return Math.max(1, Number(room?.capacity || 1));
    }

    function cardHtml(side, label) {
        return `
            <div class="admin-booking-id-card" data-booking-v2-card="${side}" data-remove="false" data-existing="false">
                <button type="button" class="admin-booking-id-picker" data-booking-v2-pick="${side}">
                    <img data-booking-v2-preview="${side}" alt="${label}" hidden />
                    <span class="admin-booking-id-empty" data-booking-v2-empty="${side}"><b>${label}</b><small>Chọn ảnh nếu có</small></span>
                </button>
                <button type="button" class="admin-booking-id-remove" data-booking-v2-remove="${side}" aria-label="Bỏ ${label}" hidden>×</button>
                <input type="file" data-booking-v2-input="${side}" accept="image/jpeg,image/png,image/webp" capture="environment" hidden />
            </div>`;
    }

    function ensureEditFields(context) {
        if (!calendarRoot || !context || !['create', 'edit'].includes(context.mode)) return;
        const customerSection = [...context.modal.querySelectorAll('.booking-form-section')]
            .find(section => section.querySelector('.booking-form-title')?.textContent?.includes('Khách hàng'));
        if (!customerSection) return;

        let extra = customerSection.querySelector('[data-booking-v2-extra]');
        if (!extra) {
            extra = document.createElement('div');
            extra.className = 'admin-booking-extra-fields';
            extra.dataset.bookingV2Extra = 'true';
            extra.innerHTML = `
                <div class="field"><label>Email</label><input data-booking-v2-email type="email" maxlength="254" autocomplete="email" placeholder="Không bắt buộc với nhân viên" /><small>Khách đặt web có email bắt buộc; nhân viên có thể để trống.</small></div>
                <div class="field"><label>Số lượng khách *</label><input data-booking-v2-guests type="number" min="1" step="1" value="1" /><small data-booking-v2-guest-hint></small></div>`;
            customerSection.querySelector('.form-grid')?.insertAdjacentElement('afterend', extra);
        }

        let identity = customerSection.querySelector('[data-booking-v2-identity]');
        if (!identity) {
            identity = document.createElement('div');
            identity.className = 'admin-booking-identity-editor';
            identity.dataset.bookingV2Identity = 'true';
            identity.innerHTML = `
                <div class="admin-booking-identity-editor-head"><div><strong>CCCD / giấy tờ tùy thân</strong><small>Nhân viên có thể bỏ qua. Ảnh được lưu trong kho riêng đã mã hóa.</small></div><span>Không bắt buộc</span></div>
                <div class="admin-booking-id-grid">${cardHtml('front', 'Mặt trước')}${cardHtml('back', 'Mặt sau')}</div>`;
            extra.insertAdjacentElement('afterend', identity);
            bindIdentityEditor(identity);
        }

        const max = currentCapacity(context.modal);
        const guestInput = extra.querySelector('[data-booking-v2-guests]');
        guestInput.max = String(max);
        if (Number(guestInput.value || 1) > max) guestInput.value = String(max);
        const hint = extra.querySelector('[data-booking-v2-guest-hint]');
        const hintText = `Tối đa ${max} khách theo sức chứa của phòng.`;
        if (hint.textContent !== hintText) hint.textContent = hintText;

        const editKey = `${context.mode}:${context.booking?.id || 'new'}`;
        if (editKey === lastEditKey) return;
        lastEditKey = editKey;

        if (context.mode === 'create') {
            extra.querySelector('[data-booking-v2-email]').value = '';
            guestInput.value = '1';
            renderEditorDocuments(context, null);
            return;
        }

        if (!context.booking?.id) return;
        loadDetails(context.booking.id, false)
            .then(details => {
                const latest = currentModalContext();
                if (latest?.mode !== 'edit' || latest.booking?.id !== context.booking.id) return;
                const latestExtra = latest.modal.querySelector('[data-booking-v2-extra]');
                if (!latestExtra) return;
                latestExtra.querySelector('[data-booking-v2-email]').value = details.customerEmail || '';
                latestExtra.querySelector('[data-booking-v2-guests]').value = String(Math.max(1, Number(details.guestCount || 1)));
                renderEditorDocuments(latest, details);
                const live = vm();
                if (Object.prototype.hasOwnProperty.call(details, 'note') && live?.form && String(live.form.note || '').startsWith('[Đặt web]')) {
                    live.form.note = details.note || '';
                }
            })
            .catch(error => flash(error.message || 'Không thể tải thông tin khách.', 'error'));
    }

    function bindIdentityEditor(identity) {
        for (const side of ['front', 'back']) {
            const input = identity.querySelector(`[data-booking-v2-input="${side}"]`);
            const card = identity.querySelector(`[data-booking-v2-card="${side}"]`);
            const pick = identity.querySelector(`[data-booking-v2-pick="${side}"]`);
            const remove = identity.querySelector(`[data-booking-v2-remove="${side}"]`);
            pick.addEventListener('click', () => input.click());
            input.addEventListener('change', () => {
                const file = input.files?.[0] || null;
                if (file && file.size > 8 * 1024 * 1024) {
                    flash('Mỗi ảnh CCCD tối đa 8 MB.', 'error');
                    input.value = '';
                    return;
                }
                card.dataset.remove = 'false';
                renderPendingDocument(card, side, file);
            });
            remove.addEventListener('click', event => {
                event.stopPropagation();
                input.value = '';
                card.dataset.remove = card.dataset.existing === 'true' ? 'true' : 'false';
                renderPendingDocument(card, side, null);
            });
        }
    }

    function renderPendingDocument(card, side, file) {
        const image = card.querySelector(`[data-booking-v2-preview="${side}"]`);
        const empty = card.querySelector(`[data-booking-v2-empty="${side}"]`);
        const remove = card.querySelector(`[data-booking-v2-remove="${side}"]`);

        if (file) {
            const url = URL.createObjectURL(file);
            previewUrls.add(url);
            image.src = url;
            image.hidden = false;
            empty.hidden = true;
            remove.hidden = false;
            return;
        }
        if (card.dataset.remove === 'true') {
            image.removeAttribute('src');
            image.hidden = true;
            empty.hidden = false;
            empty.innerHTML = `<b>${side === 'front' ? 'Mặt trước' : 'Mặt sau'}</b><small>Sẽ xóa ảnh khi lưu</small>`;
            remove.hidden = false;
            return;
        }
        if (card.dataset.existingUrl) {
            image.src = card.dataset.existingUrl;
            image.hidden = false;
            empty.hidden = true;
            remove.hidden = false;
            return;
        }
        image.removeAttribute('src');
        image.hidden = true;
        empty.hidden = false;
        empty.innerHTML = `<b>${side === 'front' ? 'Mặt trước' : 'Mặt sau'}</b><small>Chọn ảnh nếu có</small>`;
        remove.hidden = true;
    }

    function renderEditorDocuments(context, details) {
        const identity = context.modal.querySelector('[data-booking-v2-identity]');
        if (!identity) return;
        const documents = Array.isArray(details?.documents) ? details.documents : [];
        for (const side of ['front', 'back']) {
            const card = identity.querySelector(`[data-booking-v2-card="${side}"]`);
            const input = identity.querySelector(`[data-booking-v2-input="${side}"]`);
            const existing = documents.some(item => item.side === side);
            card.dataset.existing = existing ? 'true' : 'false';
            card.dataset.remove = 'false';
            card.dataset.existingUrl = existing && context.booking?.id ? documentUrl(context.booking.id, side) : '';
            input.value = '';
            renderPendingDocument(card, side, null);
        }
    }

    function collectSupplement() {
        const context = currentModalContext();
        if (!calendarRoot || !context || !['create', 'edit'].includes(context.mode)) return null;
        const extra = context.modal.querySelector('[data-booking-v2-extra]');
        const identity = context.modal.querySelector('[data-booking-v2-identity]');
        if (!extra) return null;

        const emailInput = extra.querySelector('[data-booking-v2-email]');
        const guestInput = extra.querySelector('[data-booking-v2-guests]');
        const email = emailInput.value.trim();
        const guestCount = Number(guestInput.value || 0);
        const capacity = currentCapacity(context.modal);
        if (email && !emailInput.checkValidity()) throw new Error('Email khách không hợp lệ.');
        if (!Number.isInteger(guestCount) || guestCount < 1 || guestCount > capacity) throw new Error(`Phòng này tối đa ${capacity} khách.`);

        const sideValue = side => {
            const input = identity?.querySelector(`[data-booking-v2-input="${side}"]`);
            const card = identity?.querySelector(`[data-booking-v2-card="${side}"]`);
            return { file: input?.files?.[0] || null, remove: card?.dataset.remove === 'true' };
        };
        return { email, guestCount, front: sideValue('front'), back: sideValue('back') };
    }

    async function persistSupplement(booking, supplement) {
        if (!booking?.id || !supplement) return;
        await rawPut(guestDetailsUrl(booking.id), {
            customerEmail: supplement.email || null,
            guestCount: supplement.guestCount
        });

        for (const [side, value] of [['front', supplement.front], ['back', supplement.back]]) {
            if (value.file) {
                if (!rawPostForm) throw new Error('Không thể tải ảnh trong trình duyệt hiện tại.');
                const form = new FormData();
                form.append('file', value.file, value.file.name || `${side}.jpg`);
                await rawPostForm(identityUrl(booking.id, side), form);
            } else if (value.remove) {
                await rawDelete(identityUrl(booking.id, side));
            }
        }
        detailsCache.delete(booking.id);
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
        if (!calendarRoot || !isBookingCreateUrl(url)) return rawPost(url, data, headers);
        const supplement = collectSupplement();
        const booking = await rawPost(url, data, headers);
        try {
            await persistSupplement(booking, supplement);
        } catch (error) {
            flash(`Lượt đặt đã lưu nhưng thông tin khách bổ sung chưa lưu đủ: ${error.message || 'lỗi không xác định'}`, 'error');
        }
        return booking;
    };

    window.DeLongApi.put = async function (url, data) {
        if (!calendarRoot || !isBookingUpdateUrl(url)) return rawPut(url, data);
        const supplement = collectSupplement();
        const booking = await rawPut(url, data);
        try {
            await persistSupplement(booking, supplement);
        } catch (error) {
            flash(`Lượt đặt đã lưu nhưng thông tin khách bổ sung chưa lưu đủ: ${error.message || 'lỗi không xác định'}`, 'error');
        }
        return booking;
    };

    async function refreshBookings(reason) {
        if (refreshInFlight) {
            refreshQueued = true;
            return;
        }
        refreshInFlight = true;
        try {
            const rows = await rawGet(bookingsUrl());
            if (!Array.isArray(rows)) return;
            bookingByCode.clear();
            rows.forEach(item => { if (item?.code) bookingByCode.set(item.code, item); });

            const live = vm();
            if (live && Array.isArray(live.bookings)) {
                live.bookings.splice(0, live.bookings.length, ...rows);
                if (live.selectedBooking?.id) {
                    const latest = rows.find(item => item.id === live.selectedBooking.id);
                    if (latest) live.selectedBooking = latest;
                }
            } else if (reason === 'notification') {
                setTimeout(() => window.location.reload(), 150);
                return;
            }
            queueSync();
        } catch (error) {
            if (reason === 'notification') flash(error.message || 'Không thể cập nhật danh sách booking mới.', 'error');
        } finally {
            refreshInFlight = false;
            if (refreshQueued) {
                refreshQueued = false;
                setTimeout(() => refreshBookings('queued'), 100);
            }
        }
    }

    function refreshBurst() {
        [0, 900, 2800].forEach(delay => {
            const timer = setTimeout(() => {
                refreshTimers.delete(timer);
                refreshBookings('notification');
            }, delay);
            refreshTimers.add(timer);
        });
    }

    function queueSync() {
        if (syncQueued) return;
        syncQueued = true;
        requestAnimationFrame(() => {
            syncQueued = false;
            syncModal();
        });
    }

    async function syncModal() {
        const context = currentModalContext();
        if (!context) {
            lastEditKey = '';
            return;
        }

        if (context.mode === 'view') {
            if (!context.booking?.id) {
                await refreshBookings('modal');
                const retry = currentModalContext();
                if (!retry?.booking?.id) return;
                return syncModal();
            }
            const cached = detailsCache.get(context.booking.id) || null;
            renderGuestDetail(context, cached, null);
            if (!cached && !detailsLoading.has(context.booking.id)) {
                loadDetails(context.booking.id, false)
                    .then(details => {
                        const latest = currentModalContext();
                        if (latest?.mode === 'view' && latest.booking?.id === context.booking.id) renderGuestDetail(latest, details, null);
                    })
                    .catch(error => {
                        const latest = currentModalContext();
                        if (latest?.mode === 'view' && latest.booking?.id === context.booking.id) renderGuestDetail(latest, null, error.message || 'Không thể tải thông tin khách.');
                    });
            }
            return;
        }

        ensureEditFields(context);
    }

    function flash(message, type) {
        document.querySelector('[data-admin-booking-aux-toast]')?.remove();
        const toast = document.createElement('div');
        toast.className = `admin-booking-aux-toast ${type === 'error' ? 'error' : ''}`;
        toast.dataset.adminBookingAuxToast = 'true';
        toast.textContent = String(message || '');
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), type === 'error' ? 6000 : 3200);
    }

    document.addEventListener('delong:booking-notification', event => {
        if (event.detail?.propertyId && event.detail.propertyId !== propertyId) return;
        refreshBurst();
    });
    window.addEventListener('focus', () => refreshBookings('focus'));
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) refreshBookings('visible');
    });

    const observer = new MutationObserver(queueSync);
    observer.observe(root, { childList: true, subtree: true });

    refreshBookings('initial');
    queueSync();
    pollTimer = setInterval(() => {
        if (!document.hidden) refreshBookings('poll');
    }, 10000);

    window.addEventListener('beforeunload', () => {
        if (pollTimer) clearInterval(pollTimer);
        refreshTimers.forEach(clearTimeout);
        documentRetryTimers.forEach(timers => timers.forEach(clearTimeout));
        previewUrls.forEach(url => URL.revokeObjectURL(url));
        observer.disconnect();
    }, { once: true });
})();
