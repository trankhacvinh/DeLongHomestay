(function () {
    const root = document.getElementById('site-cms');
    if (!root || !window.Vue) return;
    const initial = JSON.parse(document.getElementById('site-cms-data')?.textContent || '{}');
    const { createApp, nextTick } = Vue;

    const sectionTypes = [
        { value: 'Hero', label: 'Hero / mở đầu' },
        { value: 'AvailabilitySearch', label: 'Kiểm tra phòng nhanh' },
        { value: 'RoomGrid', label: 'Danh sách phòng' },
        { value: 'FeatureGrid', label: 'Nội dung + điểm nổi bật' },
        { value: 'RichText', label: 'Nội dung tự do' },
        { value: 'Cta', label: 'Kêu gọi hành động' }
    ];

    function defaultContent(type) {
        if (type === 'Hero') return { eyebrow: '', title: '', body: '', primaryText: 'Đặt phòng', primaryUrl: '/booking', secondaryText: 'Xem phòng', secondaryUrl: '/rooms', imageUrl: '' };
        if (type === 'AvailabilitySearch') return { title: 'Chọn ngày bạn muốn ghé' };
        if (type === 'RoomGrid') return { eyebrow: 'KHÔNG GIAN', title: 'Chọn căn phòng hợp với nhịp của bạn', limit: 6 };
        if (type === 'FeatureGrid') return { eyebrow: '', title: '', body: '', items: [] };
        if (type === 'Cta') return { title: '', body: '', buttonText: 'Đặt phòng', buttonUrl: '/booking' };
        return { html: '<p>Nội dung mới</p>' };
    }

    createApp({
        data() {
            const site = initial.site || { settings: {}, sections: [] };
            return {
                propertyId: initial.propertyId,
                propertyName: initial.propertyName || '',
                publicBasePath: initial.publicBasePath || '/',
                canEditCode: initial.canEditCode === true,
                tab: 'settings',
                settings: Object.assign({ siteName: '', tagline: '', address: '', phone: '', email: '', facebookUrl: '', zaloUrl: '', googleMapsUrl: '', coverImageUrl: '', logoUrl: '', faviconUrl: '', ogImageUrl: '', metaTitle: '', metaDescription: '', canonicalBaseUrl: '', ogTitle: '', ogDescription: '', googleSiteVerification: '', robotsIndex: true, customCss: '', customJs: '' }, site.settings || {}),
                sections: site.sections || [],
                sectionTypes,
                sectionEditor: { open: false, mode: 'create', id: null },
                sectionForm: { type: 'Hero', name: '', variant: 'split', isVisible: true, content: defaultContent('Hero'), itemsText: '' },
                saving: false,
                sortable: null,
                toast: { show: false, type: 'success', message: '' }
            };
        },
        watch: {
            tab(value) { if (value === 'home') nextTick(() => this.mountSortable()); }
        },
        methods: {
            sectionTypeLabel(type) { return this.sectionTypes.find(x => x.value === type)?.label || type; },
            variantsFor(type) {
                if (type === 'Hero') return ['split', 'centered', 'image-first', 'image-full', 'booking-overlay', 'editorial'];
                if (type === 'RoomGrid') return ['grid-3', 'grid-2', 'featured-first', 'editorial-cards', 'horizontal-scroll'];
                if (type === 'FeatureGrid') return ['split', 'stacked', 'icon-grid', 'dark-band', 'editorial'];
                if (type === 'RichText') return ['narrow', 'wide', 'editorial'];
                if (type === 'Cta') return ['card', 'full-width', 'dark', 'offer'];
                return ['card', 'minimal'];
            },
            mountSortable() {
                if (!window.Sortable) return;
                if (this.sortable) this.sortable.destroy();
                const el = document.getElementById('home-section-list');
                if (!el) return;
                this.sortable = Sortable.create(el, {
                    animation: 160,
                    handle: '.home-drag-handle',
                    ghostClass: 'dragging',
                    onEnd: async (event) => {
                        if (event.oldIndex === event.newIndex) return;
                        const moved = this.sections.splice(event.oldIndex, 1)[0];
                        this.sections.splice(event.newIndex, 0, moved);
                        try {
                            await DeLongApi.put(`/api/admin/properties/${this.propertyId}/site/sections/reorder`, { ids: this.sections.map(x => x.id) });
                            this.notify('Đã cập nhật thứ tự trang chủ.');
                        } catch (error) { this.notify(error.message || 'Không thể đổi thứ tự.', 'error'); }
                    }
                });
            },
            async saveSettings() {
                if (this.saving) return;
                this.saving = true;
                try {
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/site/settings`, this.settings);
                    this.settings = Object.assign({}, this.settings, saved || {});
                    this.notify('Đã lưu cấu hình website.');
                } catch (error) { this.notify(error.message || 'Không thể lưu cấu hình website.', 'error'); }
                finally { this.saving = false; }
            },
            async uploadAsset(kind, event) {
                const file = event.target.files?.[0];
                event.target.value = '';
                if (!file) return;
                const form = new FormData(); form.append('file', file);
                try {
                    const asset = await DeLongApi.postForm(`/api/admin/properties/${this.propertyId}/site/assets/${kind}`, form);
                    if (kind === 'cover') this.settings.coverImageUrl = asset.url;
                    if (kind === 'logo') this.settings.logoUrl = asset.url;
                    if (kind === 'favicon') this.settings.faviconUrl = asset.url;
                    if (kind === 'og') this.settings.ogImageUrl = asset.url;
                    await this.saveSettings();
                } catch (error) { this.notify(error.message || 'Không thể tải ảnh.', 'error'); }
            },
            async uploadSectionImage(event) {
                const file = event.target.files?.[0]; event.target.value = '';
                if (!file) return;
                const form = new FormData(); form.append('file', file);
                try {
                    const asset = await DeLongApi.postForm(`/api/admin/properties/${this.propertyId}/site/assets/section`, form);
                    this.sectionForm.content.imageUrl = asset.url;
                    this.notify('Đã tải ảnh. Lưu khối để áp dụng.');
                } catch (error) { this.notify(error.message || 'Không thể tải ảnh.', 'error'); }
            },
            openSectionCreate() {
                const type = 'Hero';
                this.sectionForm = { type, name: '', variant: this.variantsFor(type)[0], isVisible: true, content: defaultContent(type), itemsText: '' };
                this.sectionEditor = { open: true, mode: 'create', id: null };
            },
            openSectionEdit(section) {
                let content = {};
                try { content = JSON.parse(section.contentJson || '{}'); } catch { content = {}; }
                this.sectionForm = {
                    type: section.type,
                    name: section.name,
                    variant: section.variant,
                    isVisible: section.isVisible,
                    content: Object.assign(defaultContent(section.type), content),
                    itemsText: Array.isArray(content.items) ? content.items.join('\n') : ''
                };
                this.sectionEditor = { open: true, mode: 'edit', id: section.id };
            },
            closeSectionEditor() { if (!this.saving) this.sectionEditor.open = false; },
            sectionTypeChanged() {
                const type = this.sectionForm.type;
                this.sectionForm.content = defaultContent(type);
                this.sectionForm.variant = this.variantsFor(type)[0];
                this.sectionForm.itemsText = '';
            },
            sectionPayload() {
                const content = Object.assign({}, this.sectionForm.content);
                if (this.sectionForm.type === 'FeatureGrid') content.items = this.sectionForm.itemsText.split('\n').map(x => x.trim()).filter(Boolean);
                return { type: this.sectionForm.type, name: this.sectionForm.name, variant: this.sectionForm.variant, isVisible: this.sectionForm.isVisible, contentJson: JSON.stringify(content) };
            },
            async saveSection() {
                if (this.saving) return;
                this.saving = true;
                try {
                    const payload = this.sectionPayload();
                    let saved;
                    if (this.sectionEditor.mode === 'create') saved = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/site/sections`, payload);
                    else saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/site/sections/${this.sectionEditor.id}`, payload);
                    if (this.sectionEditor.mode === 'create') this.sections.push(saved);
                    else {
                        const index = this.sections.findIndex(x => x.id === saved.id);
                        if (index >= 0) this.sections.splice(index, 1, saved);
                    }
                    this.sectionEditor.open = false; this.notify('Đã lưu khối trang chủ.'); nextTick(() => this.mountSortable());
                } catch (error) { this.notify(error.message || 'Không thể lưu khối.', 'error'); }
                finally { this.saving = false; }
            },
            async toggleSection(section) {
                const content = section.contentJson || '{}';
                try {
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/site/sections/${section.id}`, { type: section.type, name: section.name, variant: section.variant, contentJson: content, isVisible: !section.isVisible });
                    Object.assign(section, saved);
                } catch (error) { this.notify(error.message || 'Không thể đổi trạng thái khối.', 'error'); }
            },
            async deleteSection() {
                if (!this.sectionEditor.id || !window.confirm('Xóa khối này khỏi trang chủ?')) return;
                this.saving = true;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/site/sections/${this.sectionEditor.id}`);
                    this.sections = this.sections.filter(x => x.id !== this.sectionEditor.id);
                    this.sectionEditor.open = false; this.notify('Đã xóa khối.'); nextTick(() => this.mountSortable());
                } catch (error) { this.notify(error.message || 'Không thể xóa khối.', 'error'); }
                finally { this.saving = false; }
            },
            notify(message, type = 'success') {
                this.toast = { show: true, message, type };
                clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2800);
            }
        }
    }).mount(root);
})();