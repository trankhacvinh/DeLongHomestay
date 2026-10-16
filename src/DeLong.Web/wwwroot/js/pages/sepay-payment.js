(function () {
    const root = document.querySelector('[data-sepay-payment]');
    if (!root) return;
    const qr = root.querySelector('[data-sepay-qr]');
    const qrPlaceholder = root.querySelector('[data-sepay-qr-placeholder]');
    const qrDownload = root.querySelector('[data-sepay-download]');
    const qrRetry = root.querySelector('[data-sepay-qr-retry]');
    const message = root.querySelector('[data-sepay-status]');
    const time = root.querySelector('[data-sepay-time]');
    const stateBox = root.querySelector('[data-sepay-state]');
    let state = null;
    let stopped = false;
    let timer;
    let qrTimer;
    let qrUrl = null;
    let qrLoaded = false;
    function qrReady() {
        if (!state?.qrUrl || !qrUrl || Date.parse(state.expiresAtUtc) <= Date.now()) return;
        clearTimeout(qrTimer);
        qrLoaded = true;
        qr.hidden = false;
        qrDownload.hidden = false;
        qrPlaceholder.hidden = true;
        if (qrRetry) qrRetry.hidden = true;
    }
    function qrFailed() {
        if (!qrUrl || Date.parse(state?.expiresAtUtc) <= Date.now()) return;
        clearTimeout(qrTimer);
        qrLoaded = false;
        qr.hidden = true;
        qrDownload.hidden = true;
        qrPlaceholder.hidden = false;
        qrPlaceholder.textContent = 'Chưa tải được ảnh QR. Vui lòng bấm tải lại.';
        if (qrRetry) qrRetry.hidden = false;
    }
    function updateQr(url, retry = false) {
        if (!url) {
            clearTimeout(qrTimer);
            qrUrl = null;
            qrLoaded = false;
            qr.hidden = true;
            qrDownload.hidden = true;
            qrPlaceholder.hidden = false;
            if (qrRetry) qrRetry.hidden = true;
            qr.removeAttribute('src');
            return;
        }
        if (url === qrUrl && !retry) {
            if (qrLoaded) qrReady();
            return;
        }
        clearTimeout(qrTimer);
        qrUrl = url;
        qrLoaded = false;
        qr.hidden = true;
        qrDownload.hidden = true;
        qrPlaceholder.hidden = false;
        qrPlaceholder.textContent = 'Đang tải mã QR…';
        if (qrRetry) qrRetry.hidden = true;
        if (retry) qr.removeAttribute('src');
        if (qr.getAttribute('src') !== url || retry) qr.src = url;
        if (qr.complete && qr.naturalWidth > 0) qrReady();
        else qrTimer = window.setTimeout(qrFailed, 15000);
    }
    qr.addEventListener('load', qrReady);
    qr.addEventListener('error', qrFailed);
    qrRetry?.addEventListener('click', () => {
        if (state?.qrUrl && Date.parse(state.expiresAtUtc) > Date.now()) updateQr(state.qrUrl, true);
    });
    function tick() {
        if (!state) return;
        const remaining = Math.max(0, Math.ceil((Date.parse(state.expiresAtUtc) - Date.now()) / 1000));
        time.textContent = remaining ? `${Math.floor(remaining / 60)} phút ${String(remaining % 60).padStart(2, '0')} giây` : 'Đã hết thời gian';
        if (!remaining) { updateQr(null); qrPlaceholder.textContent = 'Phiên thanh toán đã hết hạn'; root.querySelector('[data-sepay-memo]').textContent = 'Đã hết thời gian chuyển khoản'; root.querySelector('[data-sepay-destination]').textContent = '—'; }
    }
    function render() {
        root.querySelector('[data-sepay-amount]').textContent = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(state.amount);
        root.querySelector('[data-sepay-memo]').textContent = state.transferContent || 'Phiên đã đóng';
        root.querySelector('[data-sepay-destination]').textContent = state.accountNumber ? `${state.bank} · ${state.accountNumber}` : '';
        if (state.status === 'Succeeded') {
            stopped = true;
            stateBox.classList.add('is-success');
            message.textContent = 'Đã nhận thanh toán. Đang mở thông tin đặt phòng…';
            window.location.assign(state.successUrl);
            return;
        }
        updateQr(state.status === 'Pending' ? state.qrUrl : null);
        if (state.status === 'PaidAfterExpiry') {
            stateBox.classList.add('is-warning');
            message.textContent = 'Đã nhận tiền sau khi phiên giữ phòng đóng. Cơ sở sẽ liên hệ để xử lý; booking chưa được xác nhận lại.';
            stopped = true;
        } else if (state.status !== 'Pending') {
            stateBox.classList.add('is-warning');
            message.textContent = 'Phiên thanh toán đã đóng. Vui lòng không chuyển thêm tiền. Nếu đã chuyển, hãy liên hệ cơ sở để đối soát.';
            stopped = true;
        } else {
            message.textContent = state.qrUrl ? 'Đang chờ tiền chuyển khoản…' : 'Đã hết thời gian chuyển khoản. Đang chờ ngân hàng xác nhận; vui lòng không chuyển thêm.';
        }
        tick();
    }
    async function poll() {
        try {
            const response = await fetch(`/api/public/payments/sepay/${encodeURIComponent(root.dataset.orderId)}`, { cache: 'no-store' });
            if (response.status === 404) { message.textContent = 'Không tìm thấy phiên thanh toán.'; stopped = true; return; }
            if (!response.ok) throw new Error('unavailable');
            state = await response.json();
            render();
        } catch {
            qr.hidden = true;
            qrDownload.hidden = true;
            qrPlaceholder.hidden = false;
            qrPlaceholder.textContent = 'Chưa tải được mã QR';
            stateBox.classList.add('is-warning');
            message.textContent = 'Chưa kết nối được máy chủ. Hệ thống đang thử lại; không chuyển tiền lần nữa nếu đã thanh toán.';
        } finally { if (!stopped) timer = window.setTimeout(poll, 4000); }
    }
    const ticker = window.setInterval(tick, 1000);
    root.querySelector('[data-sepay-copy="memo"]')?.addEventListener('click', async event => {
        const value = root.querySelector('[data-sepay-memo]')?.textContent?.trim();
        if (!value || value === 'Đang tải…') return;
        await navigator.clipboard.writeText(value);
        const button = event.currentTarget;
        button.textContent = 'Đã sao chép';
        window.setTimeout(() => { button.textContent = 'Sao chép'; }, 1600);
    });
    window.addEventListener('pagehide', () => { stopped = true; clearTimeout(timer); clearTimeout(qrTimer); clearInterval(ticker); });
    const initial = root.querySelector('[data-sepay-initial]');
    try { state = initial ? JSON.parse(initial.textContent) : null; } catch { state = null; }
    if (state) {
        render();
        if (!stopped) timer = window.setTimeout(poll, 4000);
    } else poll();
})();
