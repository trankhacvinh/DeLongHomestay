
(function () {
    const root = document.getElementById('global-site-cms');
    if (!root || !window.Vue) return;
    const initial = JSON.parse(document.getElementById('global-site-cms-data')?.textContent || '{}');
    const { createApp, nextTick } = Vue;
    const sectionTypes = [
        { value: 'Hero', label: 'Hero / mở đầu' },
        { value: 'BranchGrid', label: 'Danh sách cơ sở' },
        { value: 'RoomGrid', label: 'Danh sách phòng' },
        { value: 'AvailabilitySearch', label: 'Kiểm tra phòng nhanh' },
        { value: 'FeatureGrid', label: 'Nội dung + điểm nổi bật' },
        { value: 'RichText', label: 'Nội dung tự do' },
        { value: 'Cta', label: 'Kêu gọi hành động' }
    ];
    function defaultContent(type) {
        if (type === 'Hero') return { eyebrow: 'DE LONG HOMESTAY', title: '', body: '', primaryText: 'Xem tất cả phòng', primaryUrl: '/rooms', secondaryText: 'Khám phá các cơ sở', secondaryUrl: '/#co-so' };
        if (type === 'BranchGrid') return { eyebrow: 'CƠ SỞ', title: 'Chọn nơi bạn muốn ghé', propertyIds: [] };
        if (type === 'RoomGrid') return { eyebrow: 'PHÒNG', title: 'Một vài lựa chọn đang mở', mode: 'all', limit: 6, propertyQuotas: {}, roomIds: [] };
        if (type === 'AvailabilitySearch') return { title: 'Chọn cơ sở và ngày bạn muốn ghé' };
        if (type === 'FeatureGrid') return { eyebrow: '', title: '', body: '', items: [] };
        if (type === 'Cta') return { title: '', body: '', buttonText: 'Xem phòng', buttonUrl: '/rooms' };
        return { html: '<p>Nội dung mới</p>' };
    }
    createApp({
        data() { return { sections: initial.sections || [], properties: initial.properties || [], rooms: initial.rooms || [], sectionTypes, editor: { open: false, mode: 'create', id: null }, form: { type: 'Hero', name: '', variant: 'split', isVisible: true, content: defaultContent('Hero'), itemsText: '' }, saving: false, sortable: null, toast: { show: false, type: 'success', message: '' } }; },
        mounted() { nextTick(() => this.mountSortable()); },
        methods: {
            sectionTypeLabel(type) { return this.sectionTypes.find(x => x.value === type)?.label || type; },
            variantsFor(type) { if (type === 'Hero') return ['split', 'centered']; if (type === 'BranchGrid' || type === 'RoomGrid') return ['grid-3', 'grid-2', 'featured-first']; if (type === 'FeatureGrid') return ['split', 'stacked']; if (type === 'RichText') return ['narrow', 'wide']; if (type === 'Cta') return ['card', 'full-width']; return ['card', 'minimal']; },
            includes(list, id) { return Array.isArray(list) && list.some(x => String(x) === String(id)); },
            toggleArray(key, id) { const list = Array.isArray(this.form.content[key]) ? [...this.form.content[key]] : []; const i = list.findIndex(x => String(x) === String(id)); if (i >= 0) list.splice(i, 1); else list.push(id); this.form.content[key] = list; },
            normalizeContent(type, content) { const base = Object.assign(defaultContent(type), content || {}); if (type === 'BranchGrid' && !Array.isArray(base.propertyIds)) base.propertyIds = []; if (type === 'RoomGrid') { if (!Array.isArray(base.roomIds)) base.roomIds = []; if (!base.propertyQuotas || typeof base.propertyQuotas !== 'object' || Array.isArray(base.propertyQuotas)) base.propertyQuotas = {}; } return base; },
            mountSortable() { if (!window.Sortable) return; if (this.sortable) this.sortable.destroy(); const el = document.getElementById('global-section-list'); if (!el) return; this.sortable = Sortable.create(el, { animation: 160, handle: '.home-drag-handle', ghostClass: 'dragging', onEnd: async e => { if (e.oldIndex === e.newIndex) return; const moved = this.sections.splice(e.oldIndex, 1)[0]; this.sections.splice(e.newIndex, 0, moved); try { await DeLongApi.put('/api/admin/site/global/sections/reorder', { ids: this.sections.map(x => x.id) }); this.notify('Đã cập nhật thứ tự.'); } catch (err) { this.notify(err.message || 'Không thể đổi thứ tự.', 'error'); } } }); },
            openCreate() { const type = 'Hero'; this.form = { type, name: '', variant: this.variantsFor(type)[0], isVisible: true, content: defaultContent(type), itemsText: '' }; this.editor = { open: true, mode: 'create', id: null }; },
            openEdit(section) { let content = {}; try { content = JSON.parse(section.contentJson || '{}'); } catch (_) {} this.form = { type: section.type, name: section.name, variant: section.variant, isVisible: section.isVisible, content: this.normalizeContent(section.type, content), itemsText: Array.isArray(content.items) ? content.items.join('\n') : '' }; this.editor = { open: true, mode: 'edit', id: section.id }; },
            closeEditor() { if (!this.saving) this.editor.open = false; },
            typeChanged() { const type = this.form.type; this.form.content = defaultContent(type); this.form.variant = this.variantsFor(type)[0]; this.form.itemsText = ''; },
            payload() { const content = Object.assign({}, this.form.content); if (this.form.type === 'FeatureGrid') content.items = this.form.itemsText.split('\n').map(x => x.trim()).filter(Boolean); return { type: this.form.type, name: this.form.name, variant: this.form.variant, isVisible: this.form.isVisible, contentJson: JSON.stringify(content) }; },
            async saveSection() { if (this.saving) return; this.saving = true; try { const payload = this.payload(); const saved = this.editor.mode === 'create' ? await DeLongApi.post('/api/admin/site/global/sections', payload) : await DeLongApi.put(`/api/admin/site/global/sections/${this.editor.id}`, payload); if (this.editor.mode === 'create') this.sections.push(saved); else { const i = this.sections.findIndex(x => x.id === saved.id); if (i >= 0) this.sections.splice(i, 1, saved); } this.editor.open = false; this.notify('Đã lưu khối.'); nextTick(() => this.mountSortable()); } catch (err) { this.notify(err.message || 'Không thể lưu khối.', 'error'); } finally { this.saving = false; } },
            async toggleSection(section) { try { const saved = await DeLongApi.put(`/api/admin/site/global/sections/${section.id}`, { type: section.type, name: section.name, variant: section.variant, contentJson: section.contentJson || '{}', isVisible: !section.isVisible }); Object.assign(section, saved); } catch (err) { this.notify(err.message || 'Không thể đổi trạng thái.', 'error'); } },
            async deleteSection() { if (!this.editor.id || !window.confirm('Xóa khối này khỏi trang chủ chung?')) return; this.saving = true; try { await DeLongApi.delete(`/api/admin/site/global/sections/${this.editor.id}`); this.sections = this.sections.filter(x => x.id !== this.editor.id); this.editor.open = false; this.notify('Đã xóa khối.'); nextTick(() => this.mountSortable()); } catch (err) { this.notify(err.message || 'Không thể xóa khối.', 'error'); } finally { this.saving = false; } },
            notify(message, type = 'success') { this.toast = { show: true, message, type }; clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2800); }
        }
    }).mount(root);
})();
