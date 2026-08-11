(function () {
    const root = document.getElementById('customers-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('customers-page-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                customers: initial.customers || [],
                search: '',
                showInactive: false,
                saving: false,
                editor: { open: false, mode: 'create', customerId: null },
                form: { name: '', phone: '', email: '', identityNumber: '', note: '', isActive: true },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            filteredCustomers() {
                const q = this.search.toLowerCase();
                return this.customers.filter(customer => {
                    if (!this.showInactive && !customer.isActive) return false;
                    return !q || customer.name.toLowerCase().includes(q) || customer.phone.toLowerCase().includes(q);
                });
            }
        },
        methods: {
            dateText(value) {
                return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));
            },
            openCreate() {
                this.form = { name: '', phone: '', email: '', identityNumber: '', note: '', isActive: true };
                this.editor = { open: true, mode: 'create', customerId: null };
            },
            openEdit(customer) {
                this.form = {
                    name: customer.name,
                    phone: customer.phone,
                    email: customer.email || '',
                    identityNumber: customer.identityNumber || '',
                    note: customer.note || '',
                    isActive: customer.isActive
                };
                this.editor = { open: true, mode: 'edit', customerId: customer.id };
            },
            closeEditor() { if (!this.saving) this.editor.open = false; },
            validate() {
                if (!this.form.name.trim()) return 'Vui lòng nhập tên khách.';
                if (this.form.phone.replace(/\D/g, '').length < 8) return 'Số điện thoại không hợp lệ.';
                return null;
            },
            async saveCustomer() {
                const validation = this.validate();
                if (validation) return this.notify(validation, 'error');
                this.saving = true;
                try {
                    const base = `/api/admin/properties/${this.propertyId}/customers`;
                    const payload = {
                        name: this.form.name,
                        phone: this.form.phone,
                        email: this.form.email || null,
                        identityNumber: this.form.identityNumber || null,
                        note: this.form.note || null,
                        isActive: this.form.isActive
                    };
                    let customer;
                    if (this.editor.mode === 'create') {
                        const createPayload = { ...payload };
                        delete createPayload.isActive;
                        customer = await DeLongApi.post(base, createPayload);
                        this.customers.unshift(customer);
                    } else {
                        customer = await DeLongApi.put(`${base}/${this.editor.customerId}`, payload);
                        const index = this.customers.findIndex(x => x.id === customer.id);
                        if (index >= 0) this.customers.splice(index, 1, customer);
                    }
                    this.editor.open = false;
                    this.notify('Đã lưu khách hàng.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu khách hàng.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 3200);
                this.toast = { show: true, message, type, timer };
            }
        }
    }).mount(root);
})();
