(function () {
    let loading;
    function viewer() {
        if (window.DeLongRoomPhotos) return Promise.resolve(window.DeLongRoomPhotos);
        if (loading) return loading;
        loading = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = '/js/core/room-photo-viewer.js?v=20261108-1';
            script.onload = () => resolve(window.DeLongRoomPhotos);
            script.onerror = () => { script.remove(); loading = null; reject(new Error('Không tải được trình xem ảnh. Vui lòng thử lại.')); };
            document.head.appendChild(script);
        });
        return loading;
    }
    async function activate(event) {
        const trigger = event.target.closest?.('[data-room-photos], [data-room-photo-picker]');
        if (!trigger || trigger.disabled) return;
        if (event.type === 'keydown' && !['Enter', ' '].includes(event.key)) return;
        event.preventDefault();
        event.stopPropagation();
        if (trigger.getAttribute('aria-busy') === 'true') return;
        trigger.setAttribute('aria-busy', 'true');
        try {
            const photos = await viewer();
            if (!trigger.isConnected) return;
            if (trigger.hasAttribute('data-room-photo-picker')) photos.pick(trigger);
            else photos.open(trigger.dataset.roomPhotos, trigger);
        } catch (error) {
            if (trigger.hasAttribute('data-room-photo-picker')) {
                const select = trigger.parentElement.querySelector('select');
                if (select) {
                    select.hidden = false; select.style.removeProperty('display');
                    select.removeAttribute('aria-hidden'); select.removeAttribute('tabindex');
                    trigger.hidden = true; trigger.style.display = 'none'; select.focus();
                }
            }
            let message = trigger.parentElement.querySelector('[data-room-photo-error]');
            if (!message) {
                message = document.createElement('small');
                message.dataset.roomPhotoError = '';
                message.setAttribute('role', 'status');
                trigger.parentElement.appendChild(message);
            }
            message.textContent = 'Không tải được ảnh phòng. Vui lòng thử lại.';
        } finally { trigger.removeAttribute('aria-busy'); }
    }
    document.addEventListener('click', activate, true);
    document.addEventListener('keydown', activate, true);
})();
