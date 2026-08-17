(function () {
    if (!document.body?.classList.contains('public-body')) return;

    function modeLabel(mode) {
        return ({ all: 'Tất cả', properties: 'Theo cơ sở', manual: 'Chọn thủ công' })[mode] || 'Tất cả';
    }

    function enhance(form) {
        if (!form || form.dataset.globalSourceEnhanced === '1') return;
        const drawer = form.closest('.pve-drawer');
        const heading = drawer?.querySelector('h2')?.textContent || '';
        const isGallery = /Gallery/i.test(heading);
        const mode = form.elements.mode;
        const limit = form.elements.limit;
        const propertiesPanel = form.querySelector('[data-source-properties]');
        const manualPanel = form.querySelector('[data-source-manual]');
        if (!mode || !propertiesPanel || !manualPanel) return;

        form.dataset.globalSourceEnhanced = '1';

        const state = document.createElement('div');
        state.className = 'pve-global-source-state';
        state.innerHTML = '<div><strong data-global-source-title></strong><small data-global-source-help></small></div><span data-global-source-count></span>';
        form.querySelector('[data-editorial-source]')?.before(state);

        const title = state.querySelector('[data-global-source-title]');
        const help = state.querySelector('[data-global-source-help]');
        const count = state.querySelector('[data-global-source-count]');

        function sync() {
            const currentMode = mode.value || 'all';
            propertiesPanel.hidden = currentMode !== 'properties';
            manualPanel.hidden = currentMode !== 'manual';

            // Inline display is intentional: older editor CSS gives .pve-field display:grid,
            // which otherwise wins over the native [hidden] rule.
            propertiesPanel.style.display = currentMode === 'properties' ? '' : 'none';
            manualPanel.style.display = currentMode === 'manual' ? '' : 'none';

            const selectedProperties = form.querySelectorAll('[name="editorial.propertyIds"]:checked').length;
            const selectedItems = form.querySelectorAll('[name="editorial.itemIds"]:checked').length;
            const maxItems = Math.max(1, Number(limit?.value) || (isGallery ? 8 : 3));

            if (title) title.textContent = `Nguồn đang dùng: ${modeLabel(currentMode)}`;
            if (currentMode === 'manual') {
                if (help) help.textContent = selectedItems
                    ? `Chỉ ${isGallery ? 'ảnh' : 'bài viết'} được tick mới xuất hiện trên website.`
                    : `Chưa chọn ${isGallery ? 'ảnh' : 'bài viết'} nào; khu vực này sẽ không hiển thị sau khi lưu.`;
                if (count) {
                    count.textContent = `${selectedItems} đã chọn · tối đa ${maxItems}`;
                    count.classList.toggle('is-warning', selectedItems > maxItems || selectedItems === 0);
                }
            } else if (currentMode === 'properties') {
                if (help) help.textContent = selectedProperties
                    ? `Website tự lấy nội dung đã xuất bản từ ${selectedProperties} cơ sở được chọn.`
                    : 'Chưa chọn cơ sở; hãy chọn ít nhất một cơ sở hoặc đổi nguồn.';
                if (count) {
                    count.textContent = `${selectedProperties} cơ sở · tối đa ${maxItems}`;
                    count.classList.toggle('is-warning', selectedProperties === 0);
                }
            } else {
                if (help) help.textContent = `Website tự lấy tất cả nội dung đã xuất bản; các dấu tick bên dưới không được dùng ở chế độ này.`;
                if (count) {
                    count.textContent = `Tự động · tối đa ${maxItems}`;
                    count.classList.remove('is-warning');
                }
            }
        }

        mode.addEventListener('change', sync);
        limit?.addEventListener('input', sync);

        form.addEventListener('change', event => {
            const input = event.target;
            if (!(input instanceof HTMLInputElement)) return;

            if (input.matches('[name="editorial.itemIds"]')) {
                // A direct click on an item is an explicit request for manual selection.
                // This prevents the confusing state where checkboxes change but mode remains "all".
                form.dataset.manualSelectionTouched = '1';
                if (mode.value !== 'manual') {
                    mode.value = 'manual';
                    mode.dispatchEvent(new Event('change', { bubbles: true }));
                }
                sync();
                return;
            }

            if (input.matches('[name="editorial.propertyIds"]')) {
                if (mode.value !== 'properties') {
                    mode.value = 'properties';
                    mode.dispatchEvent(new Event('change', { bubbles: true }));
                }
                sync();
            }
        });

        // Run before the core submit handler so the payload always reflects the user's
        // most recent direct item/property selection.
        form.addEventListener('submit', () => {
            if (form.dataset.manualSelectionTouched === '1') mode.value = 'manual';
        }, true);

        sync();
    }

    function scan() {
        document.querySelectorAll('form[data-global-editorial]').forEach(enhance);
    }

    let queued = false;
    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(() => {
            queued = false;
            scan();
        });
    }

    new MutationObserver(queue).observe(document.body, { childList: true, subtree: true });
    scan();
})();
