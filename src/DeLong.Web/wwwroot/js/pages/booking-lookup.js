(function () {
    const root = document.getElementById('booking-lookup');
    if (!root || !window.Vue) return;

    const siteSlug = root.dataset.siteSlug || '';
    const { createApp } = Vue;
    createApp({
        data() {
            return {
                form: { code: '', phone: '' },
                loading: false,
                result: null,
                error: '',
                downloadingGuide: false
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
                    const endpoint = siteSlug
                        ? `/api/public/booking-lookup?siteSlug=${encodeURIComponent(siteSlug)}`
                        : '/api/public/booking-lookup';
                    this.result = await DeLongApi.post(endpoint, this.form);
                } catch (error) {
                    this.error = error.status === 429
                        ? 'Bạn đã tra cứu quá nhiều lần. Vui lòng thử lại sau ít phút.'
                        : (error.message || 'Không tìm thấy lượt đặt phù hợp với thông tin đã nhập.');
                } finally {
                    this.loading = false;
                }
            },
            async downloadGuide() {
                if (!this.result || this.downloadingGuide) return;
                this.downloadingGuide = true;
                try {
                    const endpoint = siteSlug
                        ? `/api/public/booking-guide-pdf?siteSlug=${encodeURIComponent(siteSlug)}`
                        : '/api/public/booking-guide-pdf';
                    const csrf = document.querySelector('meta[name="csrf-token"]')?.content || '';
                    const response = await fetch(endpoint, {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf },
                        body: JSON.stringify(this.form)
                    });
                    if (!response.ok) throw new Error('Không thể tải hướng dẫn cho lượt đặt này.');
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = `huong-dan-${this.result.code}.pdf`;
                    document.body.appendChild(link);
                    link.click();
                    link.remove();
                    setTimeout(() => URL.revokeObjectURL(url), 1000);
                } catch (error) {
                    this.error = error.message || 'Không thể tải hướng dẫn.';
                } finally {
                    this.downloadingGuide = false;
                }
            }
        }
    }).mount(root);
})();
