(function () {
    const root = document.getElementById('bookings-page');
    if (!root || !window.DeLongApi || window.DeLongApi.__identityViewerWrapped) return;
    const initial = (() => {
        try { return JSON.parse(document.getElementById('bookings-page-data')?.textContent || '{}'); }
        catch { return {}; }
    })();
    const propertyId = initial.propertyId;
    if (!propertyId) return;
    const originalGet = window.DeLongApi.get.bind(window.DeLongApi);
    let currentBookingId = '';
    let currentDocuments = [];
    let loadError = '';

    function documentUrl(bookingId, side) {
        return `/api/admin/properties/${propertyId}/bookings/${bookingId}/identity-documents/${side}?v=${Date.now()}`;
    }

    function render() {
        const detailList = root.querySelector('.booking-detail-list');
        if (!detailList || !currentBookingId) return;
        let panel = root.querySelector('[data-booking-identity-panel]');
        if (!panel) {
            panel = document.createElement('section');
            panel.className = 'booking-identity-panel';
            panel.dataset.bookingIdentityPanel = 'true';
            detailList.insertAdjacentElement('afterend', panel);
        }
        if (loadError) {
            panel.innerHTML = `<div class="booking-identity-head"><div><strong>CCCD / giấy tờ</strong><small>Dữ liệu riêng tư</small></div></div><div class="booking-identity-warning"></div>`;
            panel.querySelector('.booking-identity-warning').textContent = loadError;
            return;
        }
        const bySide = Object.fromEntries(currentDocuments.map(item => [item.side, item]));
        panel.innerHTML = `
            <div class="booking-identity-head"><div><strong>CCCD / giấy tờ</strong><small>Chỉ giải mã khi người có quyền mở ảnh. Trình duyệt được yêu cầu không cache.</small></div><span class="pill">Mã hóa</span></div>
            <div class="booking-identity-grid">
                ${cardHtml('front', 'Mặt trước', bySide.front)}
                ${cardHtml('back', 'Mặt sau', bySide.back)}
            </div>`;
    }

    function cardHtml(side, label, document) {
        if (!document) return `<div class="booking-identity-view"><strong>${label}</strong><span class="booking-identity-empty">Chưa có ảnh</span></div>`;
        const url = documentUrl(currentBookingId, side);
        return `<div class="booking-identity-view"><strong>${label}</strong><a href="${url}" target="_blank" rel="noopener" title="Mở ảnh ${label}"><img src="${url}" alt="${label}" loading="lazy" /></a></div>`;
    }

    async function loadIdentity(bookingId) {
        if (!bookingId) return;
        currentBookingId = bookingId;
        currentDocuments = [];
        loadError = '';
        try {
            const payload = await originalGet(`/api/admin/properties/${propertyId}/bookings/${bookingId}/identity-documents`);
            currentDocuments = Array.isArray(payload?.documents) ? payload.documents : [];
        } catch (error) {
            loadError = error.status === 503
                ? 'Không thể đọc khóa mã hóa CCCD. Hãy kiểm tra DataRoot/security hoặc khôi phục toàn bộ DataRoot từ bản sao lưu.'
                : (error.message || 'Không thể tải trạng thái CCCD.');
        }
        requestAnimationFrame(render);
    }

    window.DeLongApi.get = async function (url) {
        const result = await originalGet(url);
        const match = String(url).match(/\/api\/admin\/properties\/[^/]+\/bookings\/([0-9a-f-]{36})\/payments(?:\?|$)/i);
        if (match) void loadIdentity(match[1]);
        return result;
    };
    window.DeLongApi.__identityViewerWrapped = true;
    new MutationObserver(render).observe(root, { childList: true, subtree: true });

    const deepLinkedBookingId = new URLSearchParams(window.location.search).get('bookingId');
    if (deepLinkedBookingId) void loadIdentity(deepLinkedBookingId);
})();
