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
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }
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
        // The public visual editor intentionally exposes only the common controls for global
        // BranchGrid/RoomGrid blocks. Preserve their advanced source selections (manual rooms,
        // per-property quotas and selected branches) when a visual edit updates the common fields.
        const rawPut = global.DeLongApi.put;
        global.DeLongApi.put = async (url, data) => {
            const match = /^\/api\/admin\/site\/global\/sections\/([0-9a-f-]+)$/i.exec(url || '');
            if (match && data?.contentJson && (data.type === 'BranchGrid' || data.type === 'RoomGrid')) {
                try {
                    const current = await global.DeLongApi.get('/api/admin/site/global/');
                    const section = (current?.sections || []).find(item => String(item.id).toLowerCase() === match[1].toLowerCase());
                    if (section) {
                        const previous = JSON.parse(section.contentJson || '{}');
                        const next = JSON.parse(data.contentJson || '{}');
                        if (data.type === 'BranchGrid') next.propertyIds = Array.isArray(previous.propertyIds) ? previous.propertyIds : [];
                        if (data.type === 'RoomGrid') {
                            next.roomIds = Array.isArray(previous.roomIds) ? previous.roomIds : [];
                            next.propertyQuotas = previous.propertyQuotas && typeof previous.propertyQuotas === 'object' && !Array.isArray(previous.propertyQuotas)
                                ? previous.propertyQuotas
                                : {};
                        }
                        data = Object.assign({}, data, { contentJson: JSON.stringify(next) });
                    }
                } catch {
                    // If the preservation lookup fails, let the normal PUT surface the actual
                    // authorization/validation error rather than breaking all visual edits here.
                }
            }
            return rawPut(url, data);
        };

        if (!document.querySelector('script[data-public-visual-editor]')) {
            const script = document.createElement('script');
            script.src = '/js/pages/public-visual-editor.js?v=20260817-2';
            script.defer = true;
            script.dataset.publicVisualEditor = 'true';
            document.head.appendChild(script);
        }
    }
})(window);
