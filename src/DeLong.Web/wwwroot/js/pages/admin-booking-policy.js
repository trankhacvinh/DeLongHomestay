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
    let calendarSettings = null;
    let publicCalendarLayout = null;
    let saving = false;

    function numberValue(panel, name, fallback) {
        const value = Number(panel.querySelector(`[data-policy-field="${name}"]`)?.value);
        return Number.isFinite(value) ? value : fallback;
    }

    function render() {
        renderPolicy();
        renderCalendarSettings();
        renderPublicCalendarLayout();
    }

    function renderPublicCalendarLayout() {
        const target = root.querySelector('[data-booking-policy-tab]');
        if (!target || !publicCalendarLayout || target.querySelector('[data-public-calendar-layout]')) return;
        const panel = document.createElement('section');
        panel.className = 'panel booking-policy-settings';
        panel.dataset.publicCalendarLayout = 'true';
        panel.innerHTML = '<div class="panel-head"><h2>Lịch công khai trên điện thoại</h2></div><div class="panel-body"><label class="check-row"><input type="checkbox" data-mobile-all-rooms> Hiện tất cả phòng trên cùng bảng, kéo ngang để xem</label><p class="small muted">Tắt để quay lại giao diện từng phòng như cũ. Cấu hình được lưu theo cơ sở.</p><label class="field">Khung bị lấn: chặn đặt online khi còn tối đa (phút)<input data-minimum-remaining type="number" min="0" max="720" step="1"></label><p class="small muted">Mặc định 120 phút, tính sau thời gian dọn phòng. Nhập 0 để tắt ngưỡng; không áp dụng cho khung còn nguyên.</p><button type="button" class="btn btn-primary">Lưu cấu hình lịch công khai</button><p data-layout-result role="status"></p></div>';
        panel.querySelector('input').checked = publicCalendarLayout.allRoomsOnMobile;
        panel.querySelector('[data-minimum-remaining]').value = publicCalendarLayout.minimumRemainingMinutes ?? 120;
        panel.querySelector('button').addEventListener('click', async event => {
            const button = event.currentTarget;
            button.disabled = true;
            try {
                publicCalendarLayout = await DeLongApi.put(`/api/admin/properties/${propertyId}/booking-policy/calendar-layout`, { allRoomsOnMobile: panel.querySelector('input').checked, minimumRemainingMinutes: Number(panel.querySelector('[data-minimum-remaining]').value) });
                panel.querySelector('[data-layout-result]').textContent = 'Đã lưu. Tải lại lịch công khai để áp dụng.';
            } catch (error) { panel.querySelector('[data-layout-result]').textContent = error.message || 'Không thể lưu'; }
            finally { button.disabled = false; }
        });
        target.appendChild(panel);
    }

    function renderPolicy() {
        if (!policy || root.querySelector('[data-booking-policy-settings]')) return;
        const target = root.querySelector('[data-booking-policy-tab]');
        if (!target) return;
        const panel = document.createElement('section');
        panel.className = 'panel booking-policy-settings';
        panel.dataset.bookingPolicySettings = 'true';
        panel.innerHTML = `
            <div class="panel-head">
                <div><h2>Quy tắc đặt phòng online</h2><p class="small muted">Giới hạn dành cho khách đặt trên website. Nhân viên đặt trong quản trị không bị giới hạn số đêm.</p></div>
                <span class="pill">Giữ phòng khi tạo thanh toán</span>
            </div>
            <div class="panel-body">
                <div class="booking-policy-settings-grid">
                    <div class="field"><label>Đặt tối đa</label><input data-policy-field="publicMaxNights" type="number" min="1" max="14" value="${Number(policy.publicMaxNights || 3)}" /><small>đêm / một lượt online</small></div>
                    <div class="field"><label>Chọn khung liên tiếp tối đa</label><input data-policy-field="publicMaxConsecutiveSlotDays" type="number" min="1" max="14" value="${Number(policy.publicMaxConsecutiveSlotDays || 3)}" /><small>ngày / một lượt online</small></div>
                    <div class="field wide"><label>Giảm giá nhiều khung</label><input data-policy-field="multiSlotDiscountTiers" value="${escapeAttribute(formatTiers(policy.multiSlotDiscountTiers))}" placeholder="Ví dụ: 2:5, 3:10" /><small>Định dạng số khung:phần trăm, cách nhau bằng dấu phẩy. Ví dụ 2:5, 3:10.</small></div>
                    <div class="field"><label>Đã gồm trong giá</label><input data-policy-field="includedGuests" type="number" min="1" max="50" value="${Number(policy.includedGuests || 2)}" /><small>khách trước khi tính phụ thu</small></div>
                    <div class="field"><label>Phụ thu mỗi khách</label><input data-policy-field="extraGuestFeePerPerson" type="number" min="0" step="1000" value="${Number(policy.extraGuestFeePerPerson || 0)}" /><small>áp dụng tới sức chứa tối đa của phòng</small></div>
                    <div class="booking-policy-required-identity full"><span>CCCD khách web</span><strong>Bắt buộc mặt trước + mặt sau</strong><small>Nhân viên tạo booking trong Admin có thể bỏ qua email và CCCD; khách tự đặt trên website phải cung cấp đầy đủ.</small></div>
                    <div class="booking-policy-security full ${policy.identityEncryptionConfigured ? '' : 'is-warning'}">
                        <span aria-hidden="true">${policy.identityEncryptionConfigured ? '✓' : '!'}</span>
                        <div><strong>${policy.identityEncryptionConfigured ? 'Khóa mã hóa CCCD được hệ thống tự quản lý' : 'Không thể khởi tạo khóa mã hóa CCCD'}</strong><small>${policy.identityEncryptionConfigured ? 'Ảnh được mã hóa AES-256-GCM. Khi chuyển server, chỉ cần sao lưu và khôi phục toàn bộ DataRoot; khóa master nằm trong DataRoot/security.' : 'Hãy kiểm tra quyền đọc/ghi DataRoot/security hoặc khôi phục toàn bộ DataRoot từ bản sao lưu. Hệ thống không fallback lưu CCCD dạng plaintext.'}</small></div>
                    </div>
                    <div class="field wide"><label>Tên nội quy</label><input data-policy-field="policyTitle" maxlength="200" value="${escapeAttribute(policy.policyTitle || 'Nội quy & Chính sách')}" /></div>
                    <div class="field full"><label>Nội dung Nội quy & Chính sách</label><textarea data-policy-field="policyText" maxlength="20000"></textarea><small>Khách phải mở và đồng ý nội dung này trước khi gửi yêu cầu. Mỗi lần đổi nội dung, hệ thống tăng phiên bản chính sách.</small></div>
                </div>
                <div class="booking-policy-settings-actions"><button class="btn btn-primary" type="button" data-policy-save>Lưu quy tắc đặt phòng</button></div>
            </div>`;
        panel.querySelector('[data-policy-field="policyText"]').value = policy.policyText || '';
        panel._policyEditor = window.DeLongRichEditor?.enhance(
            panel.querySelector('[data-policy-field="policyText"]'),
            {
                allowImages: true,
                uploadImage: async file => {
                    const form = new FormData();
                    form.append('file', file);
                    const asset = await DeLongApi.postForm(`/api/admin/properties/${propertyId}/site/assets/section`, form);
                    if (!asset?.url) throw new Error('Không nhận được đường dẫn ảnh nội quy.');
                    return asset.url;
                },
                placeholder: 'Nhập nội quy, quy định nhận/trả phòng và chính sách áp dụng cho khách đặt online…',
                helpText: 'Soạn trực quan bằng Quill giống các nội dung khác trong hệ thống; có thể chuyển sang HTML khi cần.'
            });
        panel.querySelector('[data-policy-save]').addEventListener('click', () => save(panel));
        target.appendChild(panel);
    }

    function renderCalendarSettings() {
        if (!calendarSettings || root.querySelector('[data-booking-calendar-settings]')) return;
        const target = root.querySelector('[data-booking-policy-tab]');
        if (!target) return;
        const fields = [
            ['unpaidColor', 'Chưa thanh toán hết', 'Ưu tiên cao nhất khi booking còn công nợ.'],
            ['specialRequestColor', 'Có yêu cầu đặc biệt', 'Booking có nội dung ghi chú.'],
            ['flexibleTimeColor', 'Giờ linh động', 'Giờ tự nhập, không gắn với khung giá.'],
            ['mixedSlotColor', 'Booking nhiều khung', 'Một booking gồm từ hai khung liên tiếp.'],
            ['requestedColor', 'Yêu cầu mới', 'Booking đang ở trạng thái yêu cầu.'],
            ['heldColor', 'Giữ phòng', 'Booking đang giữ chỗ/chờ xử lý.'],
            ['confirmedColor', 'Đã xác nhận', 'Booking đã được xác nhận.'],
            ['checkedInColor', 'Đã nhận phòng', 'Khách đã check-in.']
        ];
        const panel = document.createElement('section');
        panel.className = 'panel booking-policy-settings';
        panel.dataset.bookingCalendarSettings = 'true';
        panel.innerHTML = `
            <div class="panel-head">
                <div><h2>Màu trạng thái trên lịch phòng</h2><p class="small muted">Áp dụng riêng cho cơ sở này. Nếu booking có nhiều dấu hiệu, thứ tự ưu tiên là: còn nợ → ghi chú → giờ linh động → nhiều khung → trạng thái gốc.</p></div>
                <span class="pill">Calendar V2</span>
            </div>
            <div class="panel-body">
                <div class="booking-calendar-color-grid">
                    ${fields.map(([key, label, help]) => `<label class="booking-calendar-color-field"><input type="color" data-calendar-color="${key}" value="${escapeAttribute(calendarSettings[key])}" /><span><strong>${label}</strong><small>${help}</small></span><code data-calendar-color-code="${key}">${escapeAttribute(calendarSettings[key])}</code></label>`).join('')}
                </div>
                <div class="booking-policy-settings-actions"><button class="btn btn-primary" type="button" data-calendar-color-save>Lưu màu lịch phòng</button></div>
            </div>`;
        panel.querySelectorAll('[data-calendar-color]').forEach(input => input.addEventListener('input', () => {
            panel.querySelector(`[data-calendar-color-code="${input.dataset.calendarColor}"]`).textContent = input.value.toUpperCase();
        }));
        panel.querySelector('[data-calendar-color-save]').addEventListener('click', () => saveCalendarSettings(panel));
        target.appendChild(panel);
    }

    function escapeAttribute(value) {
        return String(value).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function formatTiers(tiers) {
        return Array.isArray(tiers) ? tiers.map(x => `${Number(x.minimumSlots)}:${Number(x.discountPercent)}`).join(', ') : '';
    }

    function parseTiers(value) {
        if (!String(value || '').trim()) return [];
        return String(value).split(',').map(part => {
            const [minimumSlots, discountPercent] = part.trim().split(':').map(Number);
            if (!Number.isInteger(minimumSlots) || minimumSlots < 2 || !Number.isFinite(discountPercent) || discountPercent < 0 || discountPercent > 100)
                throw new Error('Mức giảm nhiều khung không đúng định dạng. Ví dụ hợp lệ: 2:5, 3:10.');
            return { minimumSlots, discountPercent };
        });
    }

    async function save(panel) {
        if (saving) return;
        panel._policyEditor?.sync();
        const button = panel.querySelector('[data-policy-save]');
        let tiers;
        try { tiers = parseTiers(panel.querySelector('[data-policy-field="multiSlotDiscountTiers"]')?.value); }
        catch (error) { button.textContent = error.message; setTimeout(() => { button.textContent = 'Lưu quy tắc đặt phòng'; }, 3000); return; }
        const payload = {
            publicMaxNights: numberValue(panel, 'publicMaxNights', 3),
            publicMaxConsecutiveSlotDays: numberValue(panel, 'publicMaxConsecutiveSlotDays', 3),
            multiSlotDiscountTiers: tiers,
            includedGuests: numberValue(panel, 'includedGuests', 2),
            extraGuestFeePerPerson: numberValue(panel, 'extraGuestFeePerPerson', 100000),
            requireIdentityDocuments: true,
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

    async function saveCalendarSettings(panel) {
        const button = panel.querySelector('[data-calendar-color-save]');
        const payload = {};
        panel.querySelectorAll('[data-calendar-color]').forEach(input => {
            payload[input.dataset.calendarColor] = input.value.toUpperCase();
        });
        button.disabled = true;
        button.textContent = 'Đang lưu...';
        try {
            calendarSettings = await DeLongApi.put(`/api/admin/properties/${propertyId}/operations/booking-calendar-settings`, payload);
            button.textContent = 'Đã lưu';
            setTimeout(() => { button.textContent = 'Lưu màu lịch phòng'; }, 1300);
        } catch (error) {
            button.textContent = error.message || 'Không thể lưu';
            setTimeout(() => { button.textContent = 'Lưu màu lịch phòng'; }, 2800);
        } finally {
            button.disabled = false;
        }
    }

    DeLongApi.get(`/api/admin/properties/${propertyId}/booking-policy`)
        .then(value => { policy = value; render(); })
        .catch(() => { });
    DeLongApi.get(`/api/admin/properties/${propertyId}/operations/booking-calendar-settings`)
        .then(value => { calendarSettings = value; render(); })
        .catch(() => { });
    DeLongApi.get(`/api/admin/properties/${propertyId}/booking-policy/calendar-layout`)
        .then(value => { publicCalendarLayout = value; render(); }).catch(() => {});
    new MutationObserver(render).observe(root, { childList: true, subtree: true });
})();
