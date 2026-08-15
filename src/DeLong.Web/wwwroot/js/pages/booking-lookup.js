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
                const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value);
                return match ? `${match[4]}:${match[5]} ${match[3]}/${match[2]}/${match[1]}` : value;
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
