(() => {
    const drawer = document.querySelector('[data-ai-chat-drawer]');
    if (!drawer || !window.DeLongApi) return;
    const toggle = document.querySelector('[data-ai-chat-toggle]');
    const close = drawer.querySelector('[data-ai-chat-close]');
    const backdrop = document.querySelector('[data-ai-chat-backdrop]');
    const messages = drawer.querySelector('[data-ai-chat-messages]');
    const form = drawer.querySelector('[data-ai-chat-form]');
    const input = drawer.querySelector('[data-ai-chat-input]');
    const submit = form.querySelector('button[type="submit"]');
    const attachmentInput = form.querySelector('[data-ai-attachment-input]');
    const attachmentList = form.querySelector('[data-ai-attachment-list]');
    const historyPanel = drawer.querySelector('[data-ai-chat-history]');
    const historyList = drawer.querySelector('[data-ai-history-list]');
    const propertyId = drawer.dataset.propertyId;
    let conversationId = null;
    let lastRequest = '';
    let pendingAttachments = [];

    const open = () => { drawer.hidden = false; backdrop.hidden = false; requestAnimationFrame(() => { drawer.classList.add('open'); backdrop.classList.add('open'); input.focus(); }); toggle?.setAttribute('aria-expanded', 'true'); };
    const hide = () => { drawer.classList.remove('open'); backdrop.classList.remove('open'); toggle?.setAttribute('aria-expanded', 'false'); setTimeout(() => { drawer.hidden = true; backdrop.hidden = true; }, 180); };
    toggle?.addEventListener('click', open); close?.addEventListener('click', hide); backdrop?.addEventListener('click', hide);
    drawer.querySelectorAll('[data-ai-prompt]').forEach(button => button.addEventListener('click', () => { input.value = button.dataset.aiPrompt; form.requestSubmit(); }));

    function resetMessages() {
        messages.replaceChildren();
        const welcome = document.createElement('div'); welcome.className = 'ai-chat-welcome';
        const title = document.createElement('strong'); title.textContent = 'Tôi có thể giúp gì?';
        const text = document.createElement('span'); text.textContent = 'Tra cứu dữ liệu, tạo báo cáo hoặc đề xuất thay đổi. Bạn có thể đính kèm Word, PDF và ảnh.';
        welcome.append(title, text); messages.append(welcome);
    }
    function renderAttachments() {
        attachmentList.replaceChildren(); attachmentList.hidden = pendingAttachments.length === 0;
        pendingAttachments.forEach(file => {
            const chip = document.createElement('div'); chip.className = 'ai-attachment-chip';
            const name = document.createElement('span'); name.textContent = file.fileName;
            const remove = document.createElement('button'); remove.type = 'button'; remove.textContent = '×'; remove.setAttribute('aria-label', `Bỏ tệp ${file.fileName}`);
            remove.addEventListener('click', () => { pendingAttachments = pendingAttachments.filter(x => x.id !== file.id); renderAttachments(); });
            chip.append(name, remove); attachmentList.append(chip);
        });
    }
    async function ensureConversation() {
        if (conversationId) return conversationId;
        const created = await DeLongApi.post(`/api/admin/properties/${propertyId}/ai/conversations`, {});
        conversationId = created.conversationId; return conversationId;
    }
    async function uploadFiles(files) {
        if (!files.length) return;
        attachmentInput.disabled = true;
        try {
            const id = await ensureConversation(); const data = new FormData();
            Array.from(files).forEach(file => data.append('files', file));
            const uploaded = await DeLongApi.postForm(`/api/admin/properties/${propertyId}/ai/conversations/${id}/attachments`, data);
            pendingAttachments.push(...uploaded); renderAttachments();
        } catch (error) { addMessage('assistant', error.message); }
        finally { attachmentInput.value = ''; attachmentInput.disabled = false; }
    }
    attachmentInput?.addEventListener('change', event => uploadFiles(event.target.files));

    async function loadHistory() {
        historyList.textContent = 'Đang tải...'; historyPanel.hidden = false;
        try {
            const rows = await DeLongApi.get(`/api/admin/properties/${propertyId}/ai/conversations`); historyList.replaceChildren();
            if (!rows.length) { historyList.textContent = 'Chưa có cuộc trò chuyện.'; return; }
            rows.forEach(row => {
                const button = document.createElement('button'); button.type = 'button'; button.className = 'ai-history-item';
                const title = document.createElement('strong'); title.textContent = row.title;
                const time = document.createElement('small'); time.textContent = new Date(row.updatedAtUtc).toLocaleString('vi-VN'); button.append(title, time);
                button.addEventListener('click', async () => {
                    conversationId = row.id; pendingAttachments = []; renderAttachments(); resetMessages();
                    const [history, files] = await Promise.all([
                        DeLongApi.get(`/api/admin/properties/${propertyId}/ai/conversations/${row.id}`),
                        DeLongApi.get(`/api/admin/properties/${propertyId}/ai/conversations/${row.id}/attachments`)
                    ]);
                    if (files.length) addMessage('assistant', `Tệp trong cuộc trò chuyện: ${files.map(x => x.fileName).join(', ')}`);
                    history.forEach(item => { addMessage(item.role, item.content); if (item.proposal) addProposal(item.proposal); }); historyPanel.hidden = true;
                }); historyList.append(button);
            });
        } catch (error) { historyList.textContent = error.message; }
    }
    drawer.querySelector('[data-ai-history-toggle]')?.addEventListener('click', loadHistory);
    drawer.querySelector('[data-ai-history-close]')?.addEventListener('click', () => { historyPanel.hidden = true; });
    drawer.querySelector('[data-ai-new-chat]')?.addEventListener('click', () => { conversationId = null; lastRequest = ''; pendingAttachments = []; renderAttachments(); resetMessages(); historyPanel.hidden = true; input.focus(); });

    function addMessage(role, content) {
        const node = document.createElement('article'); node.className = `ai-chat-message ${role}`;
        const label = document.createElement('small'); label.textContent = role === 'user' ? 'Bạn' : 'Trợ lý AI';
        const body = document.createElement('div'); body.textContent = content;
        node.append(label, body); messages.append(node); messages.scrollTop = messages.scrollHeight; return node;
    }

    const fieldLabels = {
        roomReference: 'Phòng', rateReference: 'Khung giá', price: 'Giá mới', code: 'Mã voucher',
        description: 'Mô tả', discountPercent: 'Mức giảm', appliesTo: 'Phạm vi áp dụng',
        startsAtUtc: 'Bắt đầu', endsAtUtc: 'Kết thúc', totalUsageLimit: 'Tổng lượt tối đa',
        perCustomerUsageLimit: 'Lượt tối đa mỗi khách', status: 'Trạng thái', name: 'Tên',
        capacity: 'Sức chứa', startTime: 'Giờ bắt đầu', endTime: 'Giờ kết thúc', type: 'Loại',
        weekendPrice: 'Giá cuối tuần', useWeekdayPriceOnWeekend: 'Dùng giá thường cuối tuần',
        surchargePercent: 'Phụ thu', bookingMode: 'Chế độ đặt', note: 'Ghi chú'
    };
    const operationLabels = {
        ConfigureRoomRates: 'Cấu hình giá và giờ cho nhiều phòng', UpdateRoom: 'Sức chứa và giá combo cả ngày',
        CreateRoomRate: 'Thêm khung giá', UpdateVoucher: 'Cập nhật voucher', UpdateSpecialPricingDay: 'Cập nhật ngày đặc biệt',
        UpdateRoomContent: 'Cập nhật nội dung và hướng dẫn phòng',
        UpdateSiteSettings: 'Cập nhật thông tin website và SEO',
        UpdateRoomRate: 'Cập nhật giá phòng', CreateVoucher: 'Tạo voucher',
        CreateRoomWithRates: 'Tạo phòng và bảng giá', CreateSpecialPricingDay: 'Tạo ngày đặc biệt',
        UpdatePricingSettings: 'Cập nhật quy tắc giá'
    };
    function displayValue(key, value) {
        if (value === null || value === undefined) return key.toLowerCase().includes('limit') ? 'Không giới hạn' : '—';
        if (typeof value === 'boolean') return value ? 'Có' : 'Không';
        if (['price', 'weekendPrice', 'fullDayPrice', 'weekendFullDayPrice'].includes(key)) return `${Number(value).toLocaleString('vi-VN')} đ`;
        if (key === 'discountPercent' || key === 'surchargePercent') return `${value}%`;
        if (key === 'appliesTo') return Number(value) === 7 ? 'Tất cả loại đặt' : ({ 1: 'Theo khung giờ', 2: 'Qua đêm', 4: 'Cả ngày' }[Number(value)] || String(value));
        if ((key.endsWith('Utc') || key.toLowerCase().includes('date')) && typeof value === 'string') {
            const date = new Date(value); if (!Number.isNaN(date.getTime())) return date.toLocaleString('vi-VN');
        }
        if (typeof value === 'object') return JSON.stringify(value);
        return ({Active:'Hoạt động', Paused:'Tạm dừng', Draft:'Nháp', FullDayOnly:'Chỉ combo cả ngày',
            Normal:'Cho phép ca lẻ', Automatic:'Tự động', Weekday:'Ngày thường', Weekend:'Cuối tuần',
            TimeSlot:'Theo khung giờ', Overnight:'Qua đêm', Nightly:'Theo đêm'})[value] || String(value);
    }
    function appendRows(table, payload, prefix = '') {
        Object.entries(payload || {}).forEach(([key, value]) => {
            if (key === 'operations') return;
            if (Array.isArray(value) || (value && typeof value === 'object')) {
                appendRows(table, Array.isArray(value) ? Object.fromEntries(value.map((item, index) => [`${index + 1}`, item])) : value, `${prefix}${fieldLabels[key] || key} · `);
                return;
            }
            const row = document.createElement('tr');
            const label = document.createElement('th'); label.scope = 'row'; label.textContent = `${prefix}${fieldLabels[key] || key}`;
            const data = document.createElement('td'); data.textContent = displayValue(key, value);
            row.append(label, data); table.append(row);
        });
    }
    function createPayloadTable(payload) {
        const wrap = document.createElement('div'); wrap.className = 'ai-proposal-table-wrap';
        const table = document.createElement('table'); table.className = 'ai-proposal-table';
        const body = document.createElement('tbody'); appendRows(body, payload); table.append(body); wrap.append(table);
        return wrap;
    }
    Object.assign(fieldLabels, {
        fullDayPrice:'Giá cả ngày', weekendFullDayPrice:'Giá cả ngày cuối tuần', fullDayPricingEnabled:'Bật giá cả ngày',
        useWeekdayFullDayPriceOnWeekend:'Cả ngày cuối tuần dùng giá ngày thường', sortOrder:'Thứ tự',
        threeSlotDiscountEnabled:'Bật giảm giá nhiều khung', threeSlotCount:'Số khung áp dụng',
        threeSlotDiscountPercent:'Mức giảm combo (%)', weekendDayMask:'Ngày cuối tuần (mã cấu hình)',
        isActive:'Đang hoạt động', allowThreeSlotCombo:'Cho phép combo nhiều khung', basePriceProfile:'Bảng giá cơ sở',
        startDate:'Từ ngày', endDate:'Đến ngày', category:'Loại ngày', rates:'Khung giá',
        siteName:'Tên website', tagline:'Thông điệp', address:'Địa chỉ', phone:'Điện thoại', email:'Email công khai',
        facebookUrl:'Facebook', zaloUrl:'Zalo', googleMapsUrl:'Google Maps', coverImageUrl:'Ảnh bìa', logoUrl:'Logo',
        faviconUrl:'Favicon', ogImageUrl:'Ảnh chia sẻ', metaTitle:'Tiêu đề SEO', metaDescription:'Mô tả SEO',
        canonicalBaseUrl:'Địa chỉ website chuẩn', ogTitle:'Tiêu đề chia sẻ', ogDescription:'Mô tả chia sẻ', robotsIndex:'Cho máy tìm kiếm lập chỉ mục'
        ,slug:'Đường dẫn phòng', shortDescription:'Mô tả ngắn', descriptionHtml:'Nội dung phòng', guestGuideHtml:'Hướng dẫn check-in', isPublished:'Đang công khai'
    });
    function createPreview(payload) {
        if (!Array.isArray(payload?.preparedChanges)) return createPayloadTable(payload);
        const container = document.createElement('div');
        payload.preparedChanges.forEach(change => {
            const section = document.createElement('section'); section.className = 'ai-proposal-operation';
            const title = document.createElement('strong'); title.textContent = change.target;
            const table = document.createElement('table'); table.className = 'ai-proposal-table';
            const head = document.createElement('thead'); const header = document.createElement('tr');
            ['Trường', 'Hiện tại', 'Sau khi áp dụng'].forEach(label => { const cell = document.createElement('th'); cell.textContent = label; header.append(cell); });
            head.append(header); table.append(head);
            const body = document.createElement('tbody'); let count = 0;
            Object.entries(change.after || {}).forEach(([key, value]) => {
                if (change.kind !== 'CreateRate' && JSON.stringify(value) === JSON.stringify(change.before?.[key])) return;
                const row = document.createElement('tr');
                [fieldLabels[key] || key, change.kind === 'CreateRate' ? '—' : displayValue(key, change.before?.[key]), displayValue(key, value)].forEach(text => {
                    const cell = document.createElement('td'); cell.textContent = text; row.append(cell);
                });
                body.append(row); count++;
            });
            if (!count) { const row = document.createElement('tr'); const cell = document.createElement('td'); cell.colSpan = 3; cell.textContent = 'Cấu hình đã đúng; không thay đổi.'; row.append(cell); body.append(row); }
            table.append(body); const wrap = document.createElement('div'); wrap.className = 'ai-proposal-table-wrap';
            wrap.append(table); section.append(title, wrap); container.append(section);
        });
        return container;
    }
    function addProposal(proposal) {
        const card = document.createElement('section'); card.className = 'ai-proposal-card';
        const title = document.createElement('strong'); title.textContent = 'Bản xem trước thay đổi';
        const summary = document.createElement('p'); summary.textContent = proposal.summary;
        const actions = document.createElement('div'); actions.className = 'ai-proposal-actions';
        const reject = document.createElement('button'); reject.type = 'button'; reject.className = 'btn btn-light'; reject.textContent = 'Từ chối';
        const apply = document.createElement('button'); apply.type = 'button'; apply.className = 'btn btn-primary'; apply.textContent = 'Xác nhận áp dụng';
        card.append(title, summary);
        const operations = proposal.payload?.operations;
        if (Array.isArray(operations)) operations.forEach((operation, index) => {
            const section = document.createElement('div'); section.className = 'ai-proposal-operation';
            const heading = document.createElement('strong'); heading.textContent = `${index + 1}. ${operationLabels[operation.type] || operation.type}`;
            section.append(heading, createPreview(operation.payload)); card.append(section);
        });
        else card.append(createPreview(proposal.payload));
        const isPending = proposal.status === 'Pending' || proposal.status === 0;
        if (isPending) actions.append(reject, apply);
        else actions.textContent = ({ Applied:'Đã áp dụng.', Rejected:'Đã từ chối.', Expired:'Đã hết hạn.', Failed:'Áp dụng thất bại.' }[proposal.status]
            || ({ 1:'Đã áp dụng.', 2:'Đã từ chối.', 3:'Đã hết hạn.', 4:'Áp dụng thất bại.' }[proposal.status]) || 'Đã xử lý.');
        card.append(actions); messages.append(card); messages.scrollTop = messages.scrollHeight;
        const handle = async (action, button) => {
            apply.disabled = reject.disabled = true; button.textContent = action === 'apply' ? 'Đang áp dụng...' : 'Đang xử lý...';
            try { await DeLongApi.post(`/api/admin/properties/${propertyId}/ai/proposals/${proposal.id}/${action}`, {}); card.classList.add(action === 'apply' ? 'applied' : 'rejected'); actions.textContent = action === 'apply' ? 'Đã áp dụng thành công.' : 'Đã từ chối.'; }
            catch (error) { addMessage('assistant', error.message); actions.textContent = 'Chưa áp dụng được. Hãy yêu cầu tạo preview mới để kiểm tra dữ liệu hiện tại.'; }
        };
        if (isPending) {
            apply.addEventListener('click', () => handle('apply', apply));
            reject.addEventListener('click', () => handle('reject', reject));
        }
    }
    form.addEventListener('submit', async event => {
        event.preventDefault(); const value = input.value.trim(); if (value.length < 2 || submit.disabled) return;
        addMessage('user', value); input.value = ''; submit.disabled = true; submit.textContent = 'Đang nghĩ...';
        try {
            await ensureConversation();
            const retry = /^(retry|thử lại|thu lai|làm lại|lam lai)$/i.test(value);
            if (!retry) lastRequest = value;
            const attachmentIds = pendingAttachments.map(x => x.id);
            const result = await DeLongApi.post(`/api/admin/properties/${propertyId}/ai/chat`, { conversationId, message: value, attachmentIds });
            conversationId = result.conversationId; addMessage('assistant', result.message); if (result.proposal) addProposal(result.proposal);
            pendingAttachments = []; renderAttachments();
            if (result.canRetry) addRetry();
        } catch (error) { addMessage('assistant', error.status === 429 ? 'Bạn gửi hơi nhanh. Vui lòng chờ một chút rồi thử lại.' : error.message); addRetry(); }
        finally { submit.disabled = false; submit.textContent = 'Gửi'; input.focus(); }
    });
    function addRetry() {
        const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-light';
        button.textContent = 'Thử lại yêu cầu vừa rồi';
        button.addEventListener('click', () => {
            if (submit.disabled) return;
            input.value = lastRequest || 'Thử lại'; button.remove(); form.requestSubmit();
        });
        messages.append(button); messages.scrollTop = messages.scrollHeight;
    }
    input.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); form.requestSubmit(); } });
})();
