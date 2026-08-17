(function (global) {
    if (global.DeLongRichEditor) return;

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

    function commandButton(label, title, command, value) {
        return `<button type="button" data-rich-command="${command}"${value ? ` data-rich-value="${value}"` : ''} title="${title}">${label}</button>`;
    }

    function enhance(textarea, options) {
        if (!textarea || textarea.dataset.richEnhanced === '1') return null;
        textarea.dataset.richEnhanced = '1';
        const opts = options || {};

        const shell = document.createElement('div');
        shell.className = 'pve-rich-editor';
        shell.innerHTML = `
            <div class="pve-rich-modebar">
                <div class="pve-rich-tabs" role="tablist" aria-label="Chế độ soạn thảo">
                    <button type="button" class="active" data-rich-mode="visual">Trực quan</button>
                    <button type="button" data-rich-mode="html">&lt;/&gt; HTML</button>
                </div>
                <small>${opts.helpText || 'Dùng Trực quan cho nội dung thường; HTML dành cho người có kinh nghiệm.'}</small>
            </div>
            <div class="pve-rich-toolbar" data-rich-toolbar>
                ${commandButton('P', 'Đoạn văn', 'formatBlock', 'p')}
                ${commandButton('H2', 'Tiêu đề H2', 'formatBlock', 'h2')}
                ${commandButton('H3', 'Tiêu đề H3', 'formatBlock', 'h3')}
                <span></span>
                ${commandButton('<b>B</b>', 'Đậm', 'bold')}
                ${commandButton('<i>I</i>', 'Nghiêng', 'italic')}
                ${commandButton('<u>U</u>', 'Gạch chân', 'underline')}
                <span></span>
                ${commandButton('• List', 'Danh sách dấu chấm', 'insertUnorderedList')}
                ${commandButton('1. List', 'Danh sách số', 'insertOrderedList')}
                ${commandButton('❝', 'Trích dẫn', 'formatBlock', 'blockquote')}
                <span></span>
                <button type="button" data-rich-link title="Chèn liên kết">🔗</button>
                ${commandButton('⛓', 'Bỏ liên kết', 'unlink')}
                ${commandButton('Tx', 'Xóa định dạng', 'removeFormat')}
                <span></span>
                ${commandButton('↶', 'Hoàn tác', 'undo')}
                ${commandButton('↷', 'Làm lại', 'redo')}
            </div>
            <div class="pve-rich-visual" contenteditable="true" role="textbox" aria-multiline="true" data-rich-visual></div>
            <div class="pve-rich-source" data-rich-source></div>`;

        textarea.before(shell);
        const source = shell.querySelector('[data-rich-source]');
        source.appendChild(textarea);
        textarea.classList.add('pve-rich-source-textarea');
        const visual = shell.querySelector('[data-rich-visual]');
        const toolbar = shell.querySelector('[data-rich-toolbar]');
        let mode = 'visual';
        let syncingFromVisual = false;

        function syncVisualFromSource() {
            visual.innerHTML = cleanForVisual(textarea.value);
        }

        function syncSourceFromVisual() {
            syncingFromVisual = true;
            textarea.value = visual.innerHTML;
            dispatchInput(textarea);
            syncingFromVisual = false;
        }

        function setMode(next) {
            if (next === mode) return;
            if (next === 'html') syncSourceFromVisual();
            else syncVisualFromSource();
            mode = next;
            shell.classList.toggle('is-html', mode === 'html');
            shell.querySelectorAll('[data-rich-mode]').forEach(button => button.classList.toggle('active', button.dataset.richMode === mode));
        }

        syncVisualFromSource();

        shell.querySelectorAll('[data-rich-mode]').forEach(button => button.addEventListener('click', () => setMode(button.dataset.richMode)));
        toolbar.querySelectorAll('[data-rich-command]').forEach(button => button.addEventListener('click', event => {
            event.preventDefault();
            visual.focus();
            document.execCommand(button.dataset.richCommand, false, button.dataset.richValue || null);
            syncSourceFromVisual();
        }));
        toolbar.querySelector('[data-rich-link]').addEventListener('click', event => {
            event.preventDefault();
            const url = prompt('Nhập đường dẫn liên kết (https://... hoặc /duong-dan):', 'https://');
            if (!url) return;
            visual.focus();
            document.execCommand('createLink', false, url.trim());
            syncSourceFromVisual();
        });

        visual.addEventListener('input', syncSourceFromVisual);
        visual.addEventListener('blur', syncSourceFromVisual);
        visual.addEventListener('paste', () => setTimeout(() => {
            const safe = cleanForVisual(visual.innerHTML);
            if (safe !== visual.innerHTML) visual.innerHTML = safe;
            syncSourceFromVisual();
        }, 0));
        textarea.addEventListener('input', () => {
            if (syncingFromVisual || mode === 'html') return;
            syncVisualFromSource();
        });

        return {
            shell,
            visual,
            textarea,
            getMode: () => mode,
            setMode,
            sync: () => mode === 'visual' ? syncSourceFromVisual() : syncVisualFromSource()
        };
    }

    function enhanceAll(root, selector, options) {
        (root || document).querySelectorAll(selector || 'textarea[data-rich-text]').forEach(textarea => enhance(textarea, options));
    }

    global.DeLongRichEditor = { enhance, enhanceAll, cleanForVisual };
})(window);
