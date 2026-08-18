(function (global) {
    const R = global.DeLongRowInline;
    if (!R?.ready) return;

    function safeUrl(value) {
        const url = String(value || '').trim();
        return url && !/^(javascript|data|vbscript):/i.test(url) ? url : '';
    }
    function youtube(url) {
        try {
            const parsed = new URL(url, location.origin), host = parsed.hostname.replace(/^www\./, '');
            let id = '';
            if (host === 'youtu.be') id = parsed.pathname.split('/').filter(Boolean)[0] || '';
            else if (host === 'youtube.com' || host === 'm.youtube.com') id = parsed.searchParams.get('v') || (parsed.pathname.match(/\/(?:embed|shorts)\/([^/?]+)/) || [])[1] || '';
            return /^[\w-]{6,20}$/.test(id) ? `https://www.youtube-nocookie.com/embed/${id}` : '';
        } catch { return ''; }
    }
    function vimeo(url) {
        try {
            const parsed = new URL(url, location.origin), host = parsed.hostname.replace(/^www\./, '');
            const id = (parsed.pathname.match(/\/(\d+)(?:$|\/)/) || [])[1] || '';
            return (host === 'vimeo.com' || host === 'player.vimeo.com') && /^\d+$/.test(id) ? `https://player.vimeo.com/video/${id}` : '';
        } catch { return ''; }
    }
    function enhanceVideo(root) {
        if (root.dataset.advancedRuntime === '1') return;
        const link = root.querySelector('.dl-advanced-video-link'), url = safeUrl(link?.getAttribute('href'));
        if (!link || !url || url === '#') return;
        let media = null;
        if (/\.(mp4|webm|ogg)(?:[?#].*)?$/i.test(url)) {
            media = document.createElement('video');
            media.className = 'dl-advanced-video-player'; media.src = url; media.controls = true; media.preload = 'metadata'; media.playsInline = true;
            const poster = link.querySelector('.dl-advanced-video-poster')?.getAttribute('src'); if (poster) media.poster = poster;
        } else {
            const embed = youtube(url) || vimeo(url);
            if (embed) {
                media = document.createElement('iframe'); media.className = 'dl-advanced-video-player'; media.src = embed; media.loading = 'lazy'; media.title = link.querySelector('.dl-advanced-video-copy strong')?.textContent?.trim() || 'Video'; media.allowFullscreen = true; media.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share'; media.style.aspectRatio = '16 / 9'; media.style.minHeight = '220px';
            }
        }
        if (media) { link.replaceWith(media); root.dataset.advancedRuntime = '1'; }
    }
    function enhanceMap(root) {
        if (root.dataset.advancedRuntime === '1') return;
        const canvas = root.querySelector('.dl-advanced-map-canvas'), address = root.querySelector('.dl-advanced-map-address')?.textContent?.trim();
        if (!canvas || !address) return;
        const frame = document.createElement('iframe'); frame.title = root.querySelector('.dl-advanced-map-copy > strong')?.textContent?.trim() || 'Bản đồ'; frame.loading = 'lazy'; frame.referrerPolicy = 'no-referrer-when-downgrade'; frame.src = `https://www.google.com/maps?q=${encodeURIComponent(address)}&output=embed`; frame.style.cssText = 'display:block;width:100%;min-height:220px;height:100%;border:0'; canvas.replaceChildren(frame); root.dataset.advancedRuntime = '1';
    }
    function enhance(scope) {
        scope.querySelectorAll?.('.dl-advanced-video').forEach(enhanceVideo);
        scope.querySelectorAll?.('.dl-advanced-map').forEach(enhanceMap);
    }

    R.ready.then(canEdit => {
        if (canEdit) return;
        enhance(document);
        new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(node => {
            if (node.nodeType === 1) enhance(node.matches?.('.dl-advanced-video,.dl-advanced-map') ? node.parentElement || document : node);
        }))).observe(document.body, { childList: true, subtree: true });
    }).catch(() => {});
})(window);
