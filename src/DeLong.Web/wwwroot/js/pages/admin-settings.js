(function () {
    const root = document.getElementById('settings-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('settings-page-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                rooms: initial.rooms || [],
                notification: { ...(initial.notificationSettings || {}), smtpPassword: '', clearSmtpPassword: false },
                savingNotifications: false,
                testingEmail: false,
                saving: false,
                editor: { open: false, mode: 'create', rateId: null },
                archiveEditor: { open: false, room: null, rate: null },
                form: { roomId: '', type: 0, name: '', startTime: '14:00', endTime: '17:00', price: 0, sortOrder: 0, isActive: true },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            activeRooms() {
                return this.rooms.filter(x => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
            }
        },
        methods: {
            notificationDate(value) {
                if (!value) return '';
                return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
            },
            async saveNotificationSettings() {
                this.savingNotifications = true;
                try {
                    const payload = {
                        inAppBookingEnabled: this.notification.inAppBookingEnabled === true,
                        emailBookingEnabled: this.notification.emailBookingEnabled === true,
                        emailRecipients: this.notification.emailRecipients || null,
                        smtpHost: this.notification.smtpHost || null,
                        smtpPort: Number(this.notification.smtpPort || 587),
                        smtpUseSsl: this.notification.smtpUseSsl === true,
                        smtpUsername: this.notification.smtpUsername || null,
                        smtpPassword: this.notification.smtpPassword || null,
                        clearSmtpPassword: this.notification.clearSmtpPassword === true,
                        smtpFromEmail: this.notification.smtpFromEmail || null,
                        smtpFromName: this.notification.smtpFromName || null
                    };
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/notifications/settings`, payload);
                    this.notification = { ...saved, smtpPassword: '', clearSmtpPassword: false };
                    this.notify('Đã lưu cấu hình thông báo.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu cấu hình thông báo.', 'error');
                } finally {
                    this.savingNotifications = false;
                }
            },
            async sendTestEmail() {
                if (this.notification.smtpPassword) {
                    this.notify('Hãy lưu cấu hình trước khi gửi email thử.', 'error');
                    return;
                }
                this.testingEmail = true;
                try {
                    await DeLongApi.post(`/api/admin/properties/${this.propertyId}/notifications/settings/test-email`, {});
                    this.notification.lastEmailError = null;
                    this.notification.lastEmailErrorAtUtc = null;
                    this.notification.lastEmailSentAtUtc = new Date().toISOString();
                    this.notify('Đã gửi email thử.', 'success');
                } catch (error) {
                    this.notification.lastEmailError = error.message || 'Không gửi được email thử.';
                    this.notification.lastEmailErrorAtUtc = new Date().toISOString();
                    this.notify(this.notification.lastEmailError, 'error');
                } finally {
                    this.testingEmail = false;
                }
            },
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            sortedRates(room) {
                return [...(room.rates || [])].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
            },
            openCreate(room) {
                this.form = {
                    roomId: room?.id || this.activeRooms[0]?.id || '',
                    type: 0,
                    name: '',
                    startTime: '14:00',
                    endTime: '17:00',
                    price: 0,
                    sortOrder: 0,
                    isActive: true
                };
                this.editor = { open: true, mode: 'create', rateId: null };
            },
            openEdit(room, rate) {
                this.form = {
                    roomId: room.id,
                    type: Number(rate.type ?? (rate.isOvernight ? 1 : 0)),
                    name: rate.name,
                    startTime: rate.startTime,
                    endTime: rate.endTime,
                    price: Number(rate.price || 0),
                    sortOrder: Number(rate.sortOrder || 0),
                    isActive: rate.isActive
                };
                this.editor = { open: true, mode: 'edit', rateId: rate.id };
            },
            onTypeChanged() {
                if (this.form.type === 2 && !this.form.name.trim()) this.form.name = 'Lưu trú theo đêm';
                if (this.form.type === 1 && !this.form.name.trim()) this.form.name = 'Qua đêm';
            },
            closeEditor() { if (!this.saving) this.editor.open = false; },
            validate() {
                if (!this.form.roomId) return 'Vui lòng chọn phòng.';
                if (![0, 1, 2].includes(Number(this.form.type))) return 'Vui lòng chọn loại giá.';
                if (!this.form.name.trim()) return 'Vui lòng nhập tên mức giá.';
                if (!this.form.startTime || !this.form.endTime) return 'Vui lòng nhập giờ nhận/bắt đầu và giờ trả/kết thúc.';
                if (Number(this.form.price || 0) < 0) return 'Giá không được âm.';
                if (Number(this.form.type) === 2 && Number(this.form.price || 0) <= 0) return 'Giá lưu trú theo đêm phải lớn hơn 0.';
                return null;
            },
            async saveRate() {
                const validation = this.validate();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    const base = `/api/admin/properties/${this.propertyId}/rooms/${this.form.roomId}/rates`;
                    const payload = {
                        name: this.form.name,
                        startTime: this.form.startTime,
                        endTime: this.form.endTime,
                        type: Number(this.form.type),
                        price: Number(this.form.price || 0),
                        sortOrder: Number(this.form.sortOrder || 0)
                    };
                    let rate;
                    if (this.editor.mode === 'create') {
                        rate = await DeLongApi.post(base, payload);
                        const room = this.rooms.find(x => x.id === this.form.roomId);
                        if (room) room.rates.push(rate);
                    } else {
                        rate = await DeLongApi.put(`${base}/${this.editor.rateId}`, { ...payload, isActive: this.form.isActive });
                        const room = this.rooms.find(x => x.id === this.form.roomId);
                        const index = room?.rates.findIndex(x => x.id === rate.id) ?? -1;
                        if (room && index >= 0) room.rates.splice(index, 1, rate);
                    }
                    this.editor.open = false;
                    this.notify('Đã lưu mức giá.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu mức giá.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            openArchive(room, rate) {
                this.archiveEditor = { open: true, room, rate };
            },
            closeArchive() { if (!this.saving) this.archiveEditor.open = false; },
            async confirmArchive() {
                const room = this.archiveEditor.room;
                const rate = this.archiveEditor.rate;
                if (!room || !rate) return;
                this.saving = true;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/rooms/${room.id}/rates/${rate.id}`);
                    const index = room.rates.findIndex(x => x.id === rate.id);
                    if (index >= 0) room.rates[index] = { ...rate, isActive: false };
                    this.archiveEditor.open = false;
                    this.notify('Đã ngừng mức giá.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể ngừng mức giá.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3000);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);
})();

(function () {
    const root = document.getElementById('settings-page');
    const dataNode = document.getElementById('settings-page-data');
    if (!root || !dataNode || !window.DeLongApi) return;

    let initial;
    try { initial = JSON.parse(dataNode.textContent || '{}'); } catch { return; }
    const propertyId = initial.propertyId;
    const rooms = (initial.rooms || []).filter(room => room.isActive)
        .sort((a, b) => Number(a.sortOrder || 0) - Number(b.sortOrder || 0) || String(a.name || '').localeCompare(String(b.name || ''), 'vi'));
    if (!propertyId || rooms.length === 0) return;

    const style = document.createElement('style');
    style.textContent = `
        .quick-rate-open{margin-left:auto}
        .quick-rate-backdrop{position:fixed;inset:0;z-index:360;background:rgba(8,35,36,.46);backdrop-filter:blur(3px);display:grid;place-items:center;padding:22px}
        .quick-rate-backdrop[hidden]{display:none!important}
        .quick-rate-card{width:min(1080px,96vw);max-height:min(860px,94vh);overflow:hidden;display:grid;grid-template-rows:auto minmax(0,1fr) auto;border-radius:22px;background:#fff;box-shadow:0 28px 90px rgba(8,35,36,.25)}
        .quick-rate-head{display:flex;align-items:flex-start;justify-content:space-between;gap:18px;padding:22px 24px 17px;border-bottom:1px solid #e1ebe8}
        .quick-rate-head h2{margin:3px 0 4px;color:#173e3b;font-size:23px}.quick-rate-head p{margin:0;color:#758681;font-size:12px}.quick-rate-kicker{color:#16766f;font-size:9px;font-weight:900;letter-spacing:.14em}
        .quick-rate-close{width:38px;height:38px;border:1px solid #d5e2df;border-radius:11px;background:#f7faf9;color:#49645e;font-size:22px;cursor:pointer}
        .quick-rate-body{overflow:auto;padding:18px 24px 22px;display:grid;gap:16px}.quick-rate-section{display:grid;gap:10px}.quick-rate-section-head{display:flex;align-items:center;justify-content:space-between;gap:12px}.quick-rate-section-head h3{margin:0;color:#294a45;font-size:14px}.quick-rate-section-head small{color:#7b8c87}
        .quick-room-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px}.quick-room-choice{display:flex;align-items:center;gap:9px;padding:10px 12px;border:1px solid #d9e5e2;border-radius:11px;background:#fbfdfc;cursor:pointer;min-width:0}.quick-room-choice:has(input:checked){border-color:#71a9a3;background:#edf7f4}.quick-room-choice span{min-width:0;display:grid}.quick-room-choice strong{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;color:#31504b;font-size:11px}.quick-room-choice small{color:#82918e;font-size:9px}
        .quick-rate-tools{display:flex;align-items:center;gap:7px;flex-wrap:wrap}.quick-rate-tools select{height:36px;min-width:190px;width:auto;padding:0 32px 0 10px;border-radius:9px}.quick-rate-link{border:0;background:transparent;color:#14736d;font-weight:800;font-size:10px;cursor:pointer;padding:5px 3px}.quick-rate-link:hover{text-decoration:underline}
        .quick-rate-table{display:grid;gap:7px}.quick-rate-row{display:grid;grid-template-columns:130px minmax(150px,1fr) 112px 112px 150px 38px;gap:7px;align-items:end;padding:9px;border:1px solid #dce7e4;border-radius:12px;background:#fff}.quick-rate-field{display:grid;gap:4px;min-width:0}.quick-rate-field span{font-size:8px;font-weight:850;letter-spacing:.05em;text-transform:uppercase;color:#5e7771}.quick-rate-field input,.quick-rate-field select{width:100%;height:38px;min-height:38px;padding:7px 9px;border:1px solid #ccddd8;border-radius:9px;background:#fff;color:#294945;font-size:11px}.quick-rate-remove{width:38px;height:38px;border:1px solid #efd1ce;border-radius:9px;background:#fff8f7;color:#b14640;cursor:pointer;font-size:16px}
        .quick-rate-summary{padding:10px 12px;border-radius:10px;background:#f2f8f6;color:#57706a;font-size:10px;line-height:1.45}.quick-rate-summary strong{color:#23534d}
        .quick-rate-progress{min-height:18px;color:#607772;font-size:10px}.quick-rate-error{color:#a53d37}
        .quick-rate-foot{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:13px 24px;border-top:1px solid #e1ebe8;background:#fbfdfc}.quick-rate-foot-copy{color:#71847e;font-size:10px}.quick-rate-foot-actions{display:flex;gap:8px}
        .quick-rate-flash{position:fixed;right:22px;bottom:22px;z-index:500;max-width:min(420px,calc(100vw - 44px));padding:12px 15px;border-radius:12px;background:#173f3b;color:#fff;box-shadow:0 14px 38px rgba(12,48,45,.25);font-size:11px;font-weight:750}
        @media(max-width:880px){.quick-room-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.quick-rate-row{grid-template-columns:1fr 1fr 1fr}.quick-rate-row .quick-rate-name{grid-column:span 2}.quick-rate-remove{justify-self:end}}
        @media(max-width:620px){.quick-rate-open{margin-left:0}.quick-rate-backdrop{padding:10px}.quick-rate-card{width:100%;max-height:96vh;border-radius:16px}.quick-rate-head,.quick-rate-body,.quick-rate-foot{padding-left:15px;padding-right:15px}.quick-room-grid{grid-template-columns:1fr}.quick-rate-row{grid-template-columns:1fr 1fr}.quick-rate-row .quick-rate-name{grid-column:1/-1}.quick-rate-foot{align-items:flex-start;flex-direction:column}.quick-rate-foot-actions{width:100%}.quick-rate-foot-actions .btn{flex:1}}
    `;
    document.head.appendChild(style);

    const titleRow = root.querySelector('.page-title-row');
    const addRateButton = titleRow?.querySelector('button.btn-primary');
    if (!titleRow || !addRateButton) return;

    const openButton = document.createElement('button');
    openButton.type = 'button';
    openButton.className = 'btn btn-light quick-rate-open';
    openButton.innerHTML = '<svg><use href="#i-plus"></use></svg>Tạo nhanh bộ khung giờ';
    addRateButton.before(openButton);

    const modal = document.createElement('div');
    modal.className = 'quick-rate-backdrop';
    modal.hidden = true;
    modal.innerHTML = `
        <section class="quick-rate-card" role="dialog" aria-modal="true" aria-labelledby="quick-rate-title">
            <header class="quick-rate-head">
                <div><span class="quick-rate-kicker">THIẾT LẬP NHANH</span><h2 id="quick-rate-title">Tạo bộ khung giờ</h2><p>Khai báo một lần rồi áp dụng cho nhiều phòng. Khung đã tồn tại ở phòng sẽ được bỏ qua, không tạo trùng.</p></div>
                <button type="button" class="quick-rate-close" data-quick-close aria-label="Đóng">×</button>
            </header>
            <div class="quick-rate-body">
                <section class="quick-rate-section">
                    <div class="quick-rate-section-head"><h3>1. Chọn phòng áp dụng</h3><div class="quick-rate-tools"><button type="button" class="quick-rate-link" data-quick-all>Chọn tất cả</button><button type="button" class="quick-rate-link" data-quick-none>Bỏ chọn</button></div></div>
                    <div class="quick-room-grid" data-quick-rooms></div>
                </section>
                <section class="quick-rate-section">
                    <div class="quick-rate-section-head"><h3>2. Khai báo bộ khung giờ</h3><div class="quick-rate-tools"><select data-quick-source><option value="">Lấy mẫu từ phòng...</option></select><button type="button" class="quick-rate-link" data-quick-load>Nạp mẫu</button><button type="button" class="quick-rate-link" data-quick-add>+ Thêm khung</button></div></div>
                    <div class="quick-rate-table" data-quick-rows></div>
                    <div class="quick-rate-summary" data-quick-summary></div>
                    <div class="quick-rate-progress" data-quick-progress></div>
                </section>
            </div>
            <footer class="quick-rate-foot"><div class="quick-rate-foot-copy">Giá có thể sửa riêng cho từng phòng sau khi tạo.</div><div class="quick-rate-foot-actions"><button type="button" class="btn btn-light" data-quick-close>Hủy</button><button type="button" class="btn btn-primary" data-quick-save>Tạo bộ khung giờ</button></div></footer>
        </section>`;
    document.body.appendChild(modal);

    const roomBox = modal.querySelector('[data-quick-rooms]');
    const rowsBox = modal.querySelector('[data-quick-rows]');
    const sourceSelect = modal.querySelector('[data-quick-source]');
    const summary = modal.querySelector('[data-quick-summary]');
    const progress = modal.querySelector('[data-quick-progress]');
    const saveButton = modal.querySelector('[data-quick-save]');
    let saving = false;
    let rowSeed = 0;
    let rows = [];

    const html = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const typeOf = rate => Number(rate.type ?? (rate.isOvernight ? 1 : 0));
    const activeTemplateRates = room => (room.rates || []).filter(rate => rate.isActive !== false && [0, 1].includes(typeOf(rate)))
        .sort((a, b) => Number(a.sortOrder || 0) - Number(b.sortOrder || 0) || String(a.startTime || '').localeCompare(String(b.startTime || '')));

    function createRow(seed) {
        rowSeed += 1;
        return {
            key: rowSeed,
            type: Number(seed?.type ?? 0),
            name: String(seed?.name || `Khung ${rowSeed}`),
            startTime: String(seed?.startTime || '11:00').slice(0, 5),
            endTime: String(seed?.endTime || '14:00').slice(0, 5),
            price: Number(seed?.price || 0)
        };
    }

    function selectedRooms() {
        const ids = new Set([...roomBox.querySelectorAll('input[type=checkbox]:checked')].map(input => input.value));
        return rooms.filter(room => ids.has(String(room.id)));
    }

    function renderRooms() {
        roomBox.innerHTML = rooms.map(room => `<label class="quick-room-choice"><input type="checkbox" value="${html(room.id)}" checked><span><strong>${html(room.name)}</strong><small>${html(room.code || '')} · ${activeTemplateRates(room).length} khung hiện có</small></span></label>`).join('');
        const sources = rooms.filter(room => activeTemplateRates(room).length > 0);
        sourceSelect.innerHTML = '<option value="">Lấy mẫu từ phòng...</option>' + sources.map(room => `<option value="${html(room.id)}">${html(room.name)} · ${activeTemplateRates(room).length} khung</option>`).join('');
    }

    function renderRows() {
        rowsBox.innerHTML = rows.map((row, index) => `
            <div class="quick-rate-row" data-row-key="${row.key}">
                <label class="quick-rate-field"><span>Loại</span><select data-field="type"><option value="0"${row.type === 0 ? ' selected' : ''}>Khung giờ</option><option value="1"${row.type === 1 ? ' selected' : ''}>Qua đêm</option></select></label>
                <label class="quick-rate-field quick-rate-name"><span>Tên</span><input data-field="name" maxlength="100" value="${html(row.name)}" placeholder="Ví dụ: Khung chiều" /></label>
                <label class="quick-rate-field"><span>Bắt đầu</span><input data-field="startTime" type="time" value="${html(row.startTime)}" /></label>
                <label class="quick-rate-field"><span>Kết thúc</span><input data-field="endTime" type="time" value="${html(row.endTime)}" /></label>
                <label class="quick-rate-field"><span>Giá</span><input data-field="price" type="number" min="0" step="1000" inputmode="numeric" value="${Number(row.price || 0)}" /></label>
                <button type="button" class="quick-rate-remove" data-remove="${row.key}" aria-label="Xóa khung ${index + 1}" title="Xóa khung">×</button>
            </div>`).join('');
        syncSummary();
    }

    function syncRowsFromInputs() {
        rowsBox.querySelectorAll('[data-row-key]').forEach(node => {
            const row = rows.find(item => item.key === Number(node.dataset.rowKey));
            if (!row) return;
            row.type = Number(node.querySelector('[data-field=type]')?.value || 0);
            row.name = node.querySelector('[data-field=name]')?.value.trim() || '';
            row.startTime = node.querySelector('[data-field=startTime]')?.value || '';
            row.endTime = node.querySelector('[data-field=endTime]')?.value || '';
            row.price = Number(node.querySelector('[data-field=price]')?.value || 0);
        });
    }

    function syncSummary() {
        const roomCount = selectedRooms().length;
        const count = roomCount * rows.length;
        summary.innerHTML = `<strong>${roomCount} phòng · ${rows.length} khung</strong> · tối đa ${count} mức giá sẽ được tạo. Khung trùng giờ/loại đang hoạt động sẽ tự bỏ qua.`;
    }

    function seedRows() {
        const best = [...rooms].sort((a, b) => activeTemplateRates(b).length - activeTemplateRates(a).length)[0];
        const template = best ? activeTemplateRates(best) : [];
        if (template.length > 0) rows = template.map(rate => createRow(rate));
        else rows = [createRow({ name: 'Khung 1', startTime: '11:00', endTime: '14:00', price: 0 })];
        renderRows();
    }

    function open() {
        renderRooms();
        seedRows();
        progress.textContent = '';
        progress.classList.remove('quick-rate-error');
        modal.hidden = false;
        document.body.style.overflow = 'hidden';
    }

    function close() {
        if (saving) return;
        modal.hidden = true;
        document.body.style.overflow = '';
    }

    function validate() {
        syncRowsFromInputs();
        if (selectedRooms().length === 0) return 'Hãy chọn ít nhất một phòng.';
        if (rows.length === 0) return 'Hãy thêm ít nhất một khung giờ.';
        for (let i = 0; i < rows.length; i += 1) {
            const row = rows[i];
            if (![0, 1].includes(Number(row.type))) return `Khung ${i + 1} có loại không hợp lệ.`;
            if (!row.name) return `Khung ${i + 1} chưa có tên.`;
            if (!row.startTime || !row.endTime) return `Khung ${i + 1} chưa đủ giờ bắt đầu/kết thúc.`;
            if (Number(row.price) < 0) return `Giá của khung ${i + 1} không được âm.`;
        }
        return null;
    }

    async function save() {
        const error = validate();
        if (error) {
            progress.textContent = error;
            progress.classList.add('quick-rate-error');
            return;
        }
        const targets = selectedRooms();
        const total = targets.length * rows.length;
        if (!window.confirm(`Tạo bộ ${rows.length} khung giờ cho ${targets.length} phòng?\nTối đa ${total} mức giá mới. Khung trùng sẽ được bỏ qua.`)) return;

        saving = true;
        saveButton.disabled = true;
        progress.classList.remove('quick-rate-error');
        let created = 0;
        let skipped = 0;
        let failed = 0;
        let done = 0;

        for (const room of targets) {
            const existing = activeTemplateRates(room);
            for (let index = 0; index < rows.length; index += 1) {
                const row = rows[index];
                done += 1;
                progress.textContent = `Đang tạo ${done}/${total} · ${room.name} · ${row.name}`;
                const duplicate = existing.some(rate => typeOf(rate) === Number(row.type)
                    && String(rate.startTime || '').slice(0, 5) === row.startTime
                    && String(rate.endTime || '').slice(0, 5) === row.endTime);
                if (duplicate) {
                    skipped += 1;
                    continue;
                }
                try {
                    const payload = {
                        name: row.name,
                        startTime: row.startTime,
                        endTime: row.endTime,
                        type: Number(row.type),
                        price: Number(row.price || 0),
                        sortOrder: index
                    };
                    await DeLongApi.post(`/api/admin/properties/${propertyId}/rooms/${room.id}/rates`, payload);
                    created += 1;
                } catch {
                    failed += 1;
                }
            }
        }

        const message = `Đã tạo ${created} mức giá${skipped ? ` · bỏ qua ${skipped} khung trùng` : ''}${failed ? ` · lỗi ${failed}` : ''}.`;
        sessionStorage.setItem('quick-rate-flash', message);
        window.location.reload();
    }

    openButton.addEventListener('click', open);
    modal.querySelectorAll('[data-quick-close]').forEach(button => button.addEventListener('click', close));
    modal.addEventListener('click', event => { if (event.target === modal) close(); });
    document.addEventListener('keydown', event => { if (!modal.hidden && event.key === 'Escape') close(); });
    roomBox.addEventListener('change', syncSummary);
    rowsBox.addEventListener('input', () => { syncRowsFromInputs(); syncSummary(); });
    rowsBox.addEventListener('change', () => { syncRowsFromInputs(); syncSummary(); });
    rowsBox.addEventListener('click', event => {
        const remove = event.target.closest('[data-remove]');
        if (!remove) return;
        syncRowsFromInputs();
        rows = rows.filter(row => row.key !== Number(remove.dataset.remove));
        renderRows();
    });
    modal.querySelector('[data-quick-all]').addEventListener('click', () => { roomBox.querySelectorAll('input[type=checkbox]').forEach(input => { input.checked = true; }); syncSummary(); });
    modal.querySelector('[data-quick-none]').addEventListener('click', () => { roomBox.querySelectorAll('input[type=checkbox]').forEach(input => { input.checked = false; }); syncSummary(); });
    modal.querySelector('[data-quick-add]').addEventListener('click', () => {
        syncRowsFromInputs();
        const previous = rows.at(-1);
        rows.push(createRow({ name: `Khung ${rows.length + 1}`, startTime: previous?.endTime || '14:00', endTime: previous?.endTime || '17:00', price: previous?.price || 0 }));
        renderRows();
        rowsBox.lastElementChild?.querySelector('[data-field=name]')?.focus();
    });
    modal.querySelector('[data-quick-load]').addEventListener('click', () => {
        const source = rooms.find(room => String(room.id) === String(sourceSelect.value));
        if (!source) {
            progress.textContent = 'Chọn một phòng đã có khung giờ để nạp mẫu.';
            progress.classList.add('quick-rate-error');
            return;
        }
        const template = activeTemplateRates(source);
        rows = template.map(rate => createRow(rate));
        progress.textContent = `Đã nạp ${rows.length} khung từ ${source.name}. Bạn có thể sửa giờ và giá trước khi tạo.`;
        progress.classList.remove('quick-rate-error');
        renderRows();
    });
    saveButton.addEventListener('click', save);

    const flash = sessionStorage.getItem('quick-rate-flash');
    if (flash) {
        sessionStorage.removeItem('quick-rate-flash');
        const toast = document.createElement('div');
        toast.className = 'quick-rate-flash';
        toast.textContent = flash;
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), 4500);
    }
})();