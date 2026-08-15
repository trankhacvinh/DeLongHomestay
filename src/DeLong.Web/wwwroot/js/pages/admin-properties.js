(function () {
    const root = document.getElementById('properties-admin');
    if (!root || !window.Vue) return;
    const { createApp } = Vue;
    const emptyForm = () => ({ code: '', name: '', timeZoneId: 'Asia/Ho_Chi_Minh', isActive: true });

    createApp({
        data() {
            return {
                properties: [], loading: true, saving: false, error: '',
                editor: { open: false, mode: 'create', id: null },
                form: emptyForm(), toast: { show: false, type: 'success', message: '' }
            };
        },
        async mounted() { await this.load(); },
        methods: {
            async load() {
                this.loading = true;
                try { this.properties = await DeLongApi.get('/api/admin/properties-admin'); }
                catch (error) { this.notify(error.message || 'Không thể tải danh sách cơ sở.', 'error'); }
                finally { this.loading = false; }
            },
            openCreate() { this.form = emptyForm(); this.error = ''; this.editor = { open: true, mode: 'create', id: null }; },
            openEdit(property) {
                this.form = { code: property.code, name: property.name, timeZoneId: property.timeZoneId, isActive: property.isActive };
                this.error = ''; this.editor = { open: true, mode: 'edit', id: property.id };
            },
            closeEditor() { if (!this.saving) this.editor.open = false; },
            async save() {
                if (this.saving) return;
                if (!this.form.code.trim() || !this.form.name.trim()) { this.error = 'Mã và tên cơ sở là bắt buộc.'; return; }
                this.saving = true; this.error = '';
                try {
                    if (this.editor.mode === 'create') await DeLongApi.post('/api/admin/properties-admin', this.form);
                    else await DeLongApi.put(`/api/admin/properties-admin/${this.editor.id}`, this.form);
                    await this.load(); this.editor.open = false; this.notify('Đã lưu cơ sở.');
                } catch (error) { this.error = error.message || 'Không thể lưu cơ sở.'; }
                finally { this.saving = false; }
            },
            notify(message, type = 'success') {
                this.toast = { show: true, message, type };
                clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2600);
            }
        }
    }).mount(root);
})();
