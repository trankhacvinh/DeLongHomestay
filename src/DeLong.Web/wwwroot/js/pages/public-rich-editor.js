(function (global) {
    if (global.DeLongRichEditor) return;

    const QUILL_VERSION = '2.0.3';
    const QUILL_SCRIPT = `https://cdn.jsdelivr.net/npm/quill@${QUILL_VERSION}/dist/quill.js`;
    const QUILL_STYLE = `https://cdn.jsdelivr.net/npm/quill@${QUILL_VERSION}/dist/quill.snow.css`;
    let quillPromise = null;
    let visualContextPromise = null;

    function cleanForVisual(markup) {
        const parser = new DOMParser();
        const doc = parser.parseFromString(`<div>${markup || ''}</div>`, 'text/html');
        doc.querySelectorAll('script,style,iframe,object,embed,link,meta,form').forEach(node => node.remove());
        doc.querySelectorAll('*').forEach(node => {
            [...node.attributes].forEach(attr => {
                const name = attr.name.toLowerCase();
                const value = attr.value.trim().toLowerCase();
                if (name.startsWith('on') || ((name === 'href' || name === 'src') && value.startsWith('javascript:'))) {
                    node.removeAttribute(attr.name);
                }
            });
        });
        return doc.body.firstElementChild?.innerHTML || '';
    }

    function dispatchInput(textarea) {
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
        textarea.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function normalizeFieldContainer(textarea) {
        const label = textarea.parentElement;
        if (!label || label.tagName !== 'LABEL') return;

        // A Quill editor cannot live inside a <label> that still contains the hidden
        // source textarea: clicking Quill triggers the label's default activation and
        // moves focus back to that hidden textarea. Keep the same field styling but
        // turn the wrapper into a neutral div before mounting Quill.
        const container = document.createElement('div');
        for (const attribute of [...label.attributes]) {
            container.setAttribute(attribute.name, attribute.value);
        }
        while (label.firstChild) container.appendChild(label.firstChild);
        label.replaceWith(container);
    }

    function ensureQuillStyle() {
        if (document.querySelector(`link[href="${QUILL_STYLE}"], link[data-delong-quill-style]`)) return;
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = QUILL_STYLE;
        link.dataset.delongQuillStyle = 'true';
        document.head.appendChild(link);
    }

    function ensureQuill() {
        ensureQuillStyle();
        if (global.Quill) return Promise.resolve(global.Quill);
        if (quillPromise) return quillPromise;

        quillPromise = new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${QUILL_SCRIPT}"], script[data-delong-quill-script]`);
            const script = existing || document.createElement('script');
            const done = () => global.Quill ? resolve(global.Quill) : reject(new Error('Quill không khởi tạo được.'));
            const fail = () => reject(new Error('Không thể tải trình soạn thảo Quill.'));

            if (existing) {
                if (global.Quill) return resolve(global.Quill);
                existing.addEventListener('load', done, { once: true });
                existing.addEventListener('error', fail, { once: true });
                return;
            }

            script.src = QUILL_SCRIPT;
            script.async = true;
            script.dataset.delongQuillScript = 'true';
            script.addEventListener('load', done, { once: true });
            script.addEventListener('error', fail, { once: true });
            document.head.appendChild(script);
        });
        return quillPromise;
    }

    function getVisualContext() {
        if (visualContextPromise) return visualContextPromise;
        if (!global.DeLongApi) return Promise.reject(new Error('Không tìm thấy API của Visual Editor.'));
        const normalizedPath = global.location.pathname.replace(/\/+$/, '') || '/';
        const scopedMatch = normalizedPath.match(/^\/h\/([^/]+)/i);
        const siteSlug = scopedMatch ? decodeURIComponent(scopedMatch[1]) : '';
        const url = `/api/admin/site/visual-context${siteSlug ? `?siteSlug=${encodeURIComponent(siteSlug)}` : ''}`;
        visualContextPromise = DeLongApi.get(url).then(context => {
            if (!context?.canEdit) throw new Error('Bạn không có quyền tải ảnh vào nội dung này.');
            return context;
        });
        return visualContextPromise;
    }

    async function uploadPublicAsset(file) {
        const context = await getVisualContext();
        const api = context.scope === 'global'
            ? '/api/admin/site/global'
            : `/api/admin/properties/${context.propertyId}/site`;
        const form = new FormData();
        form.append('file', file);
        const asset = await DeLongApi.postForm(`${api}/assets/section`, form);
        if (!asset?.url) throw new Error('Ảnh đã tải nhưng không nhận được URL.');
        return asset.url;
    }

    function normalizeUploadedUrl(result) {
        if (typeof result === 'string') return result;
        return result?.url || result?.imageUrl || '';
    }

    function enhance(textarea, options) {
        if (!textarea) return null;
        if (textarea._delongRichEditor) return textarea._delongRichEditor;
        textarea.dataset.richEnhanced = '1';
        const opts = options || {};

        normalizeFieldContainer(textarea);

        const shell = document.createElement('div');
        shell.className = 'pve-rich-editor is-loading';
        shell.innerHTML = `
            <div class="pve-rich-modebar">
                <div class="pve-rich-tabs" role="tablist" aria-label="Chế độ soạn thảo">
                    <button type="button" class="active" data-rich-mode="visual">Trực quan</button>
                    <button type="button" data-rich-mode="html">&lt;/&gt; HTML</button>
                </div>
                <small data-rich-status>${opts.helpText || 'Trực quan dùng Quill giống phần Blog trong Admin; HTML dành cho người cần chỉnh mã.'}</small>
            </div>
            <div class="pve-rich-quill" data-rich-quill><div class="pve-rich-loading">Đang tải trình soạn thảo…</div></div>
            <div class="pve-rich-source" data-rich-source></div>`;

        textarea.before(shell);
        const source = shell.querySelector('[data-rich-source]');
        source.appendChild(textarea);
        textarea.classList.add('pve-rich-source-textarea');
        const quillHost = shell.querySelector('[data-rich-quill]');
        const status = shell.querySelector('[data-rich-status]');
        const defaultStatus = status.textContent;
        const imageInput = document.createElement('input');
        imageInput.type = 'file';
        imageInput.accept = 'image/png,image/jpeg,image/webp';
        imageInput.hidden = true;
        shell.appendChild(imageInput);

        let mode = 'visual';
        let quill = null;
        let syncingFromVisual = false;
        let statusTimer = null;
        let destroyed = false;

        function setStatus(message, isError, sticky) {
            clearTimeout(statusTimer);
            status.textContent = message || defaultStatus;
            status.classList.toggle('error', !!isError);
            if (message && !sticky) {
                statusTimer = setTimeout(() => {
                    status.textContent = defaultStatus;
                    status.classList.remove('error');
                }, 2600);
            }
        }

        function syncVisualFromSource() {
            if (!quill) return;
            quill.clipboard.dangerouslyPasteHTML(cleanForVisual(textarea.value), 'silent');
        }

        function syncSourceFromVisual() {
            if (!quill) return;
            syncingFromVisual = true;
            const html = quill.root.innerHTML;
            textarea.value = html === '<p><br></p>' ? '' : html;
            dispatchInput(textarea);
            syncingFromVisual = false;
        }

        async function insertImage(file) {
            if (!file || !quill) return;
            const upload = typeof opts.uploadImage === 'function' ? opts.uploadImage : uploadPublicAsset;
            setStatus('Đang tải ảnh vào nội dung…', false, true);
            try {
                const result = await upload(file);
                const url = normalizeUploadedUrl(result);
                if (!url) throw new Error('Không nhận được URL ảnh sau khi tải.');
                const selection = quill.getSelection(true);
                const index = selection ? selection.index : Math.max(0, quill.getLength() - 1);
                quill.insertEmbed(index, 'image', url, 'user');
                quill.setSelection(index + 1, 0, 'silent');
                syncSourceFromVisual();
                setStatus('Đã chèn ảnh vào nội dung.');
            } catch (error) {
                setStatus(error?.message || 'Không thể chèn ảnh.', true, true);
                if (typeof opts.onError === 'function') opts.onError(error);
            } finally {
                imageInput.value = '';
            }
        }

        function setMode(next) {
            if (!['visual', 'html'].includes(next) || next === mode) return;
            if (next === 'html') syncSourceFromVisual();
            else syncVisualFromSource();
            mode = next;
            shell.classList.toggle('is-html', mode === 'html');
            shell.querySelectorAll('[data-rich-mode]').forEach(button => button.classList.toggle('active', button.dataset.richMode === mode));
            if (mode === 'visual') quill?.focus();
        }

        shell.querySelectorAll('[data-rich-mode]').forEach(button => button.addEventListener('click', () => setMode(button.dataset.richMode)));
        imageInput.addEventListener('change', () => insertImage(imageInput.files?.[0]));
        textarea.addEventListener('input', () => {
            if (syncingFromVisual || mode === 'html') return;
            syncVisualFromSource();
        });

        const instance = {
            shell,
            textarea,
            get quill() { return quill; },
            getMode: () => mode,
            setMode,
            sync: () => mode === 'visual' ? syncSourceFromVisual() : syncVisualFromSource(),
            destroy: () => { destroyed = true; clearTimeout(statusTimer); shell.remove(); textarea._delongRichEditor = null; }
        };
        textarea._delongRichEditor = instance;

        instance.ready = ensureQuill().then(Quill => {
            if (destroyed || !textarea.isConnected || !quillHost.isConnected) return instance;
            quillHost.innerHTML = '';
            const editor = document.createElement('div');
            quillHost.appendChild(editor);
            const inlineTools = opts.allowImages === false ? ['link'] : ['link', 'image'];
            const toolbarHandlers = opts.allowImages === false ? {} : { image: () => imageInput.click() };
            quill = new Quill(editor, {
                theme: 'snow',
                placeholder: opts.placeholder || 'Nhập nội dung…',
                modules: {
                    toolbar: {
                        container: [
                            [{ header: [2, 3, false] }],
                            ['bold', 'italic', 'blockquote'],
                            [{ list: 'ordered' }, { list: 'bullet' }],
                            inlineTools,
                            ['clean']
                        ],
                        handlers: toolbarHandlers
                    }
                }
            });
            syncVisualFromSource();
            quill.on('text-change', () => {
                if (mode === 'visual') syncSourceFromVisual();
            });
            quill.root.addEventListener('pointerdown', () => {
                requestAnimationFrame(() => {
                    if (mode === 'visual' && document.activeElement !== quill.root) quill.focus();
                });
            });
            shell.classList.remove('is-loading');
            if (mode === 'html') shell.classList.add('is-html');
            return instance;
        }).catch(error => {
            shell.classList.remove('is-loading');
            shell.classList.add('is-html');
            mode = 'html';
            shell.querySelectorAll('[data-rich-mode]').forEach(button => button.classList.toggle('active', button.dataset.richMode === 'html'));
            setStatus(`${error?.message || 'Không thể tải Quill.'} Bạn vẫn có thể chỉnh HTML.`, true, true);
            return instance;
        });

        return instance;
    }

    function enhanceAll(root, selector, options) {
        (root || document).querySelectorAll(selector || 'textarea[data-rich-text]').forEach(textarea => enhance(textarea, options));
    }

    global.DeLongRichEditor = { enhance, enhanceAll, cleanForVisual, ensureQuill };
})(window);
