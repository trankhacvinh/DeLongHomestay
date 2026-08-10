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

        const contentType = response.headers.get('content-type') || '';
        const payload = contentType.includes('application/json') ? await response.json() : await response.text();

        if (!response.ok) {
            const error = new Error(payload?.detail || payload?.title || payload || `HTTP ${response.status}`);
            error.status = response.status;
            error.problem = payload;
            throw error;
        }
        return payload;
    }

    global.DeLongApi = {
        get: (url) => request(url),
        post: (url, data) => request(url, { method: 'POST', body: JSON.stringify(data) }),
        put: (url, data) => request(url, { method: 'PUT', body: JSON.stringify(data) }),
        patch: (url, data) => request(url, { method: 'PATCH', body: JSON.stringify(data) }),
        delete: (url) => request(url, { method: 'DELETE' })
    };
})(window);
