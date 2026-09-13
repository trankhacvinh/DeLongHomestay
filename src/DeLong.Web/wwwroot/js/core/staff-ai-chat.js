(() => {
    const drawer = document.querySelector('[data-staff-ai-drawer]');
    const toggle = document.querySelector('[data-ai-chat-toggle]');
    if (!drawer || !toggle || !window.DeLongApi) return;
    const backdrop = document.querySelector('[data-staff-ai-backdrop]');
    const messages = drawer.querySelector('[data-staff-ai-messages]');
    const form = drawer.querySelector('[data-staff-ai-form]');
    const input = drawer.querySelector('[data-staff-ai-input]');
    const submit = form.querySelector('button[type="submit"]');
    const propertyId = drawer.dataset.propertyId;
    let contextPeriod = null;

    const open = () => { drawer.hidden = false; backdrop.hidden = false; requestAnimationFrame(() => { drawer.classList.add('open'); backdrop.classList.add('open'); input.focus(); }); toggle.setAttribute('aria-expanded', 'true'); };
    const close = () => { drawer.classList.remove('open'); backdrop.classList.remove('open'); toggle.setAttribute('aria-expanded', 'false'); setTimeout(() => { drawer.hidden = true; backdrop.hidden = true; }, 180); };
    const addMessage = (role, content, response) => {
        const node = document.createElement('article'); node.className = `ai-chat-message ${role}`;
        const label = document.createElement('small'); label.textContent = role === 'user' ? 'Bạn' : 'Trợ lý vận hành';
        const body = document.createElement('div'); body.textContent = content; node.append(label, body);
        (response?.tables || []).forEach(item => {
            const section = document.createElement('section'); section.className = 'staff-ai-result';
            const title = document.createElement('strong'); title.textContent = item.title;
            const wrap = document.createElement('div'); wrap.className = 'ai-proposal-table-wrap';
            const table = document.createElement('table'); table.className = 'ai-proposal-table';
            const head = document.createElement('thead'); const header = document.createElement('tr');
            item.columns.forEach(value => { const cell = document.createElement('th'); cell.textContent = value; header.append(cell); }); head.append(header); table.append(head);
            const tbody = document.createElement('tbody');
            if (!item.rows.length) { const row = document.createElement('tr'); const cell = document.createElement('td'); cell.colSpan = item.columns.length; cell.textContent = 'Không có dữ liệu.'; row.append(cell); tbody.append(row); }
            else item.rows.forEach(values => { const row = document.createElement('tr'); values.forEach(value => { const cell = document.createElement('td'); cell.textContent = value; row.append(cell); }); tbody.append(row); });
            table.append(tbody); wrap.append(table); section.append(title, wrap); node.append(section);
        });
        if (response?.period) { const meta = document.createElement('small'); meta.className = 'staff-ai-meta'; meta.textContent = `Kỳ dữ liệu: ${response.period} · ${response.timeZone}`; node.append(meta); }
        messages.append(node); messages.scrollTop = messages.scrollHeight;
    };
    toggle.addEventListener('click', open); drawer.querySelector('[data-staff-ai-close]').addEventListener('click', close); backdrop.addEventListener('click', close);
    drawer.querySelectorAll('[data-staff-ai-prompt]').forEach(button => button.addEventListener('click', () => { input.value = button.dataset.staffAiPrompt; form.requestSubmit(); }));
    form.addEventListener('submit', async event => {
        event.preventDefault(); const text = input.value.trim(); if (text.length < 2 || submit.disabled) return;
        addMessage('user', text); input.value = ''; submit.disabled = true;
        try { const response = await DeLongApi.post(`/api/admin/properties/${propertyId}/staff-ai/chat`, { message: text, contextPeriod }); contextPeriod = response.periodKey || null; addMessage('assistant', response.message, response); }
        catch (error) { addMessage('assistant', error.message || 'Không thể tra cứu lúc này.'); }
        finally { submit.disabled = false; input.focus(); }
    });
})();
