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
})(window);
