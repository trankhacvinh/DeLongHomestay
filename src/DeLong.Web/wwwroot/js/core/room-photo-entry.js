// Tiny always-loaded hook: the photo sheet (JS + CSS) is only fetched the first time someone opens it.
(function () {
    let loading;
    function viewer() {
        if (window.DeLongRoomPhotos) return Promise.resolve(window.DeLongRoomPhotos);
        if (loading) return loading;
        loading = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '/js/core/room-photo-viewer.js?v=20261108-2';
            script.onload = () => resolve(window.DeLongRoomPhotos);
            script.onerror = () => { script.remove(); loading = null; reject(new Error('viewer')); };
            document.head.appendChild(script);
        });
        return loading;
    }
    window.DeLongRoomPhotosLoad = viewer;
    async function activate(event) {
        const trigger = event.target.closest?.('[data-room-photos], [data-room-photo-picker]');
        if (!trigger || trigger.disabled) return;
        if (event.type === 'keydown' && !['Enter', ' '].includes(event.key)) return;
        event.preventDefault();
        event.stopPropagation();
        if (trigger.getAttribute('aria-busy') === 'true') return;
        trigger.setAttribute('aria-busy', 'true');
        const picker = trigger.hasAttribute('data-room-photo-picker');
        const field = trigger.closest('[data-room-photo-field]') || trigger.parentElement;
        try {
            const photos = await viewer();
            if (!trigger.isConnected) return;
            if (picker) photos.pick(trigger);
            else photos.open(trigger.dataset.roomPhotos, trigger, { title: trigger.dataset.roomName });
        } catch {
            const select = picker && field.querySelector('select');
            if (select) {
                // Photo module failed to load: fall back to the plain room dropdown.
                select.hidden = false; select.style.removeProperty('display');
                select.removeAttribute('aria-hidden'); select.removeAttribute('tabindex');
                field.querySelectorAll('[data-room-photo-picker-ui]').forEach(node => { node.hidden = true; node.style.display = 'none'; });
                select.focus();
            }
            let message = field.querySelector('[data-room-photo-error]');
            if (!message) {
                message = document.createElement('small');
                message.dataset.roomPhotoError = '';
                message.setAttribute('role', 'status');
                field.appendChild(message);
            }
            message.textContent = 'Không tải được ảnh phòng. Vui lòng thử lại.';
        } finally { trigger.removeAttribute('aria-busy'); }
    }
    document.addEventListener('click', activate, true);
    document.addEventListener('keydown', activate, true);
})();
