(function () {
    const root = document.getElementById('reports-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('reports-page-data').textContent || '{}');
    const { createApp } = Vue;

    function shiftMonth(value, delta) {
        const [year, month] = value.split('-').map(Number);
        const date = new Date(Date.UTC(year, month - 1 + delta, 1));
        return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`;
    }

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                scope: initial.scope || initial.propertyId,
                scopeName: initial.scopeName || initial.propertyName || '',
                properties: initial.properties || [],
                month: initial.month,
                report: initial.report || { byRoom: [], bySource: [], trend: [] }
            };
        },
        computed: {
            monthLabel() {
                const [year, month] = this.month.split('-').map(Number);
                return new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric', timeZone: 'UTC' })
                    .format(new Date(Date.UTC(year, month - 1, 1)));
            },
            hasTrendData() {
                return (this.report.trend || []).some(x => Number(x.netReceipts || 0) !== 0 || Number(x.expenses || 0) !== 0);
            },
            trendMax() {
                const values = this.report.trend.flatMap(x => [Math.abs(Number(x.netReceipts || 0)), Math.abs(Number(x.expenses || 0))]);
                return Math.max(1, ...values);
            }
        },
        methods: {
            money(value) {
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0);
            },
            compactMoney(value) {
                return new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 }).format(value || 0);
            },
            shortMonth(value) {
                const [year, month] = value.split('-').map(Number);
                return new Intl.DateTimeFormat('vi-VN', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, 1)));
            },
            barHeight(value) {
                return Math.max(2, Math.round(Math.abs(Number(value || 0)) / this.trendMax * 100));
            },
            navigate(month, scope) {
                const query = new URLSearchParams({ propertyId: this.propertyId, month, scope });
                window.location.assign(`/Admin/Reports?${query.toString()}`);
            },
            moveMonth(delta) {
                this.navigate(shiftMonth(this.month, delta), this.scope);
            },
            changeScope() {
                this.navigate(this.month, this.scope);
            }
        }
    }).mount(root);
})();
