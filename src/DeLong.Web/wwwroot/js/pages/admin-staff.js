(function () {
    const root = document.getElementById('staff-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('staff-page-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                currentUserId: initial.currentUserId,
                accounts: initial.accounts || [],
                roles: initial.roles || [],
                availableProperties: initial.availableProperties || [],
                search: '',
                roleFilter: '',
                statusFilter: 'active',
                saving: false,
                showPassword: false,
                editor: { open: false, mode: 'create', account: null },
                form: this.emptyForm ? this.emptyForm() : {
                    displayName: '', email: '', role: 'Staff', propertyIds: [], temporaryPassword: '', isActive: true
                },
                resetModal: { open: false, password: '', showPassword: false },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            filteredAccounts() {
                const q = this.search.trim().toLowerCase();
                return this.accounts.filter(account => {
                    if (this.roleFilter && account.role !== this.roleFilter) return false;
                    if (this.statusFilter === 'active' && !account.isActive) return false;
                    if (this.statusFilter === 'inactive' && account.isActive) return false;
                    if (this.statusFilter === 'locked' && !account.isLockedOut) return false;
                    if (this.statusFilter === 'temporary' && !account.mustChangePassword) return false;
                    if (!q) return true;
                    return (account.displayName || '').toLowerCase().includes(q) ||
                        (account.email || '').toLowerCase().includes(q) ||
                        this.roleLabel(account.role).toLowerCase().includes(q);
                });
            },
            activeCount() { return this.accounts.filter(x => x.isActive).length; },
            leadershipCount() { return this.accounts.filter(x => x.isActive && ['Admin', 'Manager'].includes(x.role)).length; },
            mustChangeCount() { return this.accounts.filter(x => x.isActive && x.mustChangePassword).length; },
            lockedCount() { return this.accounts.filter(x => x.isActive && x.isLockedOut).length; }
        },
        methods: {
            emptyForm() {
                return {
                    displayName: '',
                    email: '',
                    role: 'Staff',
                    propertyIds: this.availableProperties?.length === 1 ? [this.availableProperties[0].id] : [],
                    temporaryPassword: '',
                    isActive: true
                };
            },
            initials(name) {
                const parts = (name || '?').trim().split(/\s+/).filter(Boolean);
                if (!parts.length) return '?';
                if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
                return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
            },
            roleOption(value) { return this.roles.find(role => role.value === value); },
            roleLabel(value) { return this.roleOption(value)?.label || value || 'Chưa gán'; },
            roleClass(value) { return `role-${this.roleOption(value)?.tone || 'neutral'}`; },
            dateTimeText(value) {
                if (!value) return '—';
                return new Intl.DateTimeFormat('vi-VN', {
                    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
                }).format(new Date(value));
            },
            openCreate() {
                this.form = this.emptyForm();
                this.showPassword = false;
                this.editor = { open: true, mode: 'create', account: null };
            },
            openEdit(account) {
                if (!account.canManage) return this.notify('Tài khoản này có quyền ở cơ sở ngoài phạm vi bạn quản lý.', 'error');
                this.form = {
                    displayName: account.displayName,
                    email: account.email,
                    role: account.role,
                    propertyIds: (account.properties || []).map(x => x.id),
                    temporaryPassword: '',
                    isActive: account.isActive
                };
                this.showPassword = false;
                this.editor = { open: true, mode: 'edit', account: { ...account } };
            },
            closeEditor() {
                if (this.saving || this.resetModal.open) return;
                this.editor = { open: false, mode: 'create', account: null };
            },
            validate() {
                if (!this.form.displayName.trim()) return 'Vui lòng nhập tên hiển thị.';
                if (!/^\S+@\S+\.\S+$/.test(this.form.email.trim())) return 'Email không hợp lệ.';
                if (!this.form.role) return 'Vui lòng chọn vai trò.';
                if (!this.form.propertyIds.length) return 'Chọn ít nhất một cơ sở được truy cập.';
                if (this.editor.mode === 'create' && this.form.temporaryPassword.length < 8) return 'Mật khẩu tạm phải có ít nhất 8 ký tự.';
                return null;
            },
            generatePasswordValue() {
                const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
                const bytes = new Uint32Array(11);
                window.crypto.getRandomValues(bytes);
                return `DL-${Array.from(bytes, value => alphabet[value % alphabet.length]).join('')}`;
            },
            generateTemporaryPassword() {
                this.form.temporaryPassword = this.generatePasswordValue();
                this.showPassword = true;
            },
            async refreshData(focusUserId) {
                const data = await DeLongApi.get('/api/admin/staff');
                this.currentUserId = data.currentUserId;
                this.accounts = data.accounts || [];
                this.roles = data.roles || [];
                this.availableProperties = data.availableProperties || [];
                if (focusUserId && this.editor.open) {
                    const refreshed = this.accounts.find(x => x.id === focusUserId);
                    if (refreshed) this.editor.account = { ...refreshed };
                }
            },
            async saveAccount() {
                const validation = this.validate();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    if (this.editor.mode === 'create') {
                        const created = await DeLongApi.post('/api/admin/staff', {
                            displayName: this.form.displayName,
                            email: this.form.email,
                            role: this.form.role,
                            propertyIds: this.form.propertyIds,
                            temporaryPassword: this.form.temporaryPassword
                        });
                        await this.refreshData();
                        this.editor.open = false;
                        this.notify(`Đã tạo tài khoản cho ${created.displayName}.`, 'success');
                    } else {
                        const accountId = this.editor.account.id;
                        const wasActive = this.editor.account.isActive;
                        await DeLongApi.put(`/api/admin/staff/${accountId}`, {
                            displayName: this.form.displayName,
                            email: this.form.email,
                            role: this.form.role,
                            propertyIds: this.form.propertyIds,
                            isActive: this.form.isActive
                        });
                        await this.refreshData(accountId);
                        this.editor.open = false;
                        this.notify(wasActive && !this.form.isActive ? 'Đã ngừng tài khoản nhân viên.' : 'Đã lưu thay đổi tài khoản.', 'success');
                    }
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu tài khoản.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            openResetPassword() {
                this.resetModal = { open: true, password: '', showPassword: false };
            },
            closeResetPassword() {
                if (!this.saving) this.resetModal.open = false;
            },
            generateResetPassword() {
                this.resetModal.password = this.generatePasswordValue();
                this.resetModal.showPassword = true;
            },
            async resetPassword() {
                if (!this.editor.account) return;
                if (this.resetModal.password.length < 8) return this.notify('Mật khẩu tạm phải có ít nhất 8 ký tự.', 'error');
                this.saving = true;
                const accountId = this.editor.account.id;
                try {
                    await DeLongApi.post(`/api/admin/staff/${accountId}/reset-password`, { temporaryPassword: this.resetModal.password });
                    await this.refreshData(accountId);
                    this.resetModal.open = false;
                    this.notify('Đã đặt lại mật khẩu. Nhân viên sẽ phải đổi mật khẩu khi đăng nhập.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể đặt lại mật khẩu.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            async unlockAccount() {
                if (!this.editor.account) return;
                this.saving = true;
                const accountId = this.editor.account.id;
                try {
                    await DeLongApi.post(`/api/admin/staff/${accountId}/unlock`, {});
                    await this.refreshData(accountId);
                    this.notify('Đã mở khóa đăng nhập.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể mở khóa tài khoản.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3800);
                this.toast = { show: true, message, type, timer };
            }
        },
        mounted() {
            this.form = this.emptyForm();
        }
    }).mount(root);
})();
