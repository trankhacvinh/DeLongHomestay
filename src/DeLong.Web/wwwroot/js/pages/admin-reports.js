(function () {
    const root = document.getElementById('reports-page');
    if (!root || !window.Vue) return;

    const initial = JSON.parse(document.getElementById('reports-page-data').textContent || '{}');
    const { createApp } = Vue;
    const palette = ['#176b60', '#6b9f8d', '#d39a45', '#b95f54', '#78918b', '#8a6c53', '#4f7f91'];

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
                report: {
                    byRoom: [], bySource: [], byStatus: [], byPaymentMethod: [], byExpenseCategory: [], byWeekday: [], outstandingBuckets: [], daily: [], weekly: [], trend: [],
                    periodReceipts: { today: 0, thisWeek: 0, selectedMonth: 0, previousMonth: 0, monthChangePercent: 0 },
                    ...(initial.report || {})
                }
            };
        },
        computed: {
            monthLabel() {
                const [year, month] = this.month.split('-').map(Number);
                return new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, 1)));
            },
            periodChangeText() {
                const value = this.report.periodReceipts.monthChangePercent;
                if (value === null || value === undefined) return 'Chưa có kỳ đối chiếu';
                if (Number(value) === 0) return 'Không thay đổi';
                return `${Number(value) > 0 ? '+' : ''}${Number(value).toLocaleString('vi-VN')}%`;
            },
            periodChangeClass() {
                const value = this.report.periodReceipts.monthChangePercent;
                return value > 0 ? 'text-success' : value < 0 ? 'text-danger' : '';
            },
            exportUrl() {
                const query = new URLSearchParams({ handler: 'Export', propertyId: this.propertyId, month: this.month, scope: this.scope });
                return `/Admin/Reports?${query.toString()}`;
            },
            hasDailyData() {
                return (this.report.daily || []).some(x => Number(x.bookingValue || 0) !== 0 || Number(x.netReceipts || 0) !== 0 || Number(x.expenses || 0) !== 0);
            },
            dailyMax() {
                return Math.max(1, ...(this.report.daily || []).flatMap(x => [Math.abs(Number(x.bookingValue || 0)), Math.abs(Number(x.netReceipts || 0)), Math.abs(Number(x.expenses || 0))]));
            },
            chartTicks() {
                return [this.dailyMax, this.dailyMax * .75, this.dailyMax * .5, this.dailyMax * .25, 0];
            },
            statusItems() {
                const rows = this.report.byStatus || [];
                const total = rows.reduce((sum, item) => sum + Number(item.bookingCount || 0), 0);
                return rows.map((item, index) => ({
                    key: item.status,
                    label: this.statusLabel(item.status),
                    value: Number(item.bookingCount || 0),
                    percent: total ? Number(item.bookingCount || 0) / total * 100 : 0,
                    color: palette[index % palette.length]
                }));
            },
            paymentMethodItems() {
                const rows = this.report.byPaymentMethod || [];
                const total = rows.reduce((sum, item) => sum + Number(item.grossReceipts || 0), 0);
                return rows.map((item, index) => ({
                    key: item.method,
                    label: this.paymentMethodLabel(item.method),
                    value: Number(item.grossReceipts || 0),
                    percent: total ? Number(item.grossReceipts || 0) / total * 100 : 0,
                    color: palette[index % palette.length]
                }));
            },
            totalStatusBookings() { return this.statusItems.reduce((sum, item) => sum + item.value, 0); },
            totalGrossReceipts() { return this.paymentMethodItems.reduce((sum, item) => sum + item.value, 0); },
            hasTrendData() { return (this.report.trend || []).some(x => Number(x.netReceipts || 0) !== 0 || Number(x.expenses || 0) !== 0); },
            trendMax() {
                return Math.max(1, ...(this.report.trend || []).flatMap(x => [Math.abs(Number(x.netReceipts || 0)), Math.abs(Number(x.expenses || 0))]));
            },
            weeklyMax() {
                return Math.max(1, ...(this.report.weekly || []).flatMap(x => [Math.abs(Number(x.netReceipts || 0)), Math.abs(Number(x.expenses || 0))]));
            },
            expenseMax() { return Math.max(1, ...(this.report.byExpenseCategory || []).map(x => Number(x.amount || 0))); },
            weekdayMax() { return Math.max(1, ...(this.report.byWeekday || []).map(x => Number(x.bookingValue || 0))); }
        },
        methods: {
            money(value) { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value || 0); },
            compactMoney(value) { return new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 }).format(value || 0); },
            percent(value) { return `${Number(value || 0).toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%`; },
            shortMonth(value) {
                const [year, month] = value.split('-').map(Number);
                return new Intl.DateTimeFormat('vi-VN', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, 1)));
            },
            dayNumber(value) { return String(Number(value.slice(-2))); },
            showDayLabel(index) { return index === 0 || index === this.report.daily.length - 1 || index % 5 === 4; },
            chartX(index, count) { return count <= 1 ? 496 : 54 + index * (884 / (count - 1)); },
            chartY(value, max) { return 24 + (1 - Math.max(0, Number(value || 0)) / Math.max(1, max)) * 236; },
            dailyLinePoints(key) { return (this.report.daily || []).map((item, index) => `${this.chartX(index, this.report.daily.length)},${this.chartY(item[key], this.dailyMax)}`).join(' '); },
            dailyTooltip(item) { return `${this.dateLabel(item.date)} · Booking ${this.money(item.bookingValue)} · Thực thu ${this.money(item.netReceipts)} · Chi phí ${this.money(item.expenses)}`; },
            dateLabel(value) { return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)); },
            weekLabel(week) { return `${this.dateLabel(week.weekStart).slice(0, 5)} – ${this.dateLabel(week.weekEnd).slice(0, 5)}`; },
            weeklyWidth(value) { return `${Math.max(Number(value || 0) === 0 ? 0 : 4, Math.abs(Number(value || 0)) / this.weeklyMax * 100)}%`; },
            relativeWidth(value, max) { return `${Math.max(Number(value || 0) === 0 ? 0 : 3, Math.abs(Number(value || 0)) / Math.max(1, max) * 100)}%`; },
            barHeight(value) { return Math.max(Number(value || 0) === 0 ? 0 : 2, Math.round(Math.abs(Number(value || 0)) / this.trendMax * 100)); },
            statusLabel(value) { return ({ Requested: 'Yêu cầu', Held: 'Đang giữ', Confirmed: 'Đã xác nhận', CheckedIn: 'Đang ở', Completed: 'Hoàn tất', Cancelled: 'Đã hủy', NoShow: 'Không đến' })[value] || value; },
            paymentMethodLabel(value) { return ({ Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản', Card: 'Thẻ', Other: 'Khác', Pay2S: 'Pay2S' })[value] || value; },
            donutStyle(items) {
                let cursor = 0;
                const stops = items.map(item => {
                    const start = cursor;
                    cursor += item.percent;
                    return `${item.color} ${start}% ${cursor}%`;
                });
                return { background: `conic-gradient(${stops.join(',')})` };
            },
            navigate(month, scope) {
                const query = new URLSearchParams({ propertyId: this.propertyId, month, scope });
                window.location.assign(`/Admin/Reports?${query.toString()}`);
            },
            moveMonth(delta) { this.navigate(shiftMonth(this.month, delta), this.scope); },
            changeScope() { this.navigate(this.month, this.scope); }
        }
    }).mount(root);
})();
