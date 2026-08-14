(function () {
    const NativeQuill = window.Quill;
    if (!NativeQuill) return;

    const allowedSizes = ['25', '50', '75', '100'];
    const allowedAlignments = ['left', 'center', 'right'];
    const sizeClasses = allowedSizes.map(value => `room-image-size-${value}`);
    const alignmentClasses = allowedAlignments.map(value => `room-image-align-${value}`);

    const BaseImage = NativeQuill.import('formats/image');
    class RoomImageBlot extends BaseImage {
        static formats(domNode) {
            const formats = super.formats(domNode) || {};
            const sizeClass = sizeClasses.find(name => domNode.classList.contains(name));
            const alignClass = alignmentClasses.find(name => domNode.classList.contains(name));
            if (sizeClass) formats.roomSize = sizeClass.replace('room-image-size-', '');
            if (alignClass) formats.roomAlign = alignClass.replace('room-image-align-', '');
            return formats;
        }

        format(name, value) {
            if (name === 'roomSize') {
                sizeClasses.forEach(item => this.domNode.classList.remove(item));
                if (allowedSizes.includes(String(value))) this.domNode.classList.add(`room-image-size-${value}`);
                return;
            }
            if (name === 'roomAlign') {
                alignmentClasses.forEach(item => this.domNode.classList.remove(item));
                if (allowedAlignments.includes(String(value))) this.domNode.classList.add(`room-image-align-${value}`);
                return;
            }
            super.format(name, value);
        }
    }
    NativeQuill.register(RoomImageBlot, true);

    class TrackingQuill extends NativeQuill {
        constructor(...args) {
            super(...args);
            window.__deLongRoomContentQuill = this;
            queueMicrotask(() => document.dispatchEvent(new CustomEvent('delong:room-quill-ready', { detail: this })));
        }
    }
    window.Quill = TrackingQuill;

    let quill = null;
    let selectedImage = null;
    let toolbar = null;
    let altInput = null;
    let replaceTarget = null;
    let scrollParent = null;

    function createButton(label, action, value, extraClass) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `room-image-tool-btn${extraClass ? ` ${extraClass}` : ''}`;
        button.textContent = label;
        button.dataset.action = action;
        if (value != null) button.dataset.value = value;
        return button;
    }

    function buildToolbar() {
        if (toolbar) return toolbar;
        toolbar = document.createElement('div');
        toolbar.className = 'room-image-context-toolbar';
        toolbar.hidden = true;
        toolbar.setAttribute('role', 'toolbar');
        toolbar.setAttribute('aria-label', 'Công cụ chỉnh ảnh trong nội dung');

        const title = document.createElement('strong');
        title.className = 'room-image-tool-title';
        title.textContent = 'Ảnh trong bài';
        toolbar.appendChild(title);

        const sizeGroup = document.createElement('div');
        sizeGroup.className = 'room-image-tool-group';
        sizeGroup.setAttribute('aria-label', 'Kích thước ảnh');
        for (const size of allowedSizes) sizeGroup.appendChild(createButton(`${size}%`, 'size', size));
        toolbar.appendChild(sizeGroup);

        const alignGroup = document.createElement('div');
        alignGroup.className = 'room-image-tool-group';
        alignGroup.setAttribute('aria-label', 'Căn ảnh');
        alignGroup.appendChild(createButton('Trái', 'align', 'left'));
        alignGroup.appendChild(createButton('Giữa', 'align', 'center'));
        alignGroup.appendChild(createButton('Phải', 'align', 'right'));
        toolbar.appendChild(alignGroup);

        const altWrap = document.createElement('label');
        altWrap.className = 'room-image-alt-control';
        const altLabel = document.createElement('span');
        altLabel.textContent = 'Alt';
        altInput = document.createElement('input');
        altInput.type = 'text';
        altInput.maxLength = 300;
        altInput.placeholder = 'Mô tả ảnh';
        altWrap.append(altLabel, altInput);
        toolbar.appendChild(altWrap);

        const actionGroup = document.createElement('div');
        actionGroup.className = 'room-image-tool-group room-image-tool-actions';
        actionGroup.appendChild(createButton('Thay ảnh', 'replace'));
        actionGroup.appendChild(createButton('Xóa', 'delete', null, 'is-danger'));
        toolbar.appendChild(actionGroup);

        toolbar.addEventListener('click', handleToolbarClick);
        altInput.addEventListener('change', applyAltText);
        altInput.addEventListener('keydown', event => {
            if (event.key === 'Enter') {
                event.preventDefault();
                applyAltText();
                altInput.blur();
            }
        });
        document.body.appendChild(toolbar);
        return toolbar;
    }

    function getBlot(image) {
        try { return image ? window.Quill.find(image) : null; }
        catch { return null; }
    }

    function getImageIndex(image) {
        const blot = getBlot(image);
        if (!blot || !quill) return null;
        try { return quill.getIndex(blot); }
        catch { return null; }
    }

    function currentSize(image) {
        const className = sizeClasses.find(name => image?.classList.contains(name));
        return className ? className.replace('room-image-size-', '') : '100';
    }

    function currentAlignment(image) {
        const className = alignmentClasses.find(name => image?.classList.contains(name));
        return className ? className.replace('room-image-align-', '') : 'center';
    }

    function updateToolbarState() {
        if (!toolbar || !selectedImage) return;
        const size = currentSize(selectedImage);
        const align = currentAlignment(selectedImage);
        toolbar.querySelectorAll('[data-action="size"]').forEach(button => {
            button.classList.toggle('is-active', button.dataset.value === size);
            button.setAttribute('aria-pressed', button.dataset.value === size ? 'true' : 'false');
        });
        toolbar.querySelectorAll('[data-action="align"]').forEach(button => {
            button.classList.toggle('is-active', button.dataset.value === align);
            button.setAttribute('aria-pressed', button.dataset.value === align ? 'true' : 'false');
        });
        altInput.value = selectedImage.getAttribute('alt') || '';
        positionToolbar();
    }

    function selectImage(image) {
        if (!image || !quill?.root.contains(image)) return;
        if (selectedImage && selectedImage !== image) selectedImage.removeAttribute('data-editor-selected');
        selectedImage = image;
        selectedImage.setAttribute('data-editor-selected', 'true');
        buildToolbar().hidden = false;
        updateToolbarState();
    }

    function clearSelection() {
        if (selectedImage) selectedImage.removeAttribute('data-editor-selected');
        selectedImage = null;
        replaceTarget = null;
        if (toolbar) toolbar.hidden = true;
    }

    function formatSelected(name, value) {
        if (!selectedImage || !quill) return;
        const index = getImageIndex(selectedImage);
        if (index == null) return;
        quill.formatText(index, 1, name, value, 'user');
        const leaf = quill.getLeaf(index)?.[0];
        if (leaf?.domNode?.tagName === 'IMG') selectedImage = leaf.domNode;
        selectedImage?.setAttribute('data-editor-selected', 'true');
        updateToolbarState();
    }

    function applyAltText() {
        if (!selectedImage || !quill || !altInput) return;
        const index = getImageIndex(selectedImage);
        if (index == null) return;
        const value = altInput.value.trim();
        quill.formatText(index, 1, 'alt', value || false, 'user');
        const leaf = quill.getLeaf(index)?.[0];
        if (leaf?.domNode?.tagName === 'IMG') selectedImage = leaf.domNode;
        selectedImage?.setAttribute('data-editor-selected', 'true');
        updateToolbarState();
    }

    function deleteSelectedImage() {
        if (!selectedImage || !quill) return;
        const index = getImageIndex(selectedImage);
        if (index == null) return;
        quill.deleteText(index, 1, 'user');
        clearSelection();
    }

    function startReplace() {
        if (!selectedImage) return;
        replaceTarget = selectedImage;
        const imageButton = document.querySelector('.ql-toolbar button.ql-image');
        if (imageButton) imageButton.click();
    }

    function handleToolbarClick(event) {
        const button = event.target.closest('button[data-action]');
        if (!button) return;
        event.preventDefault();
        const action = button.dataset.action;
        if (action === 'size') formatSelected('roomSize', button.dataset.value);
        if (action === 'align') formatSelected('roomAlign', button.dataset.value);
        if (action === 'replace') startReplace();
        if (action === 'delete') deleteSelectedImage();
    }

    function replaceWithGalleryImage(button) {
        if (!replaceTarget || !quill) return false;
        const preview = button.querySelector('img');
        if (!preview) return false;
        const thumbnail = preview.getAttribute('src') || '';
        const largeUrl = thumbnail.replace(/\/thumb\.webp(?:\?.*)?$/i, '/large.webp');
        if (!largeUrl || largeUrl === thumbnail) return false;

        const oldImage = replaceTarget;
        const index = getImageIndex(oldImage);
        if (index == null) return false;
        const size = currentSize(oldImage);
        const align = currentAlignment(oldImage);
        const alt = preview.getAttribute('alt') || oldImage.getAttribute('alt') || '';

        quill.deleteText(index, 1, 'user');
        quill.insertEmbed(index, 'image', largeUrl, 'user');
        quill.formatText(index, 1, 'roomSize', size, 'user');
        quill.formatText(index, 1, 'roomAlign', align, 'user');
        if (alt) quill.formatText(index, 1, 'alt', alt, 'user');

        const leaf = quill.getLeaf(index)?.[0];
        replaceTarget = null;
        if (leaf?.domNode?.tagName === 'IMG') selectImage(leaf.domNode);
        return true;
    }

    function positionToolbar() {
        if (!toolbar || toolbar.hidden || !selectedImage) return;
        const rect = selectedImage.getBoundingClientRect();
        if (rect.bottom < 0 || rect.top > window.innerHeight || rect.right < 0 || rect.left > window.innerWidth) {
            toolbar.style.visibility = 'hidden';
            return;
        }
        toolbar.style.visibility = 'visible';
        const margin = 10;
        const toolbarRect = toolbar.getBoundingClientRect();
        let left = rect.left;
        left = Math.max(margin, Math.min(left, window.innerWidth - toolbarRect.width - margin));
        let top = rect.bottom + 8;
        if (top + toolbarRect.height > window.innerHeight - margin) top = rect.top - toolbarRect.height - 8;
        top = Math.max(margin, Math.min(top, window.innerHeight - toolbarRect.height - margin));
        toolbar.style.left = `${Math.round(left)}px`;
        toolbar.style.top = `${Math.round(top)}px`;
    }

    function attach(quillInstance) {
        if (!quillInstance || quill === quillInstance) return;
        quill = quillInstance;
        buildToolbar();
        quill.root.classList.add('room-editor-with-image-tools');
        quill.root.addEventListener('click', event => {
            const image = event.target.closest('img');
            if (image && quill.root.contains(image)) selectImage(image);
        });
        quill.root.addEventListener('scroll', positionToolbar, { passive: true });
        scrollParent = quill.root.closest('.ql-container');
        scrollParent?.addEventListener('scroll', positionToolbar, { passive: true });
        quill.on('text-change', () => {
            if (selectedImage && !quill.root.contains(selectedImage)) clearSelection();
            else positionToolbar();
        });
    }

    document.addEventListener('delong:room-quill-ready', event => attach(event.detail));
    document.addEventListener('click', event => {
        const galleryChoice = event.target.closest('.media-picker-grid button');
        if (galleryChoice && replaceTarget) {
            event.preventDefault();
            event.stopImmediatePropagation();
            if (replaceWithGalleryImage(galleryChoice)) {
                const close = document.querySelector('.media-picker-modal .modal-head .icon-btn');
                close?.click();
            }
            return;
        }

        const closePicker = event.target.closest('.media-picker-modal .modal-head .icon-btn');
        if (closePicker) replaceTarget = null;

        if (!selectedImage) return;
        if (event.target.closest('.room-image-context-toolbar')) return;
        if (event.target.closest('.ql-toolbar')) return;
        if (event.target === selectedImage) return;
        if (event.target.closest('.media-picker-modal')) return;
        if (event.target.closest('.ql-editor img')) return;
        clearSelection();
    }, true);

    window.addEventListener('scroll', positionToolbar, true);
    window.addEventListener('resize', positionToolbar, { passive: true });
    window.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || !selectedImage) return;
        event.stopImmediatePropagation();
        clearSelection();
    }, true);

    const readyPoll = window.setInterval(() => {
        if (!window.__deLongRoomContentQuill) return;
        window.clearInterval(readyPoll);
        attach(window.__deLongRoomContentQuill);
    }, 50);
    window.setTimeout(() => window.clearInterval(readyPoll), 5000);
})();