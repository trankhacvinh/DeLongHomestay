(function (global) {
    function linkUrl(value) {
        const raw = String(value || '').trim();
        if (!raw) return null;
        if (raw.startsWith('/') && !raw.startsWith('//')) return raw;
        try {
            const url = new URL(/^[a-z][a-z0-9+.-]*:/i.test(raw) ? raw : `https://${raw}`);
            return ['https:', 'http:', 'mailto:', 'tel:'].includes(url.protocol) ? url.href : null;
        } catch { return null; }
    }
    function youtubeUrl(value) {
        try {
            const url = new URL(linkUrl(value));
            const host = url.hostname.toLowerCase().replace(/^www\./, '');
            let id = '';
            if (host === 'youtu.be') id = url.pathname.split('/').filter(Boolean)[0] || '';
            else if (['youtube.com', 'm.youtube.com', 'youtube-nocookie.com'].includes(host)) {
                id = url.pathname === '/watch' ? url.searchParams.get('v') || '' :
                    /^\/(embed|shorts|live)\//.test(url.pathname) ? url.pathname.split('/')[2] || '' : '';
            }
            return /^[A-Za-z0-9_-]{11}$/.test(id) ? `https://www.youtube-nocookie.com/embed/${id}` : null;
        } catch { return null; }
    }
    function insertLink(quill, enabled) {
        if (!enabled) { quill.format('link', false, 'user'); return; }
        const selection = quill.getSelection(true);
        const raw = global.prompt('Nhập đường dẫn liên kết (https://…, email hoặc số điện thoại)');
        if (raw === null) return;
        const url = linkUrl(raw);
        if (!url) { global.alert('Đường dẫn không hợp lệ.'); return; }
        if (selection.length) quill.formatText(selection.index, selection.length, 'link', url, 'user');
        else quill.insertText(selection.index, raw.trim(), { link: url }, 'user');
    }
    function insertVideo(quill) {
        const raw = global.prompt('Dán đường dẫn video YouTube');
        if (raw === null) return;
        const url = youtubeUrl(raw);
        if (!url) { global.alert('Vui lòng nhập đường dẫn video YouTube hợp lệ.'); return; }
        const selection = quill.getSelection(true);
        quill.insertEmbed(selection.index, 'video', url, 'user');
        quill.insertText(selection.index + 1, '\n', 'user');
        quill.setSelection(selection.index + 2, 0, 'silent');
    }
    function addYouTubeButton(quill) {
        const toolbar = quill.getModule('toolbar')?.container;
        if (!toolbar || toolbar.querySelector('[data-insert-youtube]')) return;
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = '+ YouTube';
        button.title = 'Dán link YouTube để nhúng video';
        button.setAttribute('aria-label', button.title);
        button.dataset.insertYoutube = 'true';
        button.style.cssText = 'width:auto;padding:0 10px;font-weight:600;';
        button.addEventListener('click', () => insertVideo(quill));
        toolbar.appendChild(button);
    }
    global.DeLongRichTextTools = { linkUrl, youtubeUrl, insertLink, insertVideo, addYouTubeButton };
})(window);
