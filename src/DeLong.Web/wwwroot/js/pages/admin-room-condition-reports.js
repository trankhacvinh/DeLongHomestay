(function () {
    const root = document.getElementById('room-condition-page');
    if (!root) return;
    const initial = JSON.parse(document.getElementById('room-condition-page-data').textContent || '{}');
    const { createApp } = Vue;
    const emptyForm = () => ({ open:false, roomId:'', inspectionType:'Routine', severity:'Normal', rating:5, content:'', tags:[], media:[], saving:false });

    async function optimizeImage(file) {
        if (!/^image\/(jpeg|png|webp)$/i.test(file.type)) return file;
        const bitmap = await createImageBitmap(file, { imageOrientation:'from-image' });
        try {
            const scale = Math.min(1, 1920 / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.max(1, Math.round(bitmap.width * scale));
            canvas.height = Math.max(1, Math.round(bitmap.height * scale));
            canvas.getContext('2d', { alpha:false }).drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .82));
            if (!blob || blob.size >= file.size) return file;
            return new File([blob], `${file.name.replace(/\.[^.]+$/, '') || 'anh-phong'}.jpg`, { type:'image/jpeg', lastModified:Date.now() });
        } finally { bitmap.close(); }
    }

    createApp({
        data: () => ({ propertyId:initial.propertyId, rooms:initial.rooms || [], reports:initial.conditionReports || [], tags:initial.conditionTags || [], timeZone:initial.timeZoneId || 'Asia/Ho_Chi_Minh', filters:{ roomId:initial.selectedRoomId || '', status:'' }, expandedId:null, form:emptyForm(), canManage:window.DeLongRoomConditionCanManage === true, canEditTemplates:window.DeLongRoomConditionCanEditTemplates === true, templateEditor:{ open:false, items:[], name:'', category:'', creating:false, savingId:null, deleteId:null }, toast:{ show:false, message:'', type:'success', timer:null } }),
        computed: {
            filteredReports() { return this.reports.filter(x => (!this.filters.roomId || x.roomId === this.filters.roomId) && (this.filters.status === '' || String(x.status) === this.filters.status)); },
            tagGroups() { const groups = new Map(); for (const tag of this.tags) { const category = tag.category || 'Khác'; if (!groups.has(category)) groups.set(category, []); groups.get(category).push(tag); } return Array.from(groups, ([category,tags]) => ({category,tags})); }
        },
        methods: {
            openForm() { const roomId = this.filters.roomId || ''; this.form = emptyForm(); this.form.open = true; this.form.roomId = roomId; },
            closeForm() { if (this.form.saving) return; this.form.media.forEach(x => URL.revokeObjectURL(x.previewUrl)); this.form = emptyForm(); },
            async addMedia(event) {
                const files = Array.from(event.target.files || []); event.target.value = '';
                for (const source of files) {
                    if (!source.type.startsWith('image/') && !source.type.startsWith('video/')) { this.notify(`Không hỗ trợ ${source.name}.`, 'error'); continue; }
                    if (source.type.startsWith('video/') && source.size > 250 * 1024 * 1024) { this.notify(`${source.name} vượt quá 250 MB.`, 'error'); continue; }
                    const item = { file:source, isVideo:source.type.startsWith('video/'), previewUrl:URL.createObjectURL(source), optimizing:source.type.startsWith('image/') };
                    this.form.media.push(item);
                    if (!item.isVideo) {
                        try { const optimized = await optimizeImage(source); if (optimized !== source) { URL.revokeObjectURL(item.previewUrl); item.file = optimized; item.previewUrl = URL.createObjectURL(optimized); } }
                        catch { /* Server vẫn xác thực và tối ưu ảnh. */ }
                        finally { item.optimizing = false; }
                    }
                }
            },
            removeMedia(index) { const [item] = this.form.media.splice(index, 1); if (item) URL.revokeObjectURL(item.previewUrl); },
            toggleTag(name) { const index = this.form.tags.indexOf(name); if (index >= 0) this.form.tags.splice(index, 1); else this.form.tags.push(name); this.form.content = this.form.tags.join('\n'); },
            toggleTemplateEditor() { this.templateEditor.open = !this.templateEditor.open; this.templateEditor.items = this.templateEditor.open ? this.tags.map(x => ({...x})) : []; this.templateEditor.deleteId = null; },
            async createTemplate() {
                if (!this.templateEditor.category || !this.templateEditor.name) return this.notify('Nhập đủ nhóm và nội dung mẫu.', 'error');
                this.templateEditor.creating = true;
                try {
                    const tag = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/housekeeping/report-tags`, { name:this.templateEditor.name, category:this.templateEditor.category });
                    this.tags.push(tag); this.templateEditor.items.push({...tag}); this.templateEditor.name = ''; this.notify('Đã thêm nội dung mẫu.', 'success');
                } catch (error) { this.notify(error.message || 'Không thể thêm nội dung mẫu.', 'error'); }
                finally { this.templateEditor.creating = false; }
            },
            async saveTemplate(tag) {
                if (!tag.category?.trim() || !tag.name?.trim()) return this.notify('Nhập đủ nhóm và nội dung mẫu.', 'error');
                this.templateEditor.savingId = tag.id;
                try {
                    const updated = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/housekeeping/report-tags/${tag.id}`, { name:tag.name, category:tag.category });
                    const index = this.tags.findIndex(x => x.id === tag.id); const oldName = index >= 0 ? this.tags[index].name : '';
                    if (index >= 0) this.tags.splice(index, 1, updated);
                    const selectedIndex = this.form.tags.indexOf(oldName); if (selectedIndex >= 0) { this.form.tags.splice(selectedIndex, 1, updated.name); this.form.content = this.form.tags.join('\n'); }
                    this.notify('Đã cập nhật nội dung mẫu.', 'success');
                } catch (error) { this.notify(error.message || 'Không thể cập nhật nội dung mẫu.', 'error'); }
                finally { this.templateEditor.savingId = null; }
            },
            async deleteTemplate(tag) {
                if (this.templateEditor.deleteId !== tag.id) { this.templateEditor.deleteId = tag.id; return; }
                this.templateEditor.savingId = tag.id;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/housekeeping/report-tags/${tag.id}`);
                    const current = this.tags.find(x => x.id === tag.id); this.tags = this.tags.filter(x => x.id !== tag.id); this.templateEditor.items = this.templateEditor.items.filter(x => x.id !== tag.id); this.form.tags = this.form.tags.filter(x => x !== current?.name); this.form.content = this.form.tags.join('\n'); this.templateEditor.deleteId = null;
                    this.notify('Đã xóa nội dung mẫu.', 'success');
                } catch (error) { this.notify(error.message || 'Không thể xóa nội dung mẫu.', 'error'); }
                finally { this.templateEditor.savingId = null; }
            },
            async submit() {
                if (!this.form.roomId) return this.notify('Vui lòng chọn phòng.', 'error');
                if (!this.form.media.length) return this.notify('Vui lòng chọn ít nhất một ảnh hoặc video.', 'error');
                if (!this.form.content.trim()) return this.notify('Vui lòng nhập hoặc chọn nội dung mẫu.', 'error');
                this.form.saving = true;
                try {
                    const data = new FormData(); data.append('roomId', this.form.roomId); data.append('inspectionType', this.form.inspectionType); data.append('severity', this.form.severity); data.append('rating', String(this.form.rating)); data.append('content', this.form.content.trim());
                    this.form.tags.forEach(tag => data.append('tags', tag)); this.form.media.forEach(media => data.append('files', media.file, media.file.name));
                    const report = await DeLongApi.postForm(`/api/admin/properties/${this.propertyId}/housekeeping/reports`, data);
                    this.reports.unshift(report); this.expandedId = report.id; this.form.saving = false; this.closeForm(); this.notify('Đã lưu báo cáo tình trạng phòng.', 'success');
                } catch (error) { this.form.saving = false; this.notify(error.message || 'Không thể gửi báo cáo.', 'error'); }
            },
            toggle(id) { this.expandedId = this.expandedId === id ? null : id; },
            async changeStatus(report,status) { try { const updated = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/housekeeping/reports/${report.id}/status`, {status}); this.reports.splice(this.reports.findIndex(x => x.id === report.id), 1, updated); this.notify('Đã cập nhật trạng thái.', 'success'); } catch(error) { this.notify(error.message || 'Không thể cập nhật trạng thái.', 'error'); } },
            dateText(value) { return new Intl.DateTimeFormat('vi-VN', { timeZone:this.timeZone, dateStyle:'short', timeStyle:'short' }).format(new Date(value)); },
            inspectionText(v) { return ({0:'Trước nhận phòng',1:'Sau trả phòng',2:'Định kỳ',3:'Sự cố'})[v] || 'Kiểm tra'; },
            severityText(v) { return ({0:'Bình thường',1:'Theo dõi',2:'Khẩn cấp'})[v] || 'Bình thường'; },
            severityClass(v) { return v === 2 ? 'danger' : (v === 1 ? 'warning' : 'success'); },
            statusText(v) { return ({0:'Mới báo',1:'Đang xử lý',2:'Đã hoàn thành'})[v] || 'Mới báo'; },
            statusClass(v) { return v === 2 ? 'success' : (v === 1 ? 'warning' : ''); },
            stars(value) { return '★'.repeat(value) + '☆'.repeat(5-value); },
            bytes(value) { return value < 1048576 ? `${Math.max(1,Math.round(value/1024))} KB` : `${(value/1048576).toFixed(1)} MB`; },
            notify(message,type) { if (this.toast.timer) clearTimeout(this.toast.timer); this.toast = { show:true, message, type, timer:setTimeout(() => { this.toast.show=false; }, 3500) }; }
        }
    }).mount(root);
})();
