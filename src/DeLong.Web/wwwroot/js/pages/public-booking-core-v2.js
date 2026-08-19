(function () {
    const root = document.getElementById('public-booking-page');
    if (!root || !window.DeLongApi) return;

    const pageData = (() => {
        try { return JSON.parse(document.getElementById('public-booking-data')?.textContent || '{}'); }
        catch { return {}; }
    })();
    const siteSlug = pageData.siteSlug || '';
    const policyUrl = siteSlug ? `/api/public/booking-policy?siteSlug=${encodeURIComponent(siteSlug)}` : '/api/public/booking-policy';
    const state = {
        policy: null,
        email: '',
        guestCount: 2,
        front: null,
        back: null,
        frontUrl: '',
        backUrl: '',
        restored: false,
        selectionSignature: '',
        baseTotal: 0
    };

    function money(value) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value || 0));
    }

    function escapeHtml(value) {
        return String(value || '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
    }

    function savedContact() {
        try { return JSON.parse(localStorage.getItem('delong.booking.contact.v1') || '{}'); }
        catch { return {}; }
    }

    function persistContact() {
        const name = root.querySelector('input[autocomplete="name"]')?.value?.trim() || '';
        const phone = root.querySelector('input[autocomplete="tel"]')?.value?.trim() || '';
        const email = root.querySelector('[data-booking-email]')?.value?.trim() || state.email;
        try { localStorage.setItem('delong.booking.contact.v1', JSON.stringify({ name, phone, email })); }
        catch { }
    }

    function restoreContact() {
        if (state.restored) return;
        const saved = savedContact();
        const nameInput = root.querySelector('input[autocomplete="name"]');
        const phoneInput = root.querySelector('input[autocomplete="tel"]');
        const emailInput = root.querySelector('[data-booking-email]');
        if (!nameInput || !phoneInput || !emailInput) return;
        if (!nameInput.value && saved.name) {
            nameInput.value = saved.name;
            nameInput.dispatchEvent(new Event('input', { bubbles: true }));
        }
        if (!phoneInput.value && saved.phone) {
            phoneInput.value = saved.phone;
            phoneInput.dispatchEvent(new Event('input', { bubbles: true }));
        }
        if (!emailInput.value && saved.email) {
            emailInput.value = saved.email;
            state.email = saved.email;
        }
        state.restored = true;
    }

    function currentCapacity() {
        const text = root.querySelector('.public-booking-room.active .public-booking-room-copy small')?.textContent || '';
        const match = text.match(/(\d+)\s*người/i);
        const capacity = match ? Number(match[1]) : 0;
        return Number.isFinite(capacity) && capacity > 0 ? capacity : 50;
    }

    function currentSelectionSignature() {
        const room = root.querySelector('.public-booking-room.active .public-booking-room-copy strong')?.textContent?.trim() || '';
        const quickRate = root.querySelector('.public-rate-choice.active span strong')?.textContent?.trim() || '';
        const nightlyRate = root.querySelector('.public-nightly-summary-copy > strong')?.textContent?.trim() || '';
        const date = root.querySelector('.public-booking-mobile-bar small')?.textContent?.trim() || '';
        return [room, quickRate || nightlyRate, date].join('|');
    }

    function parseMoneyText(value) {
        const digits = String(value || '').replace(/[^0-9]/g, '');
        return digits ? Number(digits) : 0;
    }

    function renderedBaseTotal() {
        return parseMoneyText(root.querySelector('.public-summary-content dl .total dd')?.textContent || '');
    }

    function syncSelection() {
        const signature = currentSelectionSignature();
        if (signature && signature !== state.selectionSignature) {
            state.selectionSignature = signature;
            state.baseTotal = renderedBaseTotal();
        } else if (!state.baseTotal) {
            state.baseTotal = renderedBaseTotal();
        }
        const capacity = currentCapacity();
        state.guestCount = Math.max(1, Math.min(state.guestCount, capacity));
        return capacity;
    }

    function currentSurcharge() {
        if (!state.policy) return 0;
        return Math.max(0, state.guestCount - Number(state.policy.includedGuests || 0)) * Number(state.policy.extraGuestFeePerPerson || 0);
    }

    function currentEstimatedTotal() {
        return Math.max(0, Number(state.baseTotal || 0)) + currentSurcharge();
    }

    function setText(node, text) {
        if (node && node.textContent !== text) node.textContent = text;
    }

    function syncDisplayedTotal() {
        if (!state.baseTotal) return;
        const text = money(currentEstimatedTotal());
        setText(root.querySelector('.public-summary-content dl .total dd'), text);
        setText(root.querySelector('.public-booking-mobile-bar strong'), text);
    }

    function updateGuestSummary(container) {
        if (!container || !state.policy) return;
        const capacity = syncSelection();
        const count = container.querySelector('[data-booking-guest-count]');
        setText(count, String(state.guestCount));

        const minus = container.querySelector('[data-guest-minus]');
        const plus = container.querySelector('[data-guest-plus]');
        if (minus) minus.disabled = state.guestCount <= 1;
        if (plus) {
            plus.disabled = state.guestCount >= capacity;
            plus.title = state.guestCount >= capacity ? `Phòng này tối đa ${capacity} khách.` : 'Tăng số khách';
        }

        const summary = container.querySelector('[data-booking-guest-summary]');
        const included = Math.min(Number(state.policy.includedGuests || 2), capacity);
        const fee = Number(state.policy.extraGuestFeePerPerson || 0);
        const surcharge = currentSurcharge();
        const total = currentEstimatedTotal();
        const summaryText = surcharge > 0
            ? `Phụ thu ${money(surcharge)} · Tổng dự kiến ${money(total)} · Tối đa ${capacity} khách.`
            : `${included} khách đầu đã gồm trong giá${fee > 0 && capacity > included ? `; từ khách tiếp theo +${money(fee)}/người` : ''}. Tối đa ${capacity} khách.`;
        setText(summary, summaryText);
        syncDisplayedTotal();
    }

    function revokePreview(side) {
        const key = side === 'front' ? 'frontUrl' : 'backUrl';
        if (state[key]) URL.revokeObjectURL(state[key]);
        state[key] = '';
    }

    function setDocument(side, file, card) {
        revokePreview(side);
        state[side] = file || null;
        const preview = card.querySelector('img');
        const empty = card.querySelector('.booking-id-empty');
        const remove = card.querySelector('[data-remove-id]');
        if (file) {
            const key = side === 'front' ? 'frontUrl' : 'backUrl';
            state[key] = URL.createObjectURL(file);
            preview.src = state[key];
            preview.hidden = false;
            empty.hidden = true;
            remove.hidden = false;
            card.classList.add('has-image');
        } else {
            preview.removeAttribute('src');
            preview.hidden = true;
            empty.hidden = false;
            remove.hidden = true;
            card.classList.remove('has-image');
        }
    }

    function createIdentityCard(side, label) {
        const card = document.createElement('div');
        card.className = 'booking-id-card';
        card.dataset.identitySide = side;
        card.innerHTML = `
            <button type="button" class="booking-id-picker" data-pick-id>
                <img alt="${escapeHtml(label)}" hidden />
                <span class="booking-id-empty"><span class="booking-id-icon" aria-hidden="true">▧+</span><strong>${escapeHtml(label)}</strong><small>Chạm để chọn ảnh</small></span>
            </button>
            <button type="button" class="booking-id-remove" data-remove-id title="Bỏ ảnh" aria-label="Bỏ ${escapeHtml(label)}" hidden>×</button>
            <input type="file" accept="image/jpeg,image/png,image/webp" capture="environment" hidden />`;
        const input = card.querySelector('input[type="file"]');
        card.querySelector('[data-pick-id]').addEventListener('click', () => input.click());
        input.addEventListener('change', () => {
            const file = input.files?.[0] || null;
            if (file && file.size > 8 * 1024 * 1024) {
                window.alert('Mỗi ảnh CCCD tối đa 8 MB.');
                input.value = '';
                return;
            }
            setDocument(side, file, card);
        });
        card.querySelector('[data-remove-id]').addEventListener('click', event => {
            event.stopPropagation();
            input.value = '';
            setDocument(side, null, card);
        });
        return card;
    }

    function openPolicy() {
        if (!state.policy) return;
        let modal = document.getElementById('booking-policy-modal');
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'booking-policy-modal';
            modal.className = 'booking-policy-modal';
            modal.innerHTML = `
                <div class="booking-policy-dialog" role="dialog" aria-modal="true" aria-labelledby="booking-policy-title">
                    <div class="booking-policy-head"><div><span>NỘI QUY</span><h2 id="booking-policy-title"></h2></div><button type="button" aria-label="Đóng">×</button></div>
                    <div class="booking-policy-copy"></div>
                    <div class="booking-policy-actions"><button type="button" class="public-btn public-btn-primary">Đã đọc</button></div>
                </div>`;
            document.body.appendChild(modal);
            modal.addEventListener('click', event => { if (event.target === modal) closePolicy(); });
            modal.querySelector('.booking-policy-head button').addEventListener('click', closePolicy);
            modal.querySelector('.booking-policy-actions button').addEventListener('click', closePolicy);
        }
        modal.querySelector('#booking-policy-title').textContent = state.policy.policyTitle || 'Nội quy & Chính sách';
        modal.querySelector('.booking-policy-copy').textContent = state.policy.policyText || '';
        modal.classList.add('is-open');
        document.documentElement.classList.add('booking-policy-open');
    }

    function closePolicy() {
        document.getElementById('booking-policy-modal')?.classList.remove('is-open');
        document.documentElement.classList.remove('booking-policy-open');
    }

    function injectFields() {
        if (!state.policy) return;
        const grid = root.querySelector('.public-contact-step .public-form-grid');
        if (!grid) return;
        const existing = grid.querySelector('[data-booking-v2-fields]');
        if (existing) {
            updateGuestSummary(existing);
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'public-form-full booking-v2-fields';
        wrapper.dataset.bookingV2Fields = 'true';
        wrapper.innerHTML = `
            <div class="booking-v2-contact-row">
                <label class="booking-email-field"><span>Email *</span><input data-booking-email type="email" maxlength="254" autocomplete="email" placeholder="ten@email.com" /></label>
                <div class="booking-guest-field">
                    <span>Số lượng khách *</span>
                    <div class="booking-guest-stepper"><button type="button" data-guest-minus aria-label="Giảm số khách">−</button><strong data-booking-guest-count>${state.guestCount}</strong><button type="button" data-guest-plus aria-label="Tăng số khách">+</button></div>
                    <small data-booking-guest-summary></small>
                </div>
            </div>
            <div class="booking-id-section">
                <div class="booking-id-heading"><div><strong>CCCD / giấy tờ tùy thân${state.policy.requireIdentityDocuments ? ' *' : ''}</strong><small>Ảnh được mã hóa trước khi lưu xuống ổ đĩa; không đưa vào thư viện ảnh công khai.</small></div><span>${state.policy.requireIdentityDocuments ? 'Bắt buộc' : 'Không bắt buộc'}</span></div>
                <div class="booking-id-grid" data-id-grid></div>
            </div>
            <label class="booking-policy-check"><input type="checkbox" data-policy-accepted /><span>Tôi đã đọc và đồng ý với <button type="button" data-open-policy>${escapeHtml(state.policy.policyTitle || 'Nội quy & Chính sách')}</button>.</span></label>`;

        const note = grid.querySelector('.public-form-full');
        if (note) grid.insertBefore(wrapper, note);
        else grid.appendChild(wrapper);

        const email = wrapper.querySelector('[data-booking-email]');
        email.value = state.email || '';
        email.addEventListener('input', () => { state.email = email.value; });
        wrapper.querySelector('[data-guest-minus]').addEventListener('click', () => {
            state.guestCount = Math.max(1, state.guestCount - 1);
            updateGuestSummary(wrapper);
        });
        wrapper.querySelector('[data-guest-plus]').addEventListener('click', () => {
            const capacity = currentCapacity();
            state.guestCount = Math.min(capacity, state.guestCount + 1);
            updateGuestSummary(wrapper);
        });
        wrapper.querySelector('[data-open-policy]').addEventListener('click', openPolicy);
        const idGrid = wrapper.querySelector('[data-id-grid]');
        idGrid.appendChild(createIdentityCard('front', 'Mặt trước'));
        idGrid.appendChild(createIdentityCard('back', 'Mặt sau'));
        updateGuestSummary(wrapper);
        restoreContact();

        const notice = root.querySelector('.public-booking-notice');
        if (notice) notice.innerHTML = `Sau khi gửi, hệ thống <strong>giữ phòng tạm ${Number(state.policy.publicHoldMinutes || 3)} phút</strong> trên server để tránh người khác đặt trùng. Nhân viên vẫn cần xác nhận lượt đặt.`;
    }

    function validateExtraFields() {
        const wrapper = root.querySelector('[data-booking-v2-fields]');
        if (!wrapper || !state.policy) return 'Đang tải thông tin đặt phòng. Vui lòng thử lại sau vài giây.';
        const email = wrapper.querySelector('[data-booking-email]');
        if (!email.value.trim() || !email.checkValidity()) return 'Vui lòng nhập email hợp lệ.';
        const capacity = currentCapacity();
        if (state.guestCount < 1) return 'Vui lòng chọn số lượng khách.';
        if (state.guestCount > capacity) return `Phòng này tối đa ${capacity} khách.`;
        if (!wrapper.querySelector('[data-policy-accepted]').checked) return `Bạn cần đọc và đồng ý với ${state.policy.policyTitle || 'Nội quy & Chính sách'}.`;
        if (state.policy.requireIdentityDocuments && (!state.front || !state.back)) return 'Vui lòng chọn ảnh CCCD mặt trước và mặt sau.';
        state.email = email.value.trim();
        return null;
    }

    async function uploadIdentity(bookingId, side, file, requestKey) {
        const form = new FormData();
        form.append('file', file, file.name || `${side}.jpg`);
        const query = siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : '';
        const csrf = document.querySelector('meta[name="csrf-token"]')?.content || '';
        const response = await fetch(`/api/public/booking-requests/${encodeURIComponent(bookingId)}/identity-documents/${side}${query}`, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { Accept: 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': requestKey },
            body: form
        });
        if (response.ok) return;
        let message = 'Không thể tải ảnh CCCD.';
        try {
            const payload = await response.json();
            const firstValidation = payload?.errors && typeof payload.errors === 'object'
                ? Object.values(payload.errors).flat().find(value => typeof value === 'string' && value.trim())
                : null;
            message = payload.detail || firstValidation || payload.title || message;
        } catch { }
        const error = new Error(`Lượt đặt đã được tạo nhưng ${message} Bấm gửi lại để hệ thống thử tải ảnh lần nữa.`);
        error.identityUploadFailed = true;
        throw error;
    }

    function wrapBookingPost() {
        if (window.DeLongApi.__bookingCoreV2Wrapped) return;
        const originalPost = window.DeLongApi.post.bind(window.DeLongApi);
        window.DeLongApi.post = async function (url, data, headers) {
            if (!String(url).includes('/api/public/booking-requests') || String(url).includes('/identity-documents/'))
                return originalPost(url, data, headers);

            const validation = validateExtraFields();
            if (validation) throw new Error(validation);
            const payload = {
                ...data,
                customerEmail: state.email,
                guestCount: state.guestCount,
                policyAccepted: true,
                policyVersion: Number(state.policy.policyVersion || 1),
                hasIdentityFront: !!state.front,
                hasIdentityBack: !!state.back
            };
            const result = await originalPost(url, payload, headers);
            const requestKey = headers?.['Idempotency-Key'] || headers?.['idempotency-key'] || '';
            if (result?.bookingId && requestKey) {
                if (state.front) await uploadIdentity(result.bookingId, 'front', state.front, requestKey);
                if (state.back) await uploadIdentity(result.bookingId, 'back', state.back, requestKey);
            }
            persistContact();
            return result;
        };
        window.DeLongApi.__bookingCoreV2Wrapped = true;
    }

    const observer = new MutationObserver(() => {
        const wrapper = root.querySelector('[data-booking-v2-fields]');
        if (!wrapper) {
            injectFields();
            return;
        }
        if (currentSelectionSignature() !== state.selectionSignature) updateGuestSummary(wrapper);
    });
    observer.observe(root, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') closePolicy(); });
    wrapBookingPost();
    DeLongApi.get(policyUrl)
        .then(policy => {
            state.policy = policy;
            state.guestCount = Math.max(1, Number(policy.includedGuests || 2));
            state.email = savedContact().email || '';
            injectFields();
        })
        .catch(() => {
            state.policy = {
                publicMaxNights: 3,
                includedGuests: 2,
                extraGuestFeePerPerson: 100000,
                requireIdentityDocuments: false,
                policyTitle: 'Nội quy & Chính sách',
                policyText: 'Vui lòng liên hệ cơ sở để xem nội quy hiện hành.',
                policyVersion: 1,
                publicHoldMinutes: 3
            };
            injectFields();
        });
})();
