(function () {
    const root = document.getElementById('booking-lookup');
    if (!root || !window.Vue) return;

    const { createApp } = Vue;
    createApp({
        data() {
            return {
                form: { code: '', phone: '' },
                loading: false,
                result: null,
                error: ''
            };
        },
        computed: {
            canSubmit() {
                return this.form.code.trim().length >= 8 && this.form.phone.trim().length >= 8;
            }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            dateTime(value) {
                if (!value) return '';
                const date = new Date(value);
                return new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }).format(date);
            },
            async lookup() {
                if (!this.canSubmit || this.loading) return;
                this.loading = true;
                this.result = null;
                this.error = '';
                try {
                    this.result = await DeLongApi.post('/api/public/booking-lookup', this.form);
                } catch (error) {
                    this.error = error.status === 429
                        ? 'Bạn đã tra cứu quá nhiều lần. Vui lòng thử lại sau ít phút.'
                        : (error.message || 'Không tìm thấy lượt đặt phù hợp với thông tin đã nhập.');
                } finally {
                    this.loading = false;
                }
            }
        }
    }).mount(root);
})();
