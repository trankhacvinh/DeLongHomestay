(function (global) {
    function profile(url) {
        const path = new URL(url, global.location.origin).pathname;
        if (/\/identity-documents\/(front|back|second-front|second-back)$/.test(path)) return { maxEdge: 1600, quality: 0.9, opaque: true, targetBytes: 250 * 1024 };
        if (/\/assets\/[^/]+$|\/content\/images$|\/media\/upload$/.test(path)) return { maxEdge: 2560, quality: 0.85 };
        return null;
    }

    const processingMessage = 'Xử lý ảnh mất quá lâu. Vui lòng chụp lại hoặc chọn ảnh khác.';
    function check(job) { if (job.stopped) throw new Error(processingMessage); }

    async function dimensions(file) {
        const buffer = await file.slice(0, 256 * 1024).arrayBuffer();
        const data = new DataView(buffer);
        const length = data.byteLength;
        if (length >= 24 && data.getUint32(0) === 0x89504e47 && data.getUint32(4) === 0x0d0a1a0a)
            return { width: data.getUint32(16), height: data.getUint32(20) };
        if (length >= 2 && data.getUint16(0) === 0xffd8) {
            let offset = 2, orientation = 1, size = null;
            while (offset + 4 <= length) {
                if (data.getUint8(offset++) !== 0xff) break;
                while (offset < length && data.getUint8(offset) === 0xff) offset++;
                if (offset + 3 > length) break;
                const marker = data.getUint8(offset++);
                if (marker === 0xda || marker === 0xd9) break;
                if (marker === 0x01 || marker >= 0xd0 && marker <= 0xd7) continue;
                const count = data.getUint16(offset), end = offset + count;
                if (count < 2 || end > length) break;
                if ([0xc0,0xc1,0xc2,0xc3,0xc5,0xc6,0xc7,0xc9,0xca,0xcb,0xcd,0xce,0xcf].includes(marker) && count >= 7)
                    size = { width: data.getUint16(offset + 5), height: data.getUint16(offset + 3) };
                if (marker === 0xe1 && count >= 16 && data.getUint32(offset + 2) === 0x45786966 && data.getUint16(offset + 6) === 0) {
                    const base = offset + 8, little = data.getUint16(base) === 0x4949;
                    if (data.getUint16(base + 2, little) === 42) {
                        const directory = base + data.getUint32(base + 4, little);
                        if (directory >= base && directory + 2 <= end) {
                            const entries = data.getUint16(directory, little);
                            for (let i = 0; i < entries; i++) {
                                const entry = directory + 2 + i * 12;
                                if (entry + 12 > end) break;
                                if (data.getUint16(entry, little) === 0x0112 && data.getUint16(entry + 2, little) === 3 && data.getUint32(entry + 4, little) === 1)
                                    orientation = data.getUint16(entry + 8, little);
                            }
                        }
                    }
                }
                offset = end;
            }
            if (size && orientation >= 5 && orientation <= 8) return { width: size.height, height: size.width };
            return size;
        }
        if (length >= 30 && data.getUint32(0) === 0x52494646 && data.getUint32(8) === 0x57454250) {
            const format = data.getUint32(12);
            if (format === 0x56503858) return { width: 1 + data.getUint8(24) + (data.getUint8(25) << 8) + (data.getUint8(26) << 16), height: 1 + data.getUint8(27) + (data.getUint8(28) << 8) + (data.getUint8(29) << 16) };
            if (format === 0x56503820 && data.getUint8(23) === 0x9d && data.getUint8(24) === 0x01 && data.getUint8(25) === 0x2a)
                return { width: data.getUint16(26, true) & 0x3fff, height: data.getUint16(28, true) & 0x3fff };
            if (format === 0x5650384c && data.getUint8(20) === 0x2f) {
                const bits = data.getUint32(21, true); return { width: 1 + (bits & 0x3fff), height: 1 + ((bits >>> 14) & 0x3fff) };
            }
        }
        return null;
    }

    async function decode(file, settings, job, size) {
        check(job);
        if (size && (!size.width || !size.height || size.width * size.height > 80_000_000)) throw new Error('Ảnh quá lớn. Vui lòng chụp ở độ phân giải thấp hơn.');
        if (global.createImageBitmap) {
            try {
                const options = { imageOrientation: 'from-image' };
                if (size) {
                    const scale = Math.min(1, settings.maxEdge / Math.max(size.width, size.height));
                    Object.assign(options, { resizeWidth: Math.max(1, Math.round(size.width * scale)), resizeHeight: Math.max(1, Math.round(size.height * scale)), resizeQuality: 'high' });
                }
                const bitmap = await global.createImageBitmap(file, options);
                if (job.stopped) { bitmap.close?.(); check(job); }
                return bitmap;
            } catch (error) { check(job); }
        }
        const url = URL.createObjectURL(file);
        let image;
        const cleanup = () => { if (image) { image.onload = image.onerror = null; image.src = ''; } URL.revokeObjectURL(url); };
        job.cleanup.add(cleanup);
        try {
            return await new Promise((resolve, reject) => {
                image = new Image(); image.decoding = 'async';
                image.onload = () => resolve(image);
                image.onerror = () => reject(new Error('Không thể đọc ảnh. Vui lòng chọn ảnh JPG, PNG hoặc WebP khác.'));
                image.src = url;
            });
        } finally { job.cleanup.delete(cleanup); URL.revokeObjectURL(url); }
    }

    async function optimize(file, settings, job) {
        let sourceType = file.type.toLowerCase();
        if (sourceType === 'image/jpg') sourceType = 'image/jpeg';
        if (!sourceType) sourceType = /\.jpe?g$/i.test(file.name) ? 'image/jpeg' : /\.png$/i.test(file.name) ? 'image/png' : /\.webp$/i.test(file.name) ? 'image/webp' : '';
        if (!/^(image\/(jpeg|png|webp))$/i.test(sourceType)) {
            if (settings.opaque) throw new Error('Vui lòng chọn ảnh JPG, PNG hoặc WebP.');
            return file;
        }
        const sourceSize = await dimensions(file); check(job);
        const image = await decode(file, settings, job, sourceSize); check(job);
        const canvas = document.createElement('canvas');
        const cleanup = () => { image.close?.(); canvas.width = canvas.height = 1; };
        job.cleanup.add(cleanup);
        try {
            const width = image.naturalWidth || image.width;
            const height = image.naturalHeight || image.height;
            if (!width || !height) throw new Error('Ảnh không có kích thước hợp lệ.');
            const scale = Math.min(1, settings.maxEdge / Math.max(width, height));
            canvas.width = Math.max(1, Math.round(width * scale));
            canvas.height = Math.max(1, Math.round(height * scale));
            const context = canvas.getContext('2d', { alpha: !settings.opaque });
            if (!context) throw new Error('Trình duyệt không thể xử lý ảnh. Vui lòng thử lại.');
            if (settings.opaque) { context.fillStyle = '#fff'; context.fillRect(0, 0, canvas.width, canvas.height); }
            context.drawImage(image, 0, 0, canvas.width, canvas.height);
            // Keep transparency for logos and other PNG assets.
            const type = sourceType === 'image/png' && !settings.opaque ? 'image/png' : 'image/jpeg';
            let blob = await new Promise(resolve => canvas.toBlob(resolve, type, settings.quality));
            if (settings.targetBytes && blob?.type === 'image/jpeg' && blob.size > settings.targetBytes) {
                // Encode from the same canvas: keep every pixel of document detail (1600px reads a CCCD clearly).
                // Stay on JPEG: Chrome's WebP encoder is ~5-20x slower than JPEG on phones
                // (seconds per photo), and the server re-encodes identity images to WebP anyway.
                for (const quality of [0.84, 0.78]) {
                    const candidate = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
                    check(job);
                    if (candidate?.type !== 'image/jpeg') break;
                    if (candidate.size < blob.size) blob = candidate;
                    if (blob.size <= settings.targetBytes) break;
                }
            }
            check(job);
            if (!blob) throw new Error('Không thể nén ảnh. Vui lòng thử ảnh khác.');
            const originalFits = !sourceSize || Math.max(sourceSize.width, sourceSize.height) <= settings.maxEdge;
            if (originalFits && scale === 1 && blob.size >= file.size && type === file.type) return file;
            const name = file.name.replace(/\.[^.]+$/, '') + (blob.type === 'image/png' ? '.png' : blob.type === 'image/webp' ? '.webp' : '.jpg');
            return new File([blob], name, { type: blob.type, lastModified: file.lastModified });
        } finally {
            job.cleanup.delete(cleanup); cleanup();
        }
    }

    const preparedFiles = new WeakMap();
    let processing = Promise.resolve();
    function prepareFile(url, file) {
        const settings = profile(url);
        if (!settings) return Promise.resolve(file);
        const key = `${settings.maxEdge}:${settings.quality}:${!!settings.opaque}`;
        let entries = preparedFiles.get(file);
        if (!entries) { entries = new Map(); preparedFiles.set(file, entries); }
        if (!entries.has(key)) {
            const job = { stopped: false, cleanup: new Set() };
            let rejectCancelled;
            const cancelled = new Promise((_, reject) => { rejectCancelled = reject; });
            const task = processing.then(() => {
                check(job);
                let timer;
                const timeout = new Promise((_, reject) => { timer = setTimeout(() => {
                    job.stopped = true; for (const cleanup of job.cleanup) cleanup(); job.cleanup.clear(); reject(new Error(processingMessage));
                }, 20000); });
                return Promise.race([optimize(file, settings, job), timeout, cancelled]).finally(() => clearTimeout(timer));
            });
            processing = task.catch(() => {});
            const result = Promise.race([task, cancelled]);
            result.cancel = () => { if(job.stopped)return;job.stopped=true;for(const cleanup of job.cleanup)cleanup();job.cleanup.clear();rejectCancelled(new Error('Ảnh đã được thay hoặc bỏ.')); };
            entries.set(key, result);
            result.catch(() => { if(entries.get(key)===result)entries.delete(key); });
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

    function discardFile(file) { const entries=preparedFiles.get(file);if(!entries)return;for(const result of entries.values())result.cancel?.();preparedFiles.delete(file); }
    global.DeLongImageUpload = { prepareForm, prepareFile, discardFile };
})(window);
