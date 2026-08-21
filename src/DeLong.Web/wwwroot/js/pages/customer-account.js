(function () {
    const root = document.getElementById('customer-account-page');
    if (!root || !window.Vue) return;
    const { createApp } = window.Vue;
    const parts = location.pathname.split('/').filter(Boolean);
    const siteSlug = parts[0]?.toLowerCase() === 'h' ? parts[1] : '';
    const publicUrl = path => `${path}${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
    const accountUrl = siteSlug ? `/h/${encodeURIComponent(siteSlug)}/customer/account` : '/customer/account';
    const loginUrl = siteSlug ? `/h/${encodeURIComponent(siteSlug)}/customer/login` : '/customer/login';

    createApp({
        data() {
            return {
                loading: true, saving: false, mode: 'login', loginMethod: 'password', termsOpen: false,
                settings: { registrationEnabled: true, authenticatorEnabled: true, loyaltyEnabled: false, benefitText: '', termsTitle: 'Điều khoản tài khoản khách', termsHtml: '', termsVersion: 1 },
                profile: null,
                login: { phone: '', password: '', code: '', rememberMe: true },
                register: { name: '', phone: '', email: '', password: '', termsAccepted: false },
                password: { currentPassword: '', newPassword: '' },
                identity: {
                    front: { previewUrl: '', uploading: false, uploaded: false },
                    back: { previewUrl: '', uploading: false, uploaded: false }
                },
                authenticator: { open: false, sharedKey: '', code: '', recoveryCodes: [] },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        async mounted() {
            try { this.settings = await DeLongApi.get(publicUrl('/api/public/customer-account/settings')); } catch { }
            await this.loadProfile();
            this.loading = false;
        },
        methods: {
            money(value) { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0); },
            dateText(value) { return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)); },
            statusText(value) { return ({ Requested: 'Yêu cầu', Held: 'Giữ phòng', Confirmed: 'Đã xác nhận', CheckedIn: 'Đang ở', Completed: 'Hoàn tất', Cancelled: 'Đã hủy', NoShow: 'Không đến' })[value] || value; },
            async loadProfile() {
                try {
                    const profile = await DeLongApi.get('/api/customer/account/profile');
                    if (!profile || typeof profile !== 'object' || Array.isArray(profile)) {
                        this.profile = null;
                        if (!location.pathname.toLowerCase().endsWith('/customer/login')) location.replace(loginUrl);
                        return;
                    }
                    this.profile = {
                        ...profile,
                        bookings: Array.isArray(profile.bookings) ? profile.bookings : [],
                        loyaltyHistory: Array.isArray(profile.loyaltyHistory) ? profile.loyaltyHistory : []
                    };
                }
                catch (error) {
                    this.profile = null;
                    if (error?.status === 401 || error?.status === 403) {
                        if (!location.pathname.toLowerCase().endsWith('/customer/login')) location.replace(loginUrl);
                    }
                }
            },
            async signIn() {
                if (!this.login.phone) return this.notify('Vui lòng nhập số điện thoại.', 'error');
                this.saving = true;
                try {
                    await DeLongApi.refreshAntiforgery();
                    const endpoint = this.loginMethod === 'password' ? '/api/public/customer-account/login' : publicUrl('/api/public/customer-account/authenticator-login');
                    const payload = this.loginMethod === 'password'
                        ? { phone: this.login.phone, password: this.login.password, rememberMe: this.login.rememberMe }
                        : { phone: this.login.phone, code: this.login.code, rememberMe: this.login.rememberMe };
                    await DeLongApi.post(endpoint, payload);
                    await DeLongApi.refreshAntiforgery();
                    await this.loadProfile();
                    if (this.profile) history.replaceState(null, '', accountUrl);
                    this.notify('Đăng nhập thành công.', 'success');
                } catch (error) { this.notify(error.message || 'Không thể đăng nhập.', 'error'); }
                finally { this.saving = false; }
            },
            async signUp() {
                if (!this.register.name || !this.register.phone || this.register.password.length < 8) return this.notify('Vui lòng nhập đủ tên, số điện thoại và mật khẩu ít nhất 8 ký tự.', 'error');
                if (!this.register.termsAccepted) return this.notify('Bạn cần đồng ý điều khoản tài khoản.', 'error');
                this.saving = true;
                try {
                    await DeLongApi.refreshAntiforgery();
                    await DeLongApi.post(publicUrl('/api/public/customer-account/register'), {
                        phone: this.register.phone, password: this.register.password, name: this.register.name,
                        email: this.register.email || null, termsAccepted: true, termsVersion: this.settings.termsVersion
                    });
                    await DeLongApi.refreshAntiforgery();
                    await this.loadProfile();
                    if (this.profile) history.replaceState(null, '', accountUrl);
                    this.notify('Đã tạo tài khoản.', 'success');
                } catch (error) { this.notify(error.message || 'Không thể tạo tài khoản.', 'error'); }
                finally { this.saving = false; }
            },
            async changePassword() {
                if (this.password.newPassword.length < 8) return this.notify('Mật khẩu mới phải có ít nhất 8 ký tự.', 'error');
                try {
                    await DeLongApi.post('/api/customer/account/change-password', this.password);
                    this.password = { currentPassword: '', newPassword: '' };
                    this.notify('Đã đổi mật khẩu.', 'success');
                } catch (error) { this.notify(error.message || 'Không thể đổi mật khẩu.', 'error'); }
            },
            async uploadIdentity(event, side) {
                const file = event.target.files?.[0];
                if (!file) return;
                const current = this.identity[side];
                if (current.previewUrl) URL.revokeObjectURL(current.previewUrl);
                current.previewUrl = URL.createObjectURL(file);
                current.uploading = true;
                current.uploaded = false;
                this.saving = true;
                try {
                    const form = new FormData();
                    form.append('file', file);
                    const query = siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : '';
                    await DeLongApi.postForm(`/api/customer/account/identity-documents/${side}${query}`, form);
                    await this.loadProfile();
                    current.uploaded = true;
                    this.notify(`Đã lưu ${side === 'front' ? 'mặt trước' : 'mặt sau'} CCCD cho lần đặt sau.`, 'success');
                } catch (error) { this.notify(error.message || 'Không thể tải ảnh CCCD.', 'error'); }
                finally { event.target.value = ''; current.uploading = false; this.saving = false; }
            },
            async openAuthenticator() {
                try {
                    const data = await DeLongApi.get('/api/customer/account/authenticator');
                    this.authenticator = { open: true, sharedKey: data.sharedKey, code: '', recoveryCodes: [] };
                } catch (error) { this.notify(error.message || 'Không thể mở thiết lập Authenticator.', 'error'); }
            },
            async confirmAuthenticator() {
                try {
                    const result = await DeLongApi.post('/api/customer/account/authenticator/confirm', { code: this.authenticator.code });
                    this.authenticator.recoveryCodes = result.recoveryCodes || [];
                    this.profile.authenticatorConfigured = true;
                    this.notify('Đã bật Google Authenticator.', 'success');
                } catch (error) { this.notify(error.message || 'Mã Authenticator không đúng.', 'error'); }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3500);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);
})();
