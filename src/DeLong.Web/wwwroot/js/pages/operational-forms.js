(function () {
    function localInput(value, zone) {
        const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value));
        const get = key => parts.find(x => x.type === key)?.value || '';
        return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
    }
    function toUtc(value, zone) {
        if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value || '')) throw new Error('Nhập ngày giờ hợp lệ.');
        const target = Date.parse(value + ':00Z');
        let instant = target;
        for (let i = 0; i < 4; i++) {
            const represented = Date.parse(localInput(instant, zone) + ':00Z');
            const difference = target - represented;
            if (!difference) return new Date(instant).toISOString();
            instant += difference;
        }
        throw new Error('Giờ này không tồn tại trong múi giờ của chi nhánh.');
    }
    window.DeLongOperations = {
        localInput, toUtc,
        money: value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(Number(value || 0)),
        quantity: value => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 }).format(Number(value || 0)),
        dateText: (value, zone) => new Intl.DateTimeFormat('vi-VN', { timeZone: zone, dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
    };
})();
