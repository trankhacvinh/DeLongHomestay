(function () {
    const calendarRoot = document.getElementById('calendar-page');
    const bookingsRoot = document.getElementById('bookings-page');
    const root = calendarRoot || bookingsRoot;
    if (!root || !window.DeLongApi || window.DeLongApi.__bookingLiveCoreBound) return;

    window.DeLongApi.__bookingLiveCoreBound = true;
    // PR #48 used a late DOM enhancer for these fields. This module owns the feature now so
    // the old enhancer exits instead of wrapping API calls a second time.
    window.DeLongApi.__bookingGuestDetailsWrapped = true;
    if (calendarRoot) calendarRoot.dataset.bookingRealtimeBound = 'true';

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

    const rawGet = window.DeLongApi.get.bind(window.DeLongApi);
    const rawPost = window.DeLongApi.post.bind(window.DeLongApi);
    const rawPut = window.DeLongApi.put.bind(window.DeLongApi);
    const rawDelete = window.DeLongApi.delete.bind(window.DeLongApi);
    const rawPostForm = window.DeLongApi.postForm?.bind(window.DeLongApi);
    const detailsCache = new Map();
    const detailsLoading = new Map();
    const documentRetryTimers = new Map();
    const refreshTimers = new Set();
    const previewUrls = new Set();
    let refreshInFlight = false;
    let refreshQueued = false;
    let pollTimer = null;
    let syncQueued = false;

    function ensureStyles() {
        if (document.querySelector('link[data-admin-booking-guest-details]')) return;
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/css/admin-booking-guest-details.css?v=20260819-4';
        link.dataset.adminBookingGuestDetails = 'true';
        document.head.appendChild(link);
    }

    function vm() {
        return root.__vue_app__?._instance?.proxy || null;
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, ch => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        })[ch]);
    }

    function addDays(key, amount) {
        const [year, month, day] = String(key).split('-').map(Number);
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

    function currentContext() {
        const live = vm();
        if (!live) return null;
        const modal = root.querySelector('.booking-editor');
        if (!modal) return null;

        if (calendarRoot) {
            if (!live.editor?.open) return null;
            return {
                vm: live,
                modal,
                mode: live.editor.mode || 'view',
                booking: live.selectedBooking || null
            };
        }

        if (!live.detail?.open || !live.selectedBooking) return null;
        return { vm: live, modal, mode: 'view', booking: live.selectedBooking };
    }

    function currentCapacity(context) {
        if (!context) return 1;
        const roomId = context.mode === 'create'
            ? context.vm.form?.roomId
            : (context.vm.form?.roomId || context.booking?.roomId);
        const room = (context.vm.rooms || []).find(item => item.id === roomId);
        return Math.max(1, Number(room?.capacity || 1));
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
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        }).format(date);
    }

    function renderDetail(context, details, error) {
        if (!context?.modal || context.mode !== 'view' || !context.booking) return;
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
            panel.dataset.renderKey = `error:${error}`;
            panel.innerHTML = `<div class="admin-booking-details-error"><strong>Không tải được thông tin người đặt.</strong><span>${escapeHtml(error)}</span></div>`;
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
        const webBooking = String(booking.source || '').toLowerCase() === 'website';
        const acceptedAt = formatAcceptedAt(details.policyAcceptedAtUtc);
        const policyText = details.policyAccepted
            ? `Đã đồng ý Nội quy & Chính sách${details.policyVersion ? ` v${Number(details.policyVersion)}` : ''}${acceptedAt ? ` · ${acceptedAt}` : ''}`
            : (webBooking
                ? 'Đang đồng bộ trạng thái đồng ý Nội quy & Chính sách từ lượt đặt web.'
                : 'Booking do nhân viên tạo · không yêu cầu xác nhận Nội quy & Chính sách.');
        const documents = Array.isArray(details.documents) ? details.documents : [];
        const sourceLabel = webBooking ? 'Đặt trên website' : (booking.source || 'Nhân viên đặt');
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
                <div><strong>Thông tin người đặt</strong><small>Thông tin liên hệ, số khách và giấy tờ khách đã cung cấp</small></div>
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

        if (webBooking && (!details.policyAccepted || documents.length < 2)) scheduleDocumentRetry(booking.id);
        else clearDocumentRetries(booking.id);
    }

    function detailIdentityHtml(bookingId, side, label, documents) {
        const found = documents.find(item => item.side === side);
        if (!found) return `<div class="admin-booking-detail-id"><strong>${label}</strong><span class="empty">Chưa nhận được ảnh</span></div>`;
        const url = documentUrl(bookingId, side);
        return `<div class="admin-booking-detail-id"><div class="admin-booking-id-label"><strong>${label}</strong><span>Đã nhận</span></div><a href="${url}" target="_blank" rel="noopener"><img src="${url}" alt="${label}" loading="eager" /></a></div>`;
    }

    function clearDocumentRetries(bookingId) {
        const timers = documentRetryTimers.get(bookingId) || [];
        timers.forEach(timer => clearTimeout(timer));
        documentRetryTimers.delete(bookingId);
    }

    function scheduleDocumentRetry(bookingId) {
        if (!bookingId || documentRetryTimers.has(bookingId)) return;
        const timers = [1200, 3600].map(delay => setTimeout(async () => {
            const context = currentContext();
            if (context?.mode !== 'view' || context.booking?.id !== bookingId) return;
            try {
                const details = await loadDetails(bookingId, true);
                renderDetail(context, details, null);
            } catch { }
        }, delay));
        documentRetryTimers.set(bookingId, timers);
    }

    function cardHtml(side, label) {
        return `
            <div class="admin-booking-id-card" data-admin-live-id-card="${side}" data-remove="false">
                <button type="button" class="admin-booking-id-picker" data-admin-live-id-pick="${side}">
                    <img data-admin-live-id-preview="${side}" alt="${label}" hidden />
                    <span class="admin-booking-id-empty" data-admin-live-id-empty="${side}"><b>${label}</b><small>Chọn ảnh nếu có</small></span>
                </button>
                <button type="button" class="admin-booking-id-remove" data-admin-live-id-remove="${side}" aria-label="Bỏ ${label}" hidden>×</button>
                <input type="file" data-admin-live-id-input="${side}" accept="image/jpeg,image/png,image/webp" capture="environment" hidden />
            </div>`;
    }

    function ensureCalendarForm(context) {
        if (!calendarRoot || !context || !['create', 'edit'].includes(context.mode)) return;
        const customerSection = [...context.modal.querySelectorAll('.booking-form-section')]
            .find(section => section.querySelector('.booking-form-title')?.textContent?.includes('Khách hàng'));
        if (!customerSection) return;

        let extra = customerSection.querySelector('[data-admin-live-extra-fields]');
        if (!extra) {
            extra = document.createElement('div');
            extra.className = 'admin-booking-extra-fields';
            extra.dataset.adminLiveExtraFields = 'true';
            extra.innerHTML = `
                <div class="field"><label>Email</label><input data-admin-live-email type="email" maxlength="254" autocomplete="email" placeholder="Không bắt buộc với nhân viên" /><small>Khách đặt web có email bắt buộc; nhân viên có thể để trống.</small></div>
                <div class="field"><label>Số lượng khách *</label><input data-admin-live-guests type="number" min="1" step="1" value="1" /><small data-admin-live-guest-hint></small></div>`;
            customerSection.querySelector('.form-grid')?.insertAdjacentElement('afterend', extra);
        }

        let identity = customerSection.querySelector('[data-admin-live-identity-editor]');
        if (!identity) {
            identity = document.createElement('div');
            identity.className = 'admin-booking-identity-editor';
            identity.dataset.adminLiveIdentityEditor = 'true';
            identity.innerHTML = `
                <div class="admin-booking-identity-editor-head"><div><strong>CCCD / giấy tờ tùy thân</strong><small>Nhân viên có thể bỏ qua. Ảnh được lưu trong kho riêng đã mã hóa.</small></div><span>Không bắt buộc</span></div>
                <div class="admin-booking-id-grid">${cardHtml('front', 'Mặt trước')}${cardHtml('back', 'Mặt sau')}</div>`;
            extra.insertAdjacentElement('afterend', identity);
            bindIdentityEditor(identity);
        }

        updateGuestLimit(context);
        if (context.mode === 'create') {
            if (!extra.dataset.hydratedBookingId) {
                extra.querySelector('[data-admin-live-email]').value = '';
                extra.querySelector('[data-admin-live-guests]').value = '1';
                extra.dataset.hydratedBookingId = 'new';
                renderEditDocuments(context, null);
            }
            return;
        }

        if (context.booking?.id && extra.dataset.hydratedBookingId !== context.booking.id) {
            const bookingId = context.booking.id;
            extra.dataset.hydratedBookingId = bookingId;
            loadDetails(bookingId, false)
                .then(details => {
                    const latest = currentContext();
                    if (latest?.mode !== 'edit' || latest.booking?.id !== bookingId) return;
                    const currentExtra = latest.modal.querySelector('[data-admin-live-extra-fields]');
                    if (!currentExtra) return;
                    currentExtra.querySelector('[data-admin-live-email]').value = details.customerEmail || '';
                    currentExtra.querySelector('[data-admin-live-guests]').value = String(Math.max(1, Number(details.guestCount || 1)));
                    updateGuestLimit(latest);
                    renderEditDocuments(latest, details);
                    if (Object.prototype.hasOwnProperty.call(details, 'note') && String(latest.vm.form?.note || '').startsWith('[Đặt web]')) {
                        latest.vm.form.note = details.note || '';
                    }
                })
                .catch(error => flash(error.message || 'Không thể tải thông tin khách.', 'error'));
        }
    }

    function updateGuestLimit(context) {
        const extra = context?.modal?.querySelector('[data-admin-live-extra-fields]');
        if (!extra) return;
        const max = currentCapacity(context);
        const input = extra.querySelector('[data-admin-live-guests]');
        const hint = extra.querySelector('[data-admin-live-guest-hint]');
        input.max = String(max);
        if (Number(input.value || 1) > max) input.value = String(max);
        hint.textContent = `Tối đa ${max} khách theo sức chứa của phòng.`;
    }

    function bindIdentityEditor(identity) {
        for (const side of ['front', 'back']) {
            const card = identity.querySelector(`[data-admin-live-id-card="${side}"]`);
            const input = identity.querySelector(`[data-admin-live-id-input="${side}"]`);
            const pick = identity.querySelector(`[data-admin-live-id-pick="${side}"]`);
            const remove = identity.querySelector(`[data-admin-live-id-remove="${side}"]`);
            pick.addEventListener('click', () => input.click());
            input.addEventListener('change', () => {
                const file = input.files?.[0] || null;
                if (file && file.size > 8 * 1024 * 1024) {
                    flash('Mỗi ảnh CCCD tối đa 8 MB.', 'error');
                    input.value = '';
                    return;
                }
                card.dataset.remove = 'false';
                renderPendingFile(card, side, file);
            });
            remove.addEventListener('click', event => {
                event.stopPropagation();
                input.value = '';
                card.dataset.remove = card.dataset.hasExisting === 'true' ? 'true' : 'false';
                renderPendingFile(card, side, null);
            });
        }
    }

    function renderPendingFile(card, side, file) {
        const image = card.querySelector(`[data-admin-live-id-preview="${side}"]`);
        const empty = card.querySelector(`[data-admin-live-id-empty="${side}"]`);
        const remove = card.querySelector(`[data-admin-live-id-remove="${side}"]`);
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

    function renderEditDocuments(context, details) {
        const identity = context?.modal?.querySelector('[data-admin-live-identity-editor]');
        if (!identity) return;
        const documents = Array.isArray(details?.documents) ? details.documents : [];
        for (const side of ['front', 'back']) {
            const card = identity.querySelector(`[data-admin-live-id-card="${side}"]`);
            const input = identity.querySelector(`[data-admin-live-id-input="${side}"]`);
            const existing = documents.some(item => item.side === side);
            card.dataset.hasExisting = existing ? 'true' : 'false';
            card.dataset.remove = 'false';
            card.dataset.existingUrl = existing && context.booking?.id ? documentUrl(context.booking.id, side) : '';
            input.value = '';
            renderPendingFile(card, side, null);
        }
    }

    function collectSupplement() {
        const context = currentContext();
        if (!calendarRoot || !context || !['create', 'edit'].includes(context.mode)) return null;
        const extra = context.modal.querySelector('[data-admin-live-extra-fields]');
        const identity = context.modal.querySelector('[data-admin-live-identity-editor]');
        if (!extra) return null;
        const emailInput = extra.querySelector('[data-admin-live-email]');
        const guestInput = extra.querySelector('[data-admin-live-guests]');
        const email = emailInput.value.trim();
        const guestCount = Number(guestInput.value || 0);
        const capacity = currentCapacity(context);
        if (email && !emailInput.checkValidity()) throw new Error('Email khách không hợp lệ.');
        if (!Number.isInteger(guestCount) || guestCount < 1 || guestCount > capacity)
            throw new Error(`Phòng này tối đa ${capacity} khách.`);

        const sideValue = side => {
            if (!identity) return { file: null, remove: false };
            const input = identity.querySelector(`[data-admin-live-id-input="${side}"]`);
            const card = identity.querySelector(`[data-admin-live-id-card="${side}"]`);
            return { file: input?.files?.[0] || null, remove: card?.dataset.remove === 'true' };
        };
        return { email, guestCount, front: sideValue('front'), back: sideValue('back') };
    }

    async function persistSupplement(booking, supplement) {
        if (!booking?.id || !supplement) return;
        try {
            await rawPut(guestDetailsUrl(booking.id), {
                customerEmail: supplement.email || null,
                guestCount: supplement.guestCount
            });
        } catch (error) {
            flash(`Lượt đặt đã lưu nhưng email/số khách chưa lưu được: ${error.message || 'lỗi không xác định'}`, 'error');
            return;
        }

        const failures = [];
        for (const [side, value] of [['front', supplement.front], ['back', supplement.back]]) {
            try {
                if (value.file) {
                    if (!rawPostForm) throw new Error('Trình duyệt không hỗ trợ tải file qua API hiện tại.');
                    const form = new FormData();
                    form.append('file', value.file, value.file.name || `${side}.jpg`);
                    await rawPostForm(identityUrl(booking.id, side), form);
                } else if (value.remove) {
                    await rawDelete(identityUrl(booking.id, side));
                }
            } catch (error) {
                failures.push(`${side === 'front' ? 'mặt trước' : 'mặt sau'}: ${error.message || 'không thể lưu'}`);
            }
        }
        detailsCache.delete(booking.id);
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
        if (!calendarRoot || !isBookingCreateUrl(url)) return rawPost(url, data, headers);
        const supplement = collectSupplement();
        const result = await rawPost(url, data, headers);
        await persistSupplement(result, supplement);
        return result;
    };

    window.DeLongApi.put = async function (url, data) {
        if (!calendarRoot || !isBookingUpdateUrl(url)) return rawPut(url, data);
        const supplement = collectSupplement();
        const result = await rawPut(url, data);
        await persistSupplement(result, supplement);
        return result;
    };

    function bookingsUrl() {
        if (!calendarRoot) return `/api/admin/properties/${propertyId}/bookings`;
        const startDate = initial.startDate;
        const utcOffset = initial.utcOffset || '+07:00';
        if (!startDate) return `/api/admin/properties/${propertyId}/bookings`;
        const from = `${startDate}T00:00:00${utcOffset}`;
        const to = `${addDays(startDate, 7)}T00:00:00${utcOffset}`;
        const query = new URLSearchParams({ from, to });
        return `/api/admin/properties/${propertyId}/bookings?${query}`;
    }

    async function refreshBookings() {
        const live = vm();
        if (!live) return;
        if (refreshInFlight) {
            refreshQueued = true;
            return;
        }
        refreshInFlight = true;
        try {
            const rows = await rawGet(bookingsUrl());
            if (!Array.isArray(rows)) return;
            live.bookings.splice(0, live.bookings.length, ...rows);
            if (live.selectedBooking?.id) {
                const latest = rows.find(item => item.id === live.selectedBooking.id);
                if (latest) live.selectedBooking = latest;
            }
            if (live.selectedBooking?.id) detailsCache.delete(live.selectedBooking.id);
            queueSync();
        } catch {
            // A later SSE burst / poll will retry without interrupting staff work.
        } finally {
            refreshInFlight = false;
            if (refreshQueued) {
                refreshQueued = false;
                setTimeout(refreshBookings, 80);
            }
        }
    }

    function refreshBurst() {
        [0, 900, 2800].forEach(delay => {
            const timer = setTimeout(() => {
                refreshTimers.delete(timer);
                refreshBookings();
            }, delay);
            refreshTimers.add(timer);
        });
    }

    function queueSync() {
        if (syncQueued) return;
        syncQueued = true;
        requestAnimationFrame(() => {
            syncQueued = false;
            sync();
        });
    }

    function sync() {
        const context = currentContext();
        if (!context) return;
        if (context.mode === 'view' && context.booking?.id) {
            const cached = detailsCache.get(context.booking.id) || null;
            renderDetail(context, cached, null);
            if (!cached && !detailsLoading.has(context.booking.id)) {
                loadDetails(context.booking.id, false)
                    .then(details => {
                        const latest = currentContext();
                        if (latest?.mode === 'view' && latest.booking?.id === context.booking.id)
                            renderDetail(latest, details, null);
                    })
                    .catch(error => {
                        const latest = currentContext();
                        if (latest?.mode === 'view' && latest.booking?.id === context.booking.id)
                            renderDetail(latest, null, error.message || 'Không thể tải thông tin khách.');
                    });
            }
            return;
        }
        if (['create', 'edit'].includes(context.mode)) ensureCalendarForm(context);
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
    window.addEventListener('focus', refreshBookings);
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) refreshBookings();
    });

    const observer = new MutationObserver(queueSync);
    observer.observe(root, { childList: true, subtree: true });

    function waitForVue(attempt) {
        if (vm()) {
            refreshBookings();
            queueSync();
            pollTimer = setInterval(() => {
                if (!document.hidden) refreshBookings();
            }, 10000);
            return;
        }
        if (attempt >= 100) return;
        setTimeout(() => waitForVue(attempt + 1), 100);
    }

    window.addEventListener('beforeunload', () => {
        if (pollTimer) clearInterval(pollTimer);
        refreshTimers.forEach(timer => clearTimeout(timer));
        documentRetryTimers.forEach(timers => timers.forEach(timer => clearTimeout(timer)));
        previewUrls.forEach(url => URL.revokeObjectURL(url));
        observer.disconnect();
    }, { once: true });

    waitForVue(0);
})();
