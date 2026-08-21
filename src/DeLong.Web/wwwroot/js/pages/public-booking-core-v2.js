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
        savedIdentity: false,
        accountHasIdentity: false,
        authenticatedPhone: '',
        accountRestoreStarted: false,
        selectionSignature: '',
        baseTotal: 0
        ,accountSettings: null,
        accountRegistrationCompleted: false
    };

    function money(value) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value || 0));
    }

    function escapeHtml(value) {
        return String(value || '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
    }

    function normalizePhone(value) {
        return String(value || '').replace(/\D/g, '');
    }

    function updateSavedIdentityUi() {
        const identitySection = root.querySelector('.booking-id-section');
        const existingNote = root.querySelector('.booking-saved-identity-note');
        if (!identitySection) return;
        identitySection.hidden = state.savedIdentity;
        if (!state.savedIdentity) {
            existingNote?.remove();
            return;
        }
        if (existingNote) return;
        const note = document.createElement('p');
        note.className = 'booking-saved-identity-note';
        note.textContent = 'Tài khoản đã có CCCD. Lượt đặt này sẽ dùng bản mới nhất đang lưu trong tài khoản.';
        identitySection.before(note);
    }

    async function restoreCustomerAccount() {
        try {
            const profile = await DeLongApi.get('/api/customer/account/profile');
            const nameInput = root.querySelector('[data-booking-customer-name]');
            const phoneInput = root.querySelector('[data-booking-customer-phone]');
            const emailInput = root.querySelector('[data-booking-email]');
            if (nameInput) { nameInput.value = profile.name || ''; nameInput.dispatchEvent(new Event('input', { bubbles: true })); }
            if (phoneInput) { phoneInput.value = profile.phone || ''; phoneInput.dispatchEvent(new Event('input', { bubbles: true })); }
            if (emailInput) { emailInput.value = profile.email || ''; state.email = profile.email || ''; }
            state.authenticatedPhone = normalizePhone(profile.phone);
            state.accountHasIdentity = !!profile.hasIdentityDocuments;
            state.savedIdentity = state.accountHasIdentity;
            updateSavedIdentityUi();
            const panel = root.querySelector('[data-booking-account-panel]');
            if (panel) {
                panel.hidden = false;
                panel.innerHTML = '<div class="booking-account-success"><strong>Đã điền thông tin từ tài khoản</strong><small>Bạn không cần nhập lại mật khẩu khi dùng đúng số điện thoại tài khoản.</small></div>';
            }
        } catch { }
    }

    async function checkCustomerAccount(wrapper) {
        const phone = root.querySelector('[data-booking-customer-phone]')?.value?.trim() || '';
        const panel = wrapper?.querySelector('[data-booking-account-panel]');
        if (!panel || phone.replace(/\D/g, '').length < 8) { if (panel) panel.hidden = true; return; }
        if (state.authenticatedPhone && normalizePhone(phone) === state.authenticatedPhone) {
            state.savedIdentity = state.accountHasIdentity;
            updateSavedIdentityUi();
            panel.hidden = false;
            panel.innerHTML = '<div class="booking-account-success"><strong>Đang dùng tài khoản đã đăng nhập</strong><small>Thông tin đã được điền sẵn, không cần nhập lại mật khẩu.</small></div>';
            return;
        }
        try {
            const query = new URLSearchParams({ phone });
            if (siteSlug) query.set('siteSlug', siteSlug);
            const status = await DeLongApi.get(`/api/public/customer-account/status?${query}`);
            panel.hidden = false;
            const hasAccount = status.hasAccount ?? status.exists;
            if (hasAccount) {
                panel.innerHTML = `<div class="booking-account-summary"><div><strong>Đã tìm thấy tài khoản</strong><small>Đăng nhập để tự điền hồ sơ và dùng CCCD đã lưu.</small></div></div><div class="booking-account-form"><label><span>Mật khẩu</span><input type="password" autocomplete="current-password" placeholder="Nhập mật khẩu" data-quick-password /></label><button type="button" data-quick-login>Đăng nhập & điền nhanh</button></div><p class="booking-account-message" data-account-message hidden></p>`;
                panel.querySelector('[data-quick-login]').addEventListener('click', async () => {
                    const button = panel.querySelector('[data-quick-login]');
                    try {
                        button.disabled = true; button.textContent = 'Đang đăng nhập…';
                        await DeLongApi.refreshAntiforgery();
                        await DeLongApi.post('/api/public/customer-account/login', { phone, password: panel.querySelector('[data-quick-password]').value, rememberMe: true });
                        await DeLongApi.refreshAntiforgery();
                        await restoreCustomerAccount(); panel.innerHTML = '<div class="booking-account-success"><strong>Đã điền thông tin từ tài khoản</strong><small>Bạn có thể tiếp tục hoàn tất yêu cầu đặt phòng.</small></div>';
                    } catch (error) {
                        const message = panel.querySelector('[data-account-message]');
                        message.hidden = false; message.textContent = error.message || 'Không thể đăng nhập.';
                        button.disabled = false; button.textContent = 'Đăng nhập & điền nhanh';
                    }
                });
            } else if (state.accountSettings?.registrationEnabled) {
                const accountTitle = status.exists ? 'Bạn đã từng đặt phòng tại đây' : 'Đặt phòng nhanh hơn vào lần sau';
                const accountDescription = status.exists
                    ? 'Hồ sơ khách đã có, nhưng chưa có tài khoản đăng nhập. Tạo mật khẩu để dùng lại thông tin lần sau.'
                    : (state.accountSettings.benefitText || 'Lưu hồ sơ và không phải nhập lại thông tin.');
                panel.innerHTML = `<div class="booking-account-summary"><div><strong>${escapeHtml(accountTitle)}</strong><small>${escapeHtml(accountDescription)}</small></div><button type="button" data-quick-register-open>Tạo tài khoản</button></div><div class="booking-account-register" data-quick-register-form hidden><label><span>Tạo mật khẩu</span><input type="password" autocomplete="new-password" placeholder="Ít nhất 8 ký tự" data-quick-new-password /></label><label class="booking-account-terms"><input type="checkbox" data-quick-terms /><span>Tôi đồng ý <button type="button" data-account-terms-open>${escapeHtml(state.accountSettings.termsTitle || 'điều khoản tài khoản')}</button></span></label><p class="booking-account-submit-note">Tài khoản sẽ được tạo cùng lúc khi bạn gửi yêu cầu đặt phòng.</p><p class="booking-account-message" data-account-message hidden></p></div>`;
                panel.querySelector('[data-quick-register-open]').addEventListener('click', event => { event.currentTarget.hidden = true; panel.querySelector('[data-quick-register-form]').hidden = false; });
                panel.querySelector('[data-account-terms-open]').addEventListener('click', openAccountTerms);
            } else panel.hidden = true;
        } catch { panel.hidden = true; }
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
        modal.querySelector('.booking-policy-copy').innerHTML = state.policy.policyText || '';
        modal.classList.add('is-open');
        document.documentElement.classList.add('booking-policy-open');
    }

    function closePolicy() {
        document.getElementById('booking-policy-modal')?.classList.remove('is-open');
        document.getElementById('booking-account-terms-modal')?.classList.remove('is-open');
        document.documentElement.classList.remove('booking-policy-open');
    }

    function openAccountTerms() {
        if (!state.accountSettings) return;
        let modal = document.getElementById('booking-account-terms-modal');
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'booking-account-terms-modal';
            modal.className = 'booking-policy-modal';
            modal.innerHTML = `
                <div class="booking-policy-dialog" role="dialog" aria-modal="true" aria-labelledby="booking-account-terms-title">
                    <div class="booking-policy-head"><div><span>TÀI KHOẢN KHÁCH</span><h2 id="booking-account-terms-title"></h2></div><button type="button" aria-label="Đóng">×</button></div>
                    <div class="booking-policy-copy"></div>
                    <div class="booking-policy-actions"><button type="button" class="public-btn public-btn-primary">Đã đọc</button></div>
                </div>`;
            document.body.appendChild(modal);
            modal.addEventListener('click', event => { if (event.target === modal) closePolicy(); });
            modal.querySelector('.booking-policy-head button').addEventListener('click', closePolicy);
            modal.querySelector('.booking-policy-actions button').addEventListener('click', closePolicy);
        }
        modal.querySelector('#booking-account-terms-title').textContent = state.accountSettings.termsTitle || 'Điều khoản tài khoản khách';
        modal.querySelector('.booking-policy-copy').innerHTML = state.accountSettings.termsHtml || '<p>Chưa có nội dung điều khoản.</p>';
        modal.classList.add('is-open');
        document.documentElement.classList.add('booking-policy-open');
        modal.querySelector('.booking-policy-head button').focus();
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
            <div class="booking-account-panel" data-booking-account-panel hidden></div>
            <div class="booking-v2-contact-row">
                <label class="booking-email-field"><span>Email *</span><input data-booking-email type="email" maxlength="254" autocomplete="off" placeholder="ten@email.com" /><small aria-hidden="true">&nbsp;</small></label>
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
        const phoneInput = root.querySelector('[data-booking-customer-phone]');
        if (phoneInput && !phoneInput.dataset.customerAccountBound) {
            phoneInput.dataset.customerAccountBound = 'true';
            phoneInput.addEventListener('input', () => {
                if (!state.authenticatedPhone || normalizePhone(phoneInput.value) === state.authenticatedPhone) return;
                state.savedIdentity = false;
                updateSavedIdentityUi();
            });
            phoneInput.addEventListener('blur', () => checkCustomerAccount(wrapper));
        }
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
        updateSavedIdentityUi();

        if (root.dataset.customerAuthenticated === 'true' && !state.accountRestoreStarted) {
            state.accountRestoreStarted = true;
            restoreCustomerAccount();
        }

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
        if (state.policy.requireIdentityDocuments && !state.savedIdentity && (!state.front || !state.back)) return 'Vui lòng chọn ảnh CCCD mặt trước và mặt sau.';
        const registrationForm = wrapper.querySelector('[data-quick-register-form]');
        if (registrationForm && !registrationForm.hidden && !state.accountRegistrationCompleted) {
            if ((registrationForm.querySelector('[data-quick-new-password]')?.value || '').length < 8)
                return 'Mật khẩu tài khoản cần ít nhất 8 ký tự.';
            if (!registrationForm.querySelector('[data-quick-terms]')?.checked)
                return 'Bạn cần đồng ý điều khoản tài khoản khách.';
        }
        state.email = email.value.trim();
        return null;
    }

    async function createPendingCustomerAccount(originalPost) {
        const wrapper = root.querySelector('[data-booking-v2-fields]');
        const form = wrapper?.querySelector('[data-quick-register-form]');
        if (!form || form.hidden || state.accountRegistrationCompleted) return;

        const phone = root.querySelector('[data-booking-customer-phone]')?.value?.trim() || '';
        const name = root.querySelector('[data-booking-customer-name]')?.value?.trim() || '';
        const password = form.querySelector('[data-quick-new-password]').value;
        const query = siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : '';
        await DeLongApi.refreshAntiforgery();
        await originalPost(`/api/public/customer-account/register${query}`, {
            phone,
            password,
            name,
            email: state.email || null,
            termsAccepted: true,
            termsVersion: state.accountSettings.termsVersion
        });
        await DeLongApi.refreshAntiforgery();
        const verificationQuery = new URLSearchParams({ phone });
        if (siteSlug) verificationQuery.set('siteSlug', siteSlug);
        const verification = await DeLongApi.get(`/api/public/customer-account/status?${verificationQuery}`);
        if (!(verification.hasAccount ?? verification.exists))
            throw new Error('Tài khoản chưa được lưu. Yêu cầu đặt phòng chưa được gửi.');
        state.accountRegistrationCompleted = true;
        wrapper.querySelector('[data-booking-account-panel]').innerHTML = '<div class="booking-account-success"><strong>Đã tạo tài khoản</strong><small>Yêu cầu đặt phòng đang được gửi…</small></div>';
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
            await createPendingCustomerAccount(originalPost);
            const payload = {
                ...data,
                customerEmail: state.email,
                guestCount: state.guestCount,
                policyAccepted: true,
                policyVersion: Number(state.policy.policyVersion || 1),
                hasIdentityFront: !!state.front || state.savedIdentity,
                hasIdentityBack: !!state.back || state.savedIdentity
            };
            const result = await originalPost(url, payload, headers);
            const requestKey = headers?.['Idempotency-Key'] || headers?.['idempotency-key'] || '';
            if (result?.bookingId && requestKey) {
                if (state.front) await uploadIdentity(result.bookingId, 'front', state.front, requestKey);
                if (state.back) await uploadIdentity(result.bookingId, 'back', state.back, requestKey);
            }
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
    DeLongApi.get(siteSlug ? `/api/public/customer-account/settings?siteSlug=${encodeURIComponent(siteSlug)}` : '/api/public/customer-account/settings')
        .then(settings => { state.accountSettings = settings; })
        .catch(() => { state.accountSettings = null; });
    DeLongApi.get(policyUrl)
        .then(policy => {
            state.policy = policy;
            state.guestCount = Math.max(1, Number(policy.includedGuests || 2));
            state.email = '';
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
