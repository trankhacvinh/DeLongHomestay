window.DeLongCustomerAutocomplete = {
    methods: {
        attachCustomerAutocomplete() {
            if (!window.TomSelect || this.editor?.mode !== 'create') return;
            const host = this.$el?.nodeType === 1 ? this.$el : this.$el?.parentElement;
            const input = host?.querySelector('[data-admin-customer-phone]');
            if (!input || input.tomselect) return;
            if (this.customerAutocomplete) this.customerAutocomplete.destroy();
            const owner = this;
            let selected = null;
            let requestVersion = 0;
            const normalize = value => {
                const digits = String(value || '').replace(/\D/g, '');
                return digits.startsWith('84') && digits.length >= 10 ? `0${digits.slice(2)}` : digits;
            };
            const initialPhone = this.form.customerPhone || '';
            const select = new TomSelect(input, {
                valueField: 'phone', labelField: 'phone', searchField: ['phone', 'name'], maxItems: 1,
                loadThrottle: 300, createOnBlur: true, persist: false, disabledField: 'disabled',
                options: initialPhone ? [{ phone: initialPhone, name: this.form.customerName || '' }] : [],
                items: initialPhone ? [initialPhone] : [],
                create: value => ({ phone: value.trim(), name: '' }),
                shouldLoad: query => query.trim().length >= 3,
                render: {
                    option: (item, escape) => `<div>${escape(item.phone)} — ${escape(item.name || 'Khách mới')}${item.isBlocked ? ' · Đã chặn' : item.isActive === false ? ' · Hồ sơ đã ẩn' : ''}</div>`,
                    item: (item, escape) => `<div>${escape(item.phone)}</div>`,
                    no_results: () => '<div>Chưa có khách phù hợp. Tiếp tục nhập để tạo khách mới.</div>'
                },
                load: async function (query, callback) {
                    const version = ++requestVersion;
                    try {
                        const data = await DeLongApi.get(`/api/admin/properties/${owner.propertyId}/customers/autocomplete?q=${encodeURIComponent(query.trim())}`);
                        if (version !== requestVersion || !input.isConnected) return callback();
                        const options = data.map(item => ({ ...item, disabled: item.isBlocked }));
                        callback(options);
                        const exact = options.find(item => !item.disabled && normalize(item.phone) === normalize(query));
                        if (exact && normalize(owner.form.customerPhone) === normalize(query)) {
                            this.addOption(exact);
                            this.updateOption(exact.phone, exact);
                            this.setValue(exact.phone);
                        }
                    } catch { callback(); }
                },
                onType(query) {
                    requestVersion++;
                    if (selected) {
                        if (owner.form.customerName === selected.name) owner.form.customerName = '';
                        if (owner.form.customerEmail === (selected.email || '')) owner.form.customerEmail = '';
                    }
                    selected = null;
                    owner.form.customerId = null;
                    owner.form.customerPhone = query;
                },
                onChange(value) {
                    owner.form.customerPhone = value || '';
                    const item = this.options[value];
                    if (!item?.id) { owner.form.customerId = null; return; }
                    selected = item;
                    owner.form.customerId = item.id;
                    owner.form.customerName = item.name;
                    owner.form.customerEmail = item.email || '';
                },
                onBlur() {
                    const typed = this.control_input.value.trim();
                    if (typed && !this.getValue()) this.createItem(typed);
                }
            });
            this.customerAutocomplete = window.Vue?.markRaw ? window.Vue.markRaw(select) : select;
        }
    },
    mounted() {
        this.customerAutocompleteObserver = new MutationObserver(() => {
            if (this.customerAutocomplete && !this.customerAutocomplete.input.isConnected) {
                this.customerAutocomplete.destroy();
                this.customerAutocomplete = null;
            }
            this.attachCustomerAutocomplete();
        });
        const host = this.$el?.nodeType === 1 ? this.$el : this.$el?.parentElement;
        if (host) this.customerAutocompleteObserver.observe(host, { childList: true, subtree: true });
        this.$nextTick(() => this.attachCustomerAutocomplete());
    },
    beforeUnmount() {
        this.customerAutocompleteObserver?.disconnect();
        this.customerAutocomplete?.destroy();
    }
};
