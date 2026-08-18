(function (global) {
    async function request(url, options) {
        const config = Object.assign({ method: 'GET', credentials: 'same-origin' }, options || {});
        config.headers = Object.assign({ Accept: 'application/json' }, config.headers || {});

        if (config.body && !(config.body instanceof FormData)) {
            config.headers['Content-Type'] = 'application/json';
        }

        if (!['GET', 'HEAD', 'OPTIONS'].includes(config.method.toUpperCase())) {
            const token = document.querySelector('meta[name="csrf-token"]')?.content;
            if (token) config.headers['X-CSRF-TOKEN'] = token;
        }

        const response = await fetch(url, config);
        if (response.status === 204) return null;

        const contentType = (response.headers.get('content-type') || '').toLowerCase();
        const isJson = contentType.includes('json');
        let payload;
        if (isJson) {
            try { payload = await response.json(); }
            catch { payload = null; }
        } else {
            payload = await response.text();
        }

        if (!response.ok) {
            const objectPayload = payload && typeof payload === 'object' ? payload : null;
            const message = objectPayload?.detail || objectPayload?.title || payload || `HTTP ${response.status}`;
            const error = new Error(message);
            error.status = response.status;
            error.problem = objectPayload;
            throw error;
        }
        return payload;
    }

    global.DeLongApi = {
        get: (url) => request(url),
        post: (url, data, headers) => request(url, { method: 'POST', headers: headers || {}, body: JSON.stringify(data) }),
        postForm: (url, formData) => request(url, { method: 'POST', body: formData }),
        put: (url, data) => request(url, { method: 'PUT', body: JSON.stringify(data) }),
        patch: (url, data) => request(url, { method: 'PATCH', body: JSON.stringify(data) }),
        delete: (url) => request(url, { method: 'DELETE' })
    };

    if (document.body?.classList.contains('public-body')) {
        function addStyle(marker, href) {
            if (document.querySelector(`link[${marker}]`)) return;
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = href;
            link.setAttribute(marker, 'true');
            document.head.appendChild(link);
        }

        function addScript(marker, src) {
            if (document.querySelector(`script[${marker}]`)) return;
            const script = document.createElement('script');
            script.src = src;
            script.async = false;
            script.setAttribute(marker, 'true');
            document.head.appendChild(script);
        }

        addStyle('data-public-row-builder', '/css/public-row-builder.css?v=20260817-2');
        addStyle('data-public-row-responsive', '/css/public-row-responsive.css?v=20260817-1');
        addStyle('data-public-row-responsive-preview', '/css/public-row-responsive-preview.css?v=20260817-1');
        addStyle('data-public-row-templates', '/css/public-row-templates.css?v=20260817-1');
        addStyle('data-public-editor-enhancements', '/css/public-visual-editor-enhancements.css?v=20260817-2');
        addStyle('data-public-global-gallery-picker', '/css/public-global-gallery-picker.css?v=20260817-2');
        addStyle('data-public-contextual-editor', '/css/public-contextual-editor.css?v=20260817-1');
        addStyle('data-public-contextual-shell-rates', '/css/public-contextual-shell-rates.css?v=20260817-2');
        addStyle('data-public-shell-link-picker', '/css/public-shell-link-picker.css?v=20260817-3');
        addStyle('data-public-gallery-studio', '/css/public-gallery-studio.css?v=20260817-1');
        addStyle('data-public-contextual-content-cards', '/css/public-contextual-content-cards.css?v=20260817-1');
        addStyle('data-public-inline-editor', '/css/public-inline-editor.css?v=20260817-1');
        addStyle('data-public-row-inline', '/css/public-row-inline-editor.css?v=20260818-1');
        addStyle('data-public-row-advanced-elements', '/css/public-row-advanced-elements.css?v=20260818-1');

        addScript('data-public-shell-runtime', '/js/pages/public-shell-runtime.js?v=20260817-2');
        addScript('data-public-rich-editor', '/js/pages/public-rich-editor.js?v=20260817-3');
        addScript('data-public-editorial-order', '/js/pages/public-editorial-order.js?v=20260817-2');
        addScript('data-public-visual-editor', '/js/pages/public-visual-editor-v2.js?v=20260817-4');
        addScript('data-public-editor-enhancements', '/js/pages/public-visual-editor-enhancements.js?v=20260817-5');
        addScript('data-public-global-editorial-source', '/js/pages/public-global-editorial-source.js?v=20260817-1');
        addScript('data-public-row-editor', '/js/pages/public-visual-row-builder-v4.js?v=20260817-1');
        addScript('data-public-row-templates', '/js/pages/public-visual-row-templates.js?v=20260817-1');
        addScript('data-public-contextual-editor', '/js/pages/public-contextual-editor.js?v=20260817-1');
        addScript('data-public-contextual-shell-rates', '/js/pages/public-contextual-shell-rates.js?v=20260817-2');
        addScript('data-public-shell-link-picker', '/js/pages/public-shell-link-picker.js?v=20260817-5');
        addScript('data-public-gallery-studio', '/js/pages/public-gallery-studio.js?v=20260817-1');
        addScript('data-public-contextual-content-cards', '/js/pages/public-contextual-content-cards.js?v=20260817-1');
        addScript('data-public-inline-stale-guard', '/js/pages/public-inline-stale-guard.js?v=20260817-1');
        addScript('data-public-row-inline-core', '/js/pages/public-row-inline-core.js?v=20260818-1');
        addScript('data-public-row-inline-ui', '/js/pages/public-row-inline-ui.js?v=20260818-1');
        addScript('data-public-row-inline-bindings', '/js/pages/public-row-inline-bindings.js?v=20260818-1');
        addScript('data-public-row-advanced-elements', '/js/pages/public-row-advanced-elements.js?v=20260818-1');
        addScript('data-public-row-advanced-runtime', '/js/pages/public-row-advanced-runtime.js?v=20260818-1');
        addScript('data-public-inline-editor', '/js/pages/public-inline-editor.js?v=20260817-1');
    }
})(window);