window.DeLongCalendarDailySummary = {
    create({ propertyId, timeZone, openBooking }) {
        let summaries = new Map();
        let requestVersion = 0;
        const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(Number(value || 0));
        const time = value => new Intl.DateTimeFormat('vi-VN', { timeZone, dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
        const element = (tag, text, className) => {
            const node = document.createElement(tag);
            if (text !== undefined) node.textContent = text;
            if (className) node.className = className;
            return node;
        };
        async function show(date) {
            const dialog = element('dialog', undefined, 'calendar-day-dialog');
            const header = element('header');
            const title = element('h2', `Kiểm kê ngày ${date.split('-').reverse().join('/')}`);
            title.id = 'calendar-day-summary-title';
            dialog.setAttribute('aria-labelledby', title.id);
            const close = element('button', 'Đóng', 'btn btn-light');
            close.type = 'button'; close.addEventListener('click', () => dialog.close());
            header.append(title, close);
            const body = element('div', 'Đang tải thống kê toàn cơ sở…', 'calendar-day-body');
            body.setAttribute('aria-live', 'polite');
            dialog.append(header, body);
            dialog.addEventListener('close', () => dialog.remove(), { once: true });
            dialog.addEventListener('click', event => { if (event.target === dialog) { const rect = dialog.getBoundingClientRect(); if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) dialog.close(); } });
            document.body.append(dialog); dialog.showModal();
            async function load() {
                body.textContent = 'Đang tải thống kê toàn cơ sở…';
                try {
                    const detail = await DeLongApi.get(`/api/admin/properties/${propertyId}/bookings/daily-summary/${date}`);
                    if (!dialog.isConnected) return;
                    body.replaceChildren();
                    const summary = detail.summary;
                    body.append(element('p', `${summary.bookingCount} lượt · Giá trị: ${money(summary.bookingValue)} · Thực thu: ${money(summary.netReceipts)}`, 'calendar-day-totals'));
                    body.append(element('p', 'Toàn cơ sở. Lượt/giá trị tính theo ngày nhận phòng, loại đơn hủy/không đến. Thực thu tính theo ngày giao dịch, đã trừ hoàn tiền và loại giao dịch vô hiệu.'));
                    body.append(element('h3', 'Booking nhận phòng trong ngày'));
                    const statuses = { Requested: 'Yêu cầu', Held: 'Giữ phòng', Confirmed: 'Đã xác nhận', CheckedIn: 'Đã nhận phòng', Completed: 'Hoàn tất' };
                    function bookingButton(id, label) {
                        const button = element('button', label, 'btn btn-light btn-sm'); button.type = 'button';
                        button.addEventListener('click', () => { dialog.close(); openBooking(id); });
                        return button;
                    }
                    for (const booking of detail.bookings) {
                        const row = element('article', undefined, 'calendar-day-row');
                        row.append(element('strong', `${booking.roomName} · ${booking.customerName}`),
                            element('span', `${time(booking.checkInUtc)} → ${time(booking.checkOutUtc)}`),
                            element('span', `${statuses[booking.status] || booking.status} · ${money(booking.amount)}`), bookingButton(booking.id, booking.code));
                        body.append(row);
                    }
                    if (!detail.bookings.length) body.append(element('p', 'Không có booking nhận phòng trong ngày.'));
                    body.append(element('h3', 'Thu / hoàn tiền trong ngày'));
                    const methods = { Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản', Card: 'Thẻ', Pay2S: 'Pay2S', SePay: 'SePay', Other: 'Khác' };
                    for (const payment of detail.payments) {
                        const row = element('article', undefined, 'calendar-day-row');
                        row.append(element('strong', `${payment.type === 'Receipt' ? 'Thu' : 'Hoàn'} ${money(payment.amount)} · ${methods[payment.method] || payment.method}`),
                            element('span', `${payment.roomName} · ${time(payment.occurredAtUtc)}`), bookingButton(payment.bookingId, payment.bookingCode));
                        body.append(row);
                    }
                    if (!detail.payments.length) body.append(element('p', 'Không có giao dịch trong ngày.'));
                } catch {
                    if (!dialog.isConnected) return;
                    body.textContent = 'Không thể tải thống kê. ';
                    const retry = element('button', 'Thử lại', 'btn btn-light'); retry.type = 'button'; retry.addEventListener('click', load); body.append(retry);
                }
            }
            await load();
        }
        return {
            async load(from, days) {
                const version = ++requestVersion;
                try {
                    const rows = await DeLongApi.get(`/api/admin/properties/${propertyId}/bookings/daily-summary?${new URLSearchParams({ from, days })}`);
                    if (version === requestVersion) summaries = new Map(rows.map(row => [row.date, row]));
                    return rows;
                } catch {
                    if (version === requestVersion) summaries = new Map();
                    return null;
                }
            },
            button(date) {
                const row = summaries.get(date);
                const button = element('button', undefined, 'calendar-day-summary'); button.type = 'button';
                button.setAttribute('aria-label', `Xem kiểm kê toàn cơ sở ngày ${date}`);
                if (row) button.append(element('strong', `${row.bookingCount} lượt`), element('span', `Giá trị ${money(row.bookingValue)}`), element('span', `Thực thu ${money(row.netReceipts)}`));
                else button.textContent = 'Xem thống kê ngày';
                button.addEventListener('click', () => show(date));
                return button;
            }
        };
    }
};
