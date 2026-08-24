(function () {
    const root = document.querySelector('[data-pay2s-return]');
    if (!root) return;
    const orderId = root.dataset.orderId;
    const message = root.querySelector('[data-payment-message]');
    const expiry = root.querySelector('[data-payment-expiry]');
    const resume = root.querySelector('[data-payment-resume]');
    if (!orderId) { message.textContent = 'Thiếu mã phiên thanh toán.'; return; }

    let stopped = false;
    let pollDelayMs = 3000;
    let rateLimitCount = 0;

    async function confirmSignedReturn() {
        const query = new URLSearchParams(window.location.search);
        if (!query.get('m2signature') || query.get('resultCode') === null) return;
        const payload = {
            partnerCode: query.get('partnerCode') || '',
            orderId: query.get('orderId') || orderId,
            requestId: query.get('requestId') || '',
            amount: query.get('amount') || '',
            orderInfo: query.get('orderInfo') || '',
            orderType: query.get('orderType') || '',
            transId: query.get('transId') || '',
            resultCode: query.get('resultCode') || '',
            message: query.get('message') || '',
            payType: query.get('payType') || '',
            responseTime: query.get('responseTime') || '',
            m2signature: query.get('m2signature') || ''
        };
        try {
            await DeLongApi.post(`/api/public/payments/pay2s/${encodeURIComponent(orderId)}/confirm-return`, payload);
            window.history.replaceState({}, document.title, `${window.location.pathname}?orderId=${encodeURIComponent(orderId)}`);
        } catch (error) {
            message.textContent = error?.message || 'Chưa xác thực được kết quả Pay2S trả về. Hệ thống vẫn tiếp tục chờ IPN.';
        }
    }
    resume.addEventListener('click', async (event) => {
        event.preventDefault();
        if (resume.dataset.loading === 'true') return;
        resume.dataset.loading = 'true';
        resume.textContent = 'Đang kiểm tra…';
        try {
            const result = await DeLongApi.get(`/api/public/payments/pay2s/${encodeURIComponent(orderId)}/resume`);
            if (!result.paymentUrl) throw new Error('Phiên thanh toán không còn hiệu lực.');
            window.location.assign(result.paymentUrl);
        } catch (error) {
            resume.hidden = true;
            stopped = true;
            message.textContent = error?.message || 'Phiên giữ phòng đã kết thúc. Vui lòng tạo lượt đặt mới.';
        } finally {
            resume.dataset.loading = 'false';
            resume.textContent = 'Tiếp tục thanh toán';
        }
    });

    async function poll() {
        try {
            const result = await DeLongApi.get(`/api/public/payments/pay2s/${encodeURIComponent(orderId)}`);
            rateLimitCount = 0;
            pollDelayMs = 3000;
            resume.hidden = !result.canResume;
            if (result.inSettlementGrace && result.releaseAtUtc) {
                expiry.textContent = `Đang chờ ngân hàng xác nhận. Phòng vẫn được khóa đến ${new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(result.releaseAtUtc))}.`;
                message.textContent = 'Đã hết thời gian khởi tạo thanh toán mới. Hệ thống vẫn chờ kết quả giao dịch đang xử lý.';
            } else if (result.expiresAtUtc) {
                expiry.textContent = `Bạn có thể thanh toán đến ${new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(result.expiresAtUtc))}.`;
            }
            if (result.status === 'Succeeded') {
                stopped = true;
                localStorage.removeItem('delong.pendingPay2S');
                window.location.replace(result.successUrl || `/booking/success?code=${encodeURIComponent(result.bookingCode)}`);
                return;
            }
            if (result.status === 'PaidAfterExpiry') { stopped = true; message.textContent = 'Khoản tiền đến sau khi giữ phòng hết hạn. Cơ sở đã nhận thông tin và sẽ liên hệ để xử lý.'; return; }
            if (result.status === 'Expired') { stopped = true; resume.hidden = true; expiry.textContent = 'Phòng không còn được giữ cho lượt đặt này.'; message.textContent = 'Phiên giữ phòng đã hết hạn. Vui lòng tạo lượt đặt mới.'; return; }
            if (result.status === 'Failed') { stopped = true; message.textContent = 'Giao dịch chưa thành công. Bạn có thể tiếp tục thanh toán trước khi hết hạn.'; }
        } catch (error) {
            if (error?.status === 429) {
                rateLimitCount += 1;
                pollDelayMs = Math.min(30000, 3000 * (2 ** Math.min(rateLimitCount, 3)));
                message.textContent = 'Hệ thống đang nhận nhiều yêu cầu xác nhận. Trạng thái thanh toán sẽ được kiểm tra lại tự động.';
            } else {
                pollDelayMs = Math.min(15000, Math.max(5000, pollDelayMs * 1.5));
                message.textContent = 'Chưa đọc được trạng thái. Hệ thống sẽ tự thử lại.';
            }
        }
        if (!stopped) window.setTimeout(poll, pollDelayMs);
    }
    confirmSignedReturn().finally(poll);
})();
