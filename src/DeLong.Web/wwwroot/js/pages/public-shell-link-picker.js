(function () {
    if (!document.body?.classList.contains('public-body')) return;

    const pagePath = window.location.pathname.replace(/\/+$/, '') || '/';
    const scoped = pagePath.match(/^\/h\/([^/]+)(?:\/|$)/i);
    const siteSlug = scoped ? decodeURIComponent(scoped[1]) : '';

    const coreLinks = [
        { token: '@home', label: 'Trang chủ' },
        { token: '@rooms', label: 'Phòng' },
        { token: '@booking', label: 'Đặt phòng' },
        { token: '@lookup', label: 'Tra cứu' },
        { token: '@branches', label: 'Cơ sở' }
    ];

    function resolveToken(token) {
        const prefix = siteSlug ? `/h/${encodeURIComponent(siteSlug)}` : '';
        switch (String(token || '').toLowerCase()) {
            case '@home': return prefix || '/';
            case '@rooms': return prefix ? `${prefix}/rooms` : '/rooms';
            case '@branches': return '/#co-so';
            case '@booking': return prefix ? `${prefix}/booking` : '/booking';
            case '@lookup': return prefix ? `${prefix}/booking/lookup` : '/booking/lookup';
            default: return String(token || '');
        }
    }

    function defaultTokenFor(target, input) {
        if (target?.matches?.('[data-nav-id]')) {
            const id = String(target.dataset.navId || '').toLowerCase();
            if (['home', 'rooms', 'branches', 'booking', 'lookup'].includes(id)) return `@${id}`;
        }
        if (input?.name === 'headerCtaUrl' || input?.name === 'footerBookingUrl') return '@booking';
        return '';
    }

    function sourceValue(target) {
        if (target?.matches?.('[data-nav-id]')) return target.dataset.navSourceUrl || '';
        return target?.dataset?.tokenSource || '';
    }

    function setSourceValue(target, value) {
        if (target?.matches?.('[data-nav-id]')) target.dataset.navSourceUrl = value;
        else if (target?.dataset) target.dataset.tokenSource = value;
    }

    function statusFor(target, input) {
        const source = sourceValue(target);
        if (source.startsWith('@') && input.value.trim() === resolveToken(source)) {
            const found = coreLinks.find(x => x.token === source.toLowerCase());
            return found ? `Đang dùng link hệ thống: ${found.label}` : 'Đang dùng link hệ thống';
        }
        return 'Đang dùng link tùy chỉnh';
    }

    function safePreviewUrl(value) {
        const raw = String(value || '').trim();
        if (!raw || /^(javascript|data|vbscript):/i.test(raw)) return '';
        try {
            const url = new URL(raw, window.location.origin);
            if (!['http:', 'https:', 'mailto:', 'tel:'].includes(url.protocol)) return '';
            return url.href;
        } catch {
            return '';
        }
    }

    function suggestionButton(link) {
        const resolved = resolveToken(link.token);
        return `<button type="button" data-link-suggestion="${link.token}" title="${resolved}">${link.label}<small>${resolved}</small></button>`;
    }

    function buildPicker(target, input) {
        if (!target || !input || target.dataset.systemLinkPicker === '1') return;
        target.dataset.systemLinkPicker = '1';

        const picker = document.createElement('div');
        picker.className = 'pve-system-link-picker';
        const defaultToken = defaultTokenFor(target, input);
        const blogUrl = siteSlug ? `/h/${encodeURIComponent(siteSlug)}/blog` : '/blog';
        picker.innerHTML = `
            <div class="pve-system-link-picker-head">
                <div><strong>Chọn nhanh link có sẵn</strong><small>Không cần nhớ đường dẫn kỹ thuật.</small></div>
                <span data-link-status></span>
            </div>
            <div class="pve-system-link-options">
                ${coreLinks.map(suggestionButton).join('')}
                <button type="button" data-link-value="${blogUrl}" title="${blogUrl}">Blog<small>${blogUrl}</small></button>
            </div>
            <div class="pve-system-link-actions">
                ${defaultToken ? `<button type="button" data-link-reset="${defaultToken}">↺ Khôi phục link mặc định</button>` : ''}
                <button type="button" data-link-test>↗ Mở thử link hiện tại</button>
            </div>`;

        input.closest('label')?.insertAdjacentElement('afterend', picker);
        if (!picker.isConnected) target.appendChild(picker);

        const updateStatus = () => {
            const status = picker.querySelector('[data-link-status]');
            if (status) status.textContent = statusFor(target, input);
            const test = picker.querySelector('[data-link-test]');
            if (test) test.disabled = !safePreviewUrl(input.value);
        };

        picker.addEventListener('click', event => {
            const tokenButton = event.target.closest('[data-link-suggestion]');
            if (tokenButton) {
                const token = tokenButton.dataset.linkSuggestion;
                setSourceValue(target, token);
                input.value = resolveToken(token);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.focus();
                updateStatus();
                return;
            }

            const valueButton = event.target.closest('[data-link-value]');
            if (valueButton) {
                const value = valueButton.dataset.linkValue || '';
                setSourceValue(target, value);
                input.value = value;
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.focus();
                updateStatus();
                return;
            }

            const reset = event.target.closest('[data-link-reset]');
            if (reset) {
                const token = reset.dataset.linkReset;
                setSourceValue(target, token);
                input.value = resolveToken(token);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.focus();
                updateStatus();
                return;
            }

            if (event.target.closest('[data-link-test]')) {
                const url = safePreviewUrl(input.value);
                if (url) window.open(url, '_blank', 'noopener,noreferrer');
            }
        });

        input.addEventListener('input', updateStatus);
        updateStatus();
    }

    function enhance(drawer) {
        if (!drawer?.querySelector?.('.pve-shell-editor')) return;

        drawer.querySelectorAll('[data-nav-id]').forEach(row => {
            const input = row.querySelector('[data-nav-url]');
            buildPicker(row, input);
        });

        drawer.querySelectorAll('[data-token-source]').forEach(target => {
            const input = target.querySelector('input[name="headerCtaUrl"], input[name="footerBookingUrl"]');
            buildPicker(target, input);
        });
    }

    let queued = false;
    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(() => {
            queued = false;
            document.querySelectorAll('.pve-shell-rates-drawer').forEach(enhance);
        });
    }

    const observer = new MutationObserver(queue);
    observer.observe(document.body, { childList: true, subtree: true });
    queue();
})();
