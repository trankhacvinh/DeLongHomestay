(() => {
    const node = document.getElementById('data-quality-data');
    if (!node || !window.Vue) return;
    const initial = JSON.parse(node.textContent);
    Vue.createApp({
        data: () => ({ ...initial, filter: 'pending', search: '', busy: false, saving: false, error: '', selected: null, form: {}, history: [], loading: false, searchTimer: null, requestVersion: 0 }),
        computed: {
            base() { return `/api/admin/properties/${this.propertyId}/data-quality`; },
            visible() {
                return this.result.items.filter(x => (this.filter === 'all' || (this.filter === 'pending' ? x.isDetected && ['Open','InProgress'].includes(x.status) : this.filter === 'cleared' ? !x.isDetected : x.status === this.filter)) &&
                    (!this.search || `${x.title} ${x.evidenceJson}`.toLocaleLowerCase('vi').includes(this.search.toLocaleLowerCase('vi'))));
            }
        },
        watch: {
            filter() { void this.load(1); },
            search() { clearTimeout(this.searchTimer); this.searchTimer = setTimeout(() => { void this.load(1); }, 300); }
        },
        beforeUnmount() { clearTimeout(this.searchTimer); this.requestVersion++; },
        methods: {
            async load(page = 1) {
                const version = ++this.requestVersion; this.loading = true;
                try {
                    const query = new URLSearchParams({ page, filter:this.filter, search:this.search });
                    const result = await DeLongApi.get(`${this.base}/?${query}`);
                    if (version === this.requestVersion) this.result = result;
                } catch(e) { if (version === this.requestVersion) this.error = e.message; }
                finally { if (version === this.requestVersion) this.loading = false; }
            },
            evidence(x) { return JSON.parse(x.evidenceJson); },
            money(x) { return `${Number(x || 0).toLocaleString('vi-VN')}đ`; },
            date(x) { return x ? new Date(x).toLocaleString('vi-VN', { timeZone: this.timeZoneId }) : '—'; },
            status(x) { return ({Open:'Cần xử lý',InProgress:'Đang xử lý',Resolved:'Đã xử lý',Accepted:'Đã kiểm tra hợp lệ'})[x] || x; },
            action(x) { return ({Detected:'Phát hiện',EvidenceChanged:'Dữ liệu thay đổi, cần kiểm tra lại',NoLongerDetected:'Không còn phát hiện',Reviewed:'Ghi nhận xử lý'})[x] || x; },
            historyNote(x) { const after = x.afterJson ? JSON.parse(x.afterJson) : {}; return after.note || ''; },
            bookingUrl(x) { return `/Admin/Bookings/Index?propertyId=${this.propertyId}&bookingId=${x.bookingId}`; },
            async scan() {
                if (this.busy) return;
                this.busy = true; this.error = '';
                try { await DeLongApi.post(`${this.base}/scan`, {}); await this.load(this.result.page || 1); }
                catch (e) { this.error = e.message; }
                finally { this.busy = false; }
            },
            async open(x) {
                this.selected = x; this.history = []; this.form = {status: x.isDetected ? 'InProgress' : 'Resolved', note: x.reviewNote || ''};
                try { const history = await DeLongApi.get(`${this.base}/${x.id}/history`); if (this.selected?.id === x.id) this.history = history; }
                catch(e) { this.error = e.message; }
            },
            close() { if (!this.saving) this.selected = null; },
            async save() {
                if (this.saving) return;
                if ((this.form.note || '').length < 3) { this.error = 'Nhập ghi chú ít nhất 3 ký tự.'; return; }
                this.saving = true; this.error = '';
                try { await DeLongApi.put(`${this.base}/${this.selected.id}`, this.form); this.selected = null; await this.load(this.result.page || 1); }
                catch(e) { this.error = e.message; }
                finally { this.saving = false; }
            },
            askAi(x) {
                const prompt = `Phân tích mục kiểm tra dữ liệu ${x.id}: ${x.title}. Giải thích số liệu, đề xuất các bước đối soát thủ công. Không tự thay đổi tiền hay đơn.`;
                window.dispatchEvent(new CustomEvent('delong:ai-prompt', {detail: {propertyId: this.propertyId, prompt}}));
            }
        }
    }).mount('#data-quality-page');
})();
