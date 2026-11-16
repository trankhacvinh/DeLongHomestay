// Admin → Giao diện: pick a theme + colour preset, switch homepage blocks on/off and drag them into order.
(function () {
    const root = document.getElementById('theme-picker');
    if (!root || !window.Vue) return;
    const initial = JSON.parse(document.getElementById('theme-picker-data')?.textContent || '{}');
    const { createApp, nextTick } = Vue;

    createApp({
        data() {
            const themes = (initial.themes || []).map(theme => ({ ...theme, blocks: (theme.blocks || []).map(block => ({ ...block })) }));
            return { themes, activeTheme: initial.activeTheme, legacyCustom: !!initial.legacyCustom, editKey: initial.activeTheme, busy: false, brokenPreviews: {}, sortable: null, toast: { show: false, type: 'success', message: '' } };
        },
        computed: {
            editing() { return this.themes.find(theme => theme.key === this.editKey) || null; }
        },
        watch: { editKey() { nextTick(() => this.mountSortable()); } },
        mounted() { nextTick(() => this.mountSortable()); },
        methods: {
            previewUrl(theme) { return `/?theme=${encodeURIComponent(theme.key)}&palette=${encodeURIComponent(theme.palette)}`; },
            previewStyle(theme) {
                const palette = (theme.palettes || []).find(x => x.key === theme.palette) || (theme.palettes || [])[0];
                const colors = palette?.swatches || ['#ddd', '#f5f5f5', '#bbb'];
                return { background: `linear-gradient(135deg, ${colors[0]} 0 55%, ${colors[2] || colors[0]} 55% 70%, ${colors[1] || '#fff'} 70%)` };
            },
            mountSortable() {
                if (!window.Sortable) return;
                if (this.sortable) this.sortable.destroy();
                const el = document.getElementById('theme-block-list');
                if (!el) return;
                this.sortable = Sortable.create(el, {
                    animation: 160, handle: '.home-drag-handle', ghostClass: 'dragging',
                    onEnd: e => {
                        if (e.oldIndex === e.newIndex || !this.editing) return;
                        const moved = this.editing.blocks.splice(e.oldIndex, 1)[0];
                        this.editing.blocks.splice(e.newIndex, 0, moved);
                    }
                });
            },
            applySaved(theme, saved) {
                this.activeTheme = saved?.themeKey || this.activeTheme;
                this.legacyCustom = saved?.mode === 'custom';
                const layout = saved?.layouts?.[theme.key];
                if (Array.isArray(layout)) {
                    const byType = Object.fromEntries(theme.blocks.map(block => [block.type, block]));
                    theme.blocks = layout.filter(item => byType[item.type]).map(item => ({ ...byType[item.type], visible: !!item.visible }));
                }
                if (saved?.palettes?.[theme.key]) theme.palette = saved.palettes[theme.key];
                nextTick(() => this.mountSortable());
            },
            async send(theme, body, message) {
                if (this.busy) return null;
                this.busy = true;
                try {
                    const saved = await DeLongApi.put('/api/admin/site/global/theme', { editTheme: theme.key, ...body });
                    this.applySaved(theme, saved);
                    this.notify(message);
                    return saved;
                } catch (err) { this.notify(err.message || 'Không thể lưu giao diện.', 'error'); return null; }
                finally { this.busy = false; }
            },
            layoutOf(theme) { return theme.blocks.map(block => ({ type: block.type, visible: !!block.visible })); },
            activate(theme) {
                return this.send(theme, { themeKey: theme.key, paletteKey: theme.palette, layout: this.layoutOf(theme) }, `Website đang dùng ${theme.name}.`);
            },
            save() {
                const theme = this.editing;
                if (!theme) return null;
                return this.send(theme, { paletteKey: theme.palette, layout: this.layoutOf(theme) }, 'Đã lưu bảng màu và các khối.');
            },
            async resetLayout() {
                const theme = this.editing;
                if (!theme) return;
                const saved = await this.send(theme, { resetLayout: true }, 'Đã trả các khối về mặc định của theme. Tải lại trang để xem danh sách mới.');
                if (saved) window.location.reload();
            },
            notify(message, type = 'success') { this.toast = { show: true, message, type }; clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2800); }
        }
    }).mount(root);
})();
