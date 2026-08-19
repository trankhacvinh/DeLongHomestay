(function () {
    const root = document.getElementById('settings-page');
    if (!root || !window.DeLongApi) return;
    const initial = (() => {
        try { return JSON.parse(document.getElementById('settings-page-data')?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = initial.propertyId;
    if (!propertyId) return;
    let policy = null;
    let saving = false;

    function numberValue(panel, name, fallback) {
        const value = Number(panel.querySelector(`[data-policy-field="${name}"]`)?.value);
        return Number.isFinite(value) ? value : fallback;
    }

    function render() {
        if (!policy || root.querySelector('[data-booking-policy-settings]')) return;
        const anchor = root.querySelector('.settings-room-grid');
        if (!anchor) return;
        const panel = document.createElement('section');
        panel.className = 'panel booking-policy-settings';
        panel.dataset.bookingPolicySettings = 'true';
        panel.innerHTML = `
            <div class="panel-head">
                <div><h2>Quy tắc đặt phòng online</h2><p class="small muted">Giới hạn dành cho khách đặt trên website. Nhân viên đặt trong quản trị không bị giới hạn số đêm.</p></div>
                <span class="pill">Giữ phòng ${Number(policy.publicHoldMinutes || 3)} phút</span>
            </div>
            <div class="panel-body">
                <div class="booking-policy-settings-grid">
                    <div class="field"><label>Đặt tối đa</label><input data-policy-field="publicMaxNights" type="number" min="1" max="14" value="${Number(policy.publicMaxNights || 3)}" /><small>đêm / một lượt online</small></div>
                    <div class="field"><label>Đã gồm trong giá</label><input data-policy-field="includedGuests" type="number" min="1" max="50" value="${Number(policy.includedGuests || 2)}" /><small>khách trước khi tính phụ thu</small></div>
                    <div class="field"><label>Phụ thu mỗi khách</label><input data-policy-field="extraGuestFeePerPerson" type="number" min="0" step="1000" value="${Number(policy.extraGuestFeePerPerson || 0)}" /><small>áp dụng tới sức chứa tối đa của phòng</small></div>
                    <label class="check-row full"><input data-policy-field="requireIdentityDocuments" type="checkbox" ${policy.requireIdentityDocuments ? 'checked' : ''} ${policy.identityEncryptionConfigured ? '' : 'disabled'} /> Bắt buộc CCCD mặt trước và mặt sau khi khách đặt online</label>
                    <div class="booking-policy-security full ${policy.identityEncryptionConfigured ? '' : 'is-warning'}">
                        <span aria-hidden="true">${policy.identityEncryptionConfigured ? '✓' : '!'}</span>
                        <div><strong>${policy.identityEncryptionConfigured ? 'Kho lưu CCCD đã có khóa mã hóa' : 'Chưa cấu hình khóa mã hóa CCCD'}</strong><small>${policy.identityEncryptionConfigured ? 'Ảnh được mã hóa AES-256-GCM trước khi ghi vào DataRoot và chỉ giải mã qua API quản trị có phân quyền.' : 'Cấu hình Security:IdentityDocumentEncryptionKeyBase64 bằng secret 32-byte trước khi bật yêu cầu CCCD. Hệ thống không lưu ảnh CCCD dạng plaintext.'}</small></div>
                    </div>
                    <div class="field wide"><label>Tên nội quy</label><input data-policy-field="policyTitle" maxlength="200" value="${escapeAttribute(policy.policyTitle || 'Nội quy & Chính sách')}" /></div>
                    <div class="field full"><label>Nội dung Nội quy & Chính sách</label><textarea data-policy-field="policyText" maxlength="20000"></textarea><small>Khách phải mở và đồng ý nội dung này trước khi gửi yêu cầu. Mỗi lần đổi nội dung, hệ thống tăng phiên bản chính sách.</small></div>
                </div>
                <div class="booking-policy-settings-actions"><button class="btn btn-primary" type="button" data-policy-save>Lưu quy tắc đặt phòng</button></div>
            </div>`;
        panel.querySelector('[data-policy-field="policyText"]').value = policy.policyText || '';
        panel.querySelector('[data-policy-save]').addEventListener('click', () => save(panel));
        anchor.parentNode.insertBefore(panel, anchor);
    }

    function escapeAttribute(value) {
        return String(value).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    async function save(panel) {
        if (saving) return;
        const button = panel.querySelector('[data-policy-save]');
        const requireIdentity = panel.querySelector('[data-policy-field="requireIdentityDocuments"]');
        const payload = {
            publicMaxNights: numberValue(panel, 'publicMaxNights', 3),
            includedGuests: numberValue(panel, 'includedGuests', 2),
            extraGuestFeePerPerson: numberValue(panel, 'extraGuestFeePerPerson', 100000),
            requireIdentityDocuments: requireIdentity?.checked === true,
            policyTitle: panel.querySelector('[data-policy-field="policyTitle"]')?.value?.trim() || 'Nội quy & Chính sách',
            policyText: panel.querySelector('[data-policy-field="policyText"]')?.value?.trim() || ''
        };
        saving = true;
        button.disabled = true;
        button.textContent = 'Đang lưu...';
        try {
            policy = await DeLongApi.put(`/api/admin/properties/${propertyId}/booking-policy`, payload);
            button.textContent = 'Đã lưu';
            setTimeout(() => { if (!saving) button.textContent = 'Lưu quy tắc đặt phòng'; }, 1300);
        } catch (error) {
            button.textContent = error.message || 'Không thể lưu';
            setTimeout(() => { if (!saving) button.textContent = 'Lưu quy tắc đặt phòng'; }, 2800);
        } finally {
            saving = false;
            button.disabled = false;
        }
    }

    DeLongApi.get(`/api/admin/properties/${propertyId}/booking-policy`)
        .then(value => { policy = value; render(); })
        .catch(() => { });
    new MutationObserver(render).observe(root, { childList: true, subtree: true });
})();
