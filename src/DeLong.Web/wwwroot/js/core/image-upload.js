(function (global) {
    function profile(url) {
        const path = new URL(url, global.location.origin).pathname;
        if (/\/identity-documents\/(front|back|second-front|second-back)$/.test(path)) return { maxEdge: 2000, quality: 0.9 };
        if (/\/assets\/[^/]+$|\/content\/images$|\/media\/upload$/.test(path)) return { maxEdge: 2560, quality: 0.85 };
        return null;
    }

    async function decode(file) {
        if (global.createImageBitmap) {
            try { return await global.createImageBitmap(file, { imageOrientation: 'from-image' }); }
            catch { /* Older browsers can still decode supported images through an image element. */ }
        }
        const url = URL.createObjectURL(file);
        try {
            return await new Promise((resolve, reject) => {
                const image = new Image();
                image.onload = () => resolve(image);
                image.onerror = () => reject(new Error('Không thể đọc ảnh. Vui lòng chọn ảnh JPG, PNG hoặc WebP khác.'));
                image.src = url;
            });
        } finally { URL.revokeObjectURL(url); }
    }

    async function optimize(file, settings) {
        if (!/^(image\/(jpeg|png|webp))$/i.test(file.type)) return file;
        const image = await decode(file);
        const canvas = document.createElement('canvas');
        try {
            const width = image.naturalWidth || image.width;
            const height = image.naturalHeight || image.height;
            if (!width || !height) throw new Error('Ảnh không có kích thước hợp lệ.');
            const scale = Math.min(1, settings.maxEdge / Math.max(width, height));
            canvas.width = Math.max(1, Math.round(width * scale));
            canvas.height = Math.max(1, Math.round(height * scale));
            const context = canvas.getContext('2d');
            if (!context) throw new Error('Trình duyệt không thể xử lý ảnh. Vui lòng thử lại.');
            context.drawImage(image, 0, 0, canvas.width, canvas.height);
            // Keep transparency for logos and other PNG assets.
            const type = file.type === 'image/png' ? 'image/png' : 'image/jpeg';
            const blob = await new Promise(resolve => canvas.toBlob(resolve, type, settings.quality));
            if (!blob) throw new Error('Không thể nén ảnh. Vui lòng thử ảnh khác.');
            if (scale === 1 && blob.size >= file.size) return file;
            const name = file.name.replace(/\.[^.]+$/, '') + (blob.type === 'image/png' ? '.png' : '.jpg');
            return new File([blob], name, { type: blob.type, lastModified: file.lastModified });
        } finally {
            image.close?.();
            canvas.width = canvas.height = 1;
        }
    }

    const preparedFiles = new WeakMap();
    let processing = Promise.resolve();
    function prepareFile(url, file) {
        const settings = profile(url);
        if (!settings) return Promise.resolve(file);
        const key = `${settings.maxEdge}:${settings.quality}`;
        let entries = preparedFiles.get(file);
        if (!entries) { entries = new Map(); preparedFiles.set(file, entries); }
        if (!entries.has(key)) {
            const task = processing.then(() => optimize(file, settings));
            processing = task.catch(() => {});
            entries.set(key, task);
            task.catch(() => entries.delete(key));
        }
        return entries.get(key);
    }

    async function prepareForm(url, form) {
        const settings = profile(url);
        if (!settings) return form;
        const prepared = new FormData();
        // Process sequentially so several large phone photos do not fill memory together.
        for (const [key, value] of form.entries()) {
            if (value instanceof File) {
                const file = await prepareFile(url, value);
                prepared.append(key, file, file.name);
            } else prepared.append(key, value);
        }
        return prepared;
    }

    global.DeLongImageUpload = { prepareForm, prepareFile };
})(window);
