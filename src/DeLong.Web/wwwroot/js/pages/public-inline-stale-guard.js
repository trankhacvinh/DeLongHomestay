(function () {
    if (!document.body?.classList.contains('public-body') || !window.DeLongApi?.put) return;

    const normalizedPath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = normalizedPath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const customPageId = document.querySelector('meta[name="delong-custom-page-id"]')?.content || '';
    const scopeKey = customPageId ? `page:${customPageId}` : (scoped ? decodeURIComponent(scoped[1]) : 'global');
    const pendingKey = `delong:inline:core-action:${scopeKey}`;
    let dirty = false;

    const originalPut = DeLongApi.put;
    DeLongApi.put = async function (url, data) {
        const result = await originalPut(url, data);
        if (/\/sections\/[0-9a-f-]{36}$/i.test(String(url || ''))) dirty = true;
        return result;
    };

    function remember(sectionId, action) {
        try {
            sessionStorage.setItem(pendingKey, JSON.stringify({ sectionId, action }));
            sessionStorage.setItem(`delong:inline:scroll:${scopeKey}`, String(Math.max(0, window.scrollY || 0)));
        } catch { }
    }

    document.addEventListener('click', event => {
        if (!dirty || !document.body.classList.contains('pve-editing')) return;
        const button = event.target.closest('.pve-section-controls [data-action]');
        const action = button?.dataset.action || '';
        if (!button || !['edit', 'duplicate', 'hide'].includes(action)) return;
        const sectionId = button.closest('[data-pve-section-id]')?.dataset.pveSectionId;
        if (!sectionId) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        remember(sectionId, action);
        window.location.reload();
    }, true);

    function replay() {
        let pending = null;
        try { pending = JSON.parse(sessionStorage.getItem(pendingKey) || 'null'); } catch { }
        if (!pending?.sectionId || !pending?.action) return;
        let tries = 0;
        const timer = setInterval(() => {
            tries += 1;
            const root = document.querySelector(`.pve-editable-section[data-pve-section-id="${CSS.escape(String(pending.sectionId))}"]`);
            const button = root?.querySelector(`.pve-section-controls [data-action="${CSS.escape(String(pending.action))}"]`);
            if (button) {
                clearInterval(timer);
                try { sessionStorage.removeItem(pendingKey); } catch { }
                const savedScroll = Number(sessionStorage.getItem(`delong:inline:scroll:${scopeKey}`) || 0);
                try { sessionStorage.removeItem(`delong:inline:scroll:${scopeKey}`); } catch { }
                if (savedScroll > 0) window.scrollTo({ top: savedScroll, behavior: 'auto' });
                button.click();
            } else if (tries > 30) {
                clearInterval(timer);
                try { sessionStorage.removeItem(pendingKey); } catch { }
            }
        }, 100);
    }

    replay();
})();
