(function () {
    const root = document.getElementById('settings-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('settings-page-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                activeTab: 'rooms',
                rooms: initial.rooms || [],
                housekeeping: { beforeCheckInMinutes: 0, afterCheckOutMinutes: 0, ...(initial.housekeepingSettings || {}) },
                notification: { guestCheckInEmailEnabled: false, guestCancellationEmailEnabled: true, telegramBookingEnabled: false, telegramBotTokenConfigured: false, telegramChatIds: '', ...(initial.notificationSettings || {}), smtpPassword: '', clearSmtpPassword: false, telegramBotToken: '', clearTelegramBotToken: false },
                emailVariables: [
                    { code: '{{PropertyName}}', meaning: 'Tên cơ sở' },
                    { code: '{{BookingCode}}', meaning: 'Mã booking' },
                    { code: '{{CustomerName}}', meaning: 'Họ tên khách' },
                    { code: '{{CustomerPhone}}', meaning: 'Số điện thoại khách' },
                    { code: '{{CustomerEmail}}', meaning: 'Email khách' },
                    { code: '{{RoomName}}', meaning: 'Tên phòng' },
                    { code: '{{CheckIn}}', meaning: 'Ngày giờ nhận phòng' },
                    { code: '{{CheckOut}}', meaning: 'Ngày giờ trả phòng' },
                    { code: '{{TotalAmount}}', meaning: 'Tổng tiền booking' },
                    { code: '{{GuestGuide}}', meaning: 'Hướng dẫn check-in của phòng' },
                    { code: '{{CancellationReason}}', meaning: 'Lý do hủy booking' },
                    { code: '{{VoucherCode}}', meaning: 'Mã voucher' },
                    { code: '{{DiscountPercent}}', meaning: 'Phần trăm giảm của voucher' },
                    { code: '{{StartsAt}}', meaning: 'Thời gian bắt đầu voucher' },
                    { code: '{{EndsAt}}', meaning: 'Thời gian kết thúc voucher' },
                    { code: '{{AppliesTo}}', meaning: 'Các loại đặt phòng được áp dụng' }
                ],
                emailTemplateEditors: [],
                activeEmailTemplateTab: 'internal',
                customerAccounts: { registrationEnabled: true, authenticatorEnabled: true, loyaltyEnabled: false, loyaltySpendPerPoint: 10000, benefitText: '', termsTitle: '', termsHtml: '', termsVersion: 1, ...(initial.customerAccountSettings || {}) },
                pay2s: { enabled: false, sandbox: true, partnerCode: '', partnerName: 'De Long Homestay', accessKey: '', secretKey: '', accessKeyConfigured: false, secretKeyConfigured: false, bankAccountNumber: '', bankId: 'ACB', apiEndpoint: 'https://payment.pay2s.vn/v1/gateway/api/create', callbackBaseUrl: '', holdMinutes: 15, settlementGraceMinutes: 3, clearCredentials: false, ...(initial.pay2SSettings || {}) },
                savingHousekeeping: false,
                savingNotifications: false,
                savingCustomerAccounts: false,
                savingPay2s: false,
                customerTermsEditor: null,
                testingEmail: false,
                testingTelegram: false,
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
            selectTab(tab) {
                if (!['rooms', 'housekeeping', 'booking', 'customer-accounts', 'pay2s', 'notifications'].includes(tab)) return;
                this.activeTab = tab;
                if (tab === 'customer-accounts') this.$nextTick(() => this.enhanceCustomerTerms());
                if (tab === 'notifications') this.$nextTick(() => this.enhanceEmailTemplates());
            },
            enhanceEmailTemplates() {
                root.querySelectorAll(`[data-email-template-panel="${this.activeEmailTemplateTab}"] [data-email-template-editor]`).forEach(textarea => {
                    const editor = window.DeLongRichEditor?.enhance(textarea, {
                        allowImages: false,
                        placeholder: 'Soạn nội dung email…',
                        helpText: 'Soạn trực quan hoặc chuyển sang HTML. Nội dung được làm sạch khi lưu.'
                    });
                    if (editor && !this.emailTemplateEditors.includes(editor)) this.emailTemplateEditors.push(editor);
                });
            },
            selectEmailTemplateTab(tab) {
                if (!['internal', 'checkin', 'cancellation', 'voucher'].includes(tab)) return;
                this.syncEmailTemplates();
                this.activeEmailTemplateTab = tab;
                this.$nextTick(() => this.enhanceEmailTemplates());
            },
            syncEmailTemplates() {
                this.emailTemplateEditors.forEach(editor => editor?.sync());
            },
            previewEmailSubject(template) {
                return this.replaceEmailPreviewVariables(template || '');
            },
            previewEmailHtml(template) {
                const html = this.replaceEmailPreviewVariables(template || '<p>Chưa có nội dung.</p>');
                return window.DeLongRichEditor?.cleanForVisual(html) || '';
            },
            replaceEmailPreviewVariables(template) {
                const samples = {
                    '{{PropertyName}}': 'De Long Homestay', '{{BookingCode}}': 'BK-260831-ABC123',
                    '{{CustomerName}}': 'Nguyễn Minh Anh', '{{CustomerPhone}}': '0979 745 945',
                    '{{CustomerEmail}}': 'minhanh@example.com', '{{RoomName}}': 'Coco Blue #1',
                    '{{CheckIn}}': '31/08/2026 14:00', '{{CheckOut}}': '01/09/2026 10:00',
                    '{{TotalAmount}}': '750.000', '{{GuestGuide}}': 'Nhận khóa tại quầy lễ tân. Wi-Fi: DeLongGuest.',
                    '{{CancellationReason}}': 'Booking đã được hủy theo yêu cầu.',
                    '{{VoucherCode}}': 'TRI-AN-2026', '{{DiscountPercent}}': '20',
                    '{{StartsAt}}': '01/09/2026 00:00', '{{EndsAt}}': '30/09/2026 23:59',
                    '{{AppliesTo}}': 'khung giờ, qua đêm, cả ngày'
                };
                let output = String(template || '');
                Object.entries(samples).forEach(([code, value]) => { output = output.split(code).join(value); });
                return output;
            },
            async copyEmailVariable(code) {
                try {
                    await navigator.clipboard.writeText(code);
                } catch {
                    const input = document.createElement('textarea');
                    input.value = code; input.style.position = 'fixed'; input.style.opacity = '0';
                    document.body.appendChild(input); input.select(); document.execCommand('copy'); input.remove();
                }
                this.notify(`Đã sao chép ${code}`, 'success');
            },
            async savePay2SSettings() {
                this.savingPay2s = true;
                try {
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/pay2s/settings`, {
                        enabled: this.pay2s.enabled === true,
                        sandbox: this.pay2s.sandbox === true,
                        partnerCode: this.pay2s.partnerCode,
                        partnerName: this.pay2s.partnerName,
                        accessKey: this.pay2s.accessKey || null,
                        secretKey: this.pay2s.secretKey || null,
                        bankAccountNumber: this.pay2s.bankAccountNumber,
                        bankId: this.pay2s.bankId,
                        apiEndpoint: this.pay2s.apiEndpoint,
                        callbackBaseUrl: this.pay2s.callbackBaseUrl,
                        holdMinutes: Number(this.pay2s.holdMinutes || 15),
                        settlementGraceMinutes: Number(this.pay2s.settlementGraceMinutes ?? 3),
                        clearCredentials: this.pay2s.clearCredentials === true
                    });
                    this.pay2s = { ...saved, accessKey: '', secretKey: '', clearCredentials: false };
                    this.notify('Đã lưu hồ sơ Pay2S riêng cho cơ sở.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu cấu hình Pay2S.', 'error');
                } finally { this.savingPay2s = false; }
            },
            enhanceCustomerTerms() {
                const textarea = root.querySelector('[data-customer-account-terms-editor]');
                if (!textarea || textarea.dataset.richEditorReady) return;
                this.customerTermsEditor = window.DeLongRichEditor?.enhance(textarea, {
                    allowImages: false,
                    placeholder: 'Nhập điều khoản đăng ký và sử dụng tài khoản khách…',
                    helpText: 'Nội dung được làm sạch trước khi lưu và mỗi lần thay đổi sẽ tăng phiên bản điều khoản.'
                });
            },
            async saveCustomerAccountSettings() {
                this.customerTermsEditor?.sync();
                const editor = root.querySelector('[data-customer-account-terms-editor]');
                if (editor) this.customerAccounts.termsHtml = editor.value;
                const spend = Number(this.customerAccounts.loyaltySpendPerPoint || 0);
                if (!Number.isInteger(spend) || spend < 1) return this.notify('Số tiền quy đổi điểm phải lớn hơn 0.', 'error');
                this.savingCustomerAccounts = true;
                try {
                    this.customerAccounts = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/customer-account-settings`, {
                        registrationEnabled: this.customerAccounts.registrationEnabled === true,
                        authenticatorEnabled: this.customerAccounts.authenticatorEnabled === true,
                        loyaltyEnabled: this.customerAccounts.loyaltyEnabled === true,
                        loyaltySpendPerPoint: spend,
                        benefitText: this.customerAccounts.benefitText,
                        termsTitle: this.customerAccounts.termsTitle,
                        termsHtml: this.customerAccounts.termsHtml
                    });
                    this.notify('Đã lưu cấu hình tài khoản và tích điểm.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu cấu hình tài khoản khách.', 'error');
                } finally { this.savingCustomerAccounts = false; }
            },
            notificationDate(value) {
                if (!value) return '';
                return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
            },
            async saveHousekeepingSettings() {
                const before = Number(this.housekeeping.beforeCheckInMinutes);
                const after = Number(this.housekeeping.afterCheckOutMinutes);
                if (!Number.isInteger(before) || before < 0 || before > 1440 || !Number.isInteger(after) || after < 0 || after > 1440) {
                    this.notify('Số phút dọn phòng phải là số nguyên từ 0 đến 1440.', 'error');
                    return;
                }

                this.savingHousekeeping = true;
                try {
                    this.housekeeping = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/housekeeping/settings`,
                        { beforeCheckInMinutes: before, afterCheckOutMinutes: after });
                    this.notify('Đã lưu thời điểm dọn phòng.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu thời điểm dọn phòng.', 'error');
                } finally {
                    this.savingHousekeeping = false;
                }
            },
            async saveNotificationSettings() {
                this.syncEmailTemplates();
                this.savingNotifications = true;
                try {
                    const payload = {
                        inAppBookingEnabled: this.notification.inAppBookingEnabled === true,
                        emailBookingEnabled: this.notification.emailBookingEnabled === true,
                        guestCheckInEmailEnabled: this.notification.guestCheckInEmailEnabled === true,
                        guestCancellationEmailEnabled: this.notification.guestCancellationEmailEnabled === true,
                        internalBookingEmailSubjectTemplate: this.notification.internalBookingEmailSubjectTemplate || null,
                        internalBookingEmailBodyTemplate: this.notification.internalBookingEmailBodyTemplate || null,
                        guestCheckInEmailSubjectTemplate: this.notification.guestCheckInEmailSubjectTemplate || null,
                        guestCheckInEmailBodyTemplate: this.notification.guestCheckInEmailBodyTemplate || null,
                        guestCancellationEmailSubjectTemplate: this.notification.guestCancellationEmailSubjectTemplate || null,
                        guestCancellationEmailBodyTemplate: this.notification.guestCancellationEmailBodyTemplate || null,
                        voucherEmailSubjectTemplate: this.notification.voucherEmailSubjectTemplate || null,
                        voucherEmailBodyTemplate: this.notification.voucherEmailBodyTemplate || null,
                        emailRecipients: this.notification.emailRecipients || null,
                        smtpHost: this.notification.smtpHost || null,
                        smtpPort: Number(this.notification.smtpPort || 587),
                        smtpUseSsl: this.notification.smtpUseSsl === true,
                        smtpUsername: this.notification.smtpUsername || null,
                        smtpPassword: this.notification.smtpPassword || null,
                        clearSmtpPassword: this.notification.clearSmtpPassword === true,
                        smtpFromEmail: this.notification.smtpFromEmail || null,
                        smtpFromName: this.notification.smtpFromName || null,
                        telegramBookingEnabled: this.notification.telegramBookingEnabled === true,
                        telegramBotToken: this.notification.telegramBotToken || null,
                        clearTelegramBotToken: this.notification.clearTelegramBotToken === true,
                        telegramChatIds: this.notification.telegramChatIds || null
                    };
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/notifications/settings`, payload);
                    this.notification = { ...saved, smtpPassword: '', clearSmtpPassword: false, telegramBotToken: '', clearTelegramBotToken: false };
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
            async sendTestTelegram() {
                if (this.notification.telegramBotToken) {
                    this.notify('Hãy lưu cấu hình trước khi gửi Telegram thử.', 'error');
                    return;
                }
                this.testingTelegram = true;
                try {
                    await DeLongApi.post(`/api/admin/properties/${this.propertyId}/notifications/settings/test-telegram`, {});
                    this.notification.lastTelegramError = null;
                    this.notification.lastTelegramErrorAtUtc = null;
                    this.notification.lastTelegramSentAtUtc = new Date().toISOString();
                    this.notify('Đã gửi Telegram thử.', 'success');
                } catch (error) {
                    this.notification.lastTelegramError = error.message || 'Không gửi được Telegram thử.';
                    this.notification.lastTelegramErrorAtUtc = new Date().toISOString();
                    this.notify(this.notification.lastTelegramError, 'error');
                } finally {
                    this.testingTelegram = false;
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
