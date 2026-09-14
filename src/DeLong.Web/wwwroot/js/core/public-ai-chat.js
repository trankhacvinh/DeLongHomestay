(() => {
    const drawer = document.querySelector('[data-public-ai-drawer]');
    if (!drawer) return;
    const backdrop = document.querySelector('[data-public-ai-backdrop]');
    const messages = drawer.querySelector('[data-public-ai-messages]');
    const form = drawer.querySelector('[data-public-ai-form]');
    const input = drawer.querySelector('[data-public-ai-input]');
    const submit = form.querySelector('button[type="submit"]');
    const scope = drawer.dataset.siteSlug || 'default';
    const draftStorageKey = `delong.publicAiDraft.${scope}`;
    const historyStorageKey = `delong.publicAiHistory.${scope}`;
    const currentStorageKey = `delong.publicAiCurrent.${scope}`;
    const welcome = 'Xin chào! Tôi có thể giúp bạn tìm hiểu phòng, tiện nghi, giá và chính sách của cơ sở.';
    const storageGet = key => { try { return localStorage.getItem(key); } catch { return null; } };
    const storageSet = (key, value) => { try { localStorage.setItem(key, value); } catch { /* Chat vẫn hoạt động khi bộ nhớ trình duyệt bị khóa/đầy. */ } };
    const storageRemove = key => { try { localStorage.removeItem(key); } catch { /* no-op */ } };
    let currentId = storageGet(currentStorageKey) || '';
    const readHistory = () => {
        try {
            const cutoff = Date.now() - 30 * 86400000;
            return (JSON.parse(storageGet(historyStorageKey) || '[]') || [])
                .filter(item => item && item.id && Number(item.updatedAt) >= cutoff).slice(0, 10);
        } catch { return []; }
    };
    const writeHistory = value => storageSet(historyStorageKey, JSON.stringify(value.slice(0, 10)));
    const ensureConversation = () => {
        if (currentId) return currentId;
        currentId = globalThis.crypto?.randomUUID?.() || `chat-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        storageSet(currentStorageKey, currentId);
        return currentId;
    };
    const persistMessage = message => {
        const id = ensureConversation();
        const history = readHistory();
        let conversation = history.find(item => item.id === id);
        if (!conversation) {
            conversation = { id, title: message.role === 'user' ? message.text.slice(0, 60) : 'Cuộc trò chuyện mới', updatedAt: Date.now(), messages: [] };
            history.unshift(conversation);
        }
        conversation.messages.push(message);
        conversation.messages = conversation.messages.slice(-30);
        if (message.role === 'user' && conversation.title === 'Cuộc trò chuyện mới') conversation.title = message.text.slice(0, 60);
        conversation.updatedAt = Date.now();
        writeHistory([conversation, ...history.filter(item => item.id !== id)]);
    };
    const append = (text, role, action, draft, persist = true, suggestions = null) => {
        const node = document.createElement('div');
        node.className = `public-ai-message ${role}`;
        const copy = document.createElement('div');
        copy.textContent = text;
        node.appendChild(copy);
        if (draft) {
            const table = document.createElement('dl');
            table.className = 'public-ai-draft';
            const rows = [
                ['Phòng', draft.room], ['Ngày', draft.date],
                ['Khung giờ', (draft.timeSlots || []).join(', ')],
                ['Số khách', String(draft.guestCount || '')],
                ['Tiền phòng', Number(draft.roomAmount || 0).toLocaleString('vi-VN') + 'đ'],
                ['Phụ thu', Number(draft.surchargeAmount || 0).toLocaleString('vi-VN') + 'đ'],
                ['Tổng tạm tính', Number(draft.totalAmount || 0).toLocaleString('vi-VN') + 'đ']
            ];
            if (draft.voucherCode) rows.push(['Voucher', draft.voucherCode + ' (sẽ kiểm tra lại trong form)']);
            rows.forEach(([label, value]) => {
                const dt = document.createElement('dt'); dt.textContent = label;
                const dd = document.createElement('dd'); dd.textContent = value;
                table.append(dt, dd);
            });
            node.appendChild(table);
        }
        if (action?.url && action?.label && action.url.startsWith('/')) {
            const link = document.createElement('a');
            link.className = 'public-ai-action';
            link.href = action.url;
            link.textContent = action.label;
            node.appendChild(link);
        }
        if (Array.isArray(suggestions) && suggestions.length) {
            const choices = document.createElement('div');
            choices.className = 'public-ai-suggestions';
            suggestions.forEach(suggestion => {
                const button = document.createElement('button');
                button.type = 'button';
                button.textContent = suggestion.label;
                button.addEventListener('click', () => {
                    if (submit.disabled) return;
                    input.value = suggestion.prompt;
                    form.requestSubmit();
                });
                choices.appendChild(button);
            });
            node.appendChild(choices);
        }
        messages.appendChild(node);
        messages.scrollTop = messages.scrollHeight;
        if (persist) persistMessage({ text, role, action: action || null, draft: draft || null, suggestions: suggestions || null });
    };
    const renderConversation = conversation => {
        messages.replaceChildren();
        if (!conversation?.messages?.length) append(welcome, 'assistant', null, null, false);
        else conversation.messages.forEach(message => append(message.text, message.role, message.action, message.draft, false, message.suggestions));
    };
    const historyPanel = drawer.querySelector('[data-public-ai-history-panel]');
    const historyList = drawer.querySelector('[data-public-ai-history-list]');
    const renderHistory = () => {
        historyList.replaceChildren();
        const history = readHistory();
        if (!history.length) { const empty = document.createElement('p'); empty.textContent = 'Chưa có cuộc trò chuyện nào.'; historyList.appendChild(empty); return; }
        history.forEach(conversation => {
            const row = document.createElement('div');
            const openButton = document.createElement('button'); openButton.type = 'button'; openButton.textContent = conversation.title || 'Cuộc trò chuyện';
            openButton.addEventListener('click', () => { currentId = conversation.id; storageSet(currentStorageKey, currentId); renderConversation(conversation); historyPanel.hidden = true; });
            const removeButton = document.createElement('button'); removeButton.type = 'button'; removeButton.textContent = 'Xóa'; removeButton.className = 'remove';
            removeButton.addEventListener('click', () => { writeHistory(readHistory().filter(item => item.id !== conversation.id)); if (currentId === conversation.id) startNew(); renderHistory(); });
            row.append(openButton, removeButton); historyList.appendChild(row);
        });
    };
    const startNew = () => {
        currentId = '';
        storageRemove(currentStorageKey);
        storageRemove(draftStorageKey);
        renderConversation(null);
        historyPanel.hidden = true;
    };
    const open = () => { drawer.hidden = false; backdrop.hidden = false; document.body.classList.add('modal-open'); input.focus(); };
    const close = () => { drawer.hidden = true; backdrop.hidden = true; document.body.classList.remove('modal-open'); };
    const launch = document.querySelector('[data-public-ai-open]');
    launch.addEventListener('click', open);
    drawer.querySelector('[data-public-ai-close]').addEventListener('click', close);
    drawer.querySelector('[data-public-ai-new]').addEventListener('click', startNew);
    drawer.querySelector('[data-public-ai-history]').addEventListener('click', () => { renderHistory(); historyPanel.hidden = !historyPanel.hidden; });
    drawer.querySelector('[data-public-ai-history-close]').addEventListener('click', () => { historyPanel.hidden = true; });
    backdrop.addEventListener('click', close);
    form.addEventListener('submit', async event => {
        event.preventDefault();
        const text = input.value.trim();
        if (text.length < 2 || submit.disabled) return;
        append(text, 'user'); input.value = ''; submit.disabled = true;
        try {
            const slug = encodeURIComponent(drawer.dataset.siteSlug || '');
            const response = await DeLongApi.post(`/api/public/ai/chat?siteSlug=${slug}`, {
                message: text,
                draftToken: storageGet(draftStorageKey)
            });
            if (response.draftToken) storageSet(draftStorageKey, response.draftToken);
            append(response.message, 'assistant', response.action, response.draft, true, response.suggestions);
        } catch (error) { append(error.message || 'Trợ lý đang gián đoạn. Vui lòng thử lại sau.', 'assistant'); }
        finally { submit.disabled = false; input.focus(); }
    });
    const slug = encodeURIComponent(drawer.dataset.siteSlug || '');
    const initialConversation = readHistory().find(item => item.id === currentId);
    if (initialConversation) renderConversation(initialConversation);
    DeLongApi.get(`/api/public/ai/status?siteSlug=${slug}`).then(status => { launch.hidden = !status.enabled; }).catch(() => { launch.hidden = true; });
})();
