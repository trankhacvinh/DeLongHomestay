(function () {
    const root = document.getElementById('room-content-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('room-content-data').textContent || '{}');
    const { createApp } = Vue;
    let quill = null;
    let guestGuideQuill = null;
    let gallerySortable = null;

    createApp({
        data() {
            const room = initial.room || {};
            const form = {
                code: room.code || '',
                name: room.name || '',
                capacity: Number(room.capacity || 2),
                slug: room.slug || '',
                shortDescription: room.shortDescription || '',
                descriptionHtml: room.descriptionHtml || '',
                guestGuideHtml: room.guestGuideHtml || '',
                isPublished: room.isPublished === true,
                amenities: [...(room.amenities || [])],
                tags: [...(room.tags || [])],
                highlights: [...(room.highlights || [])]
            };
            return {
                propertyId: initial.propertyId,
                room: { ...room, images: [...(room.images || [])] },
                amenityCatalog: [...(initial.amenityCatalog || [])],
                amenityPresets: [...(initial.amenityPresets || [])],
                form,
                baseline: JSON.stringify(form),
                drafts: { amenity: '', tag: '' },
                saving: false,
                uploading: false,
                editorExpanded: false,
                mediaPicker: { open: false },
                focalEditor: { open: false, image: null, x: 0.5, y: 0.5 },
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            dirty() {
                return JSON.stringify(this.form) !== this.baseline;
            },
            availableAmenitySuggestions() {
                const selected = new Set(this.form.amenities.map(x => x.toLocaleLowerCase('vi')));
                return this.amenityCatalog.filter(x => !selected.has(x.toLocaleLowerCase('vi')));
            },
            focalMarkerStyle() {
                return { left: `${this.focalEditor.x * 100}%`, top: `${this.focalEditor.y * 100}%` };
            },
            focalDraftImageStyle() {
                return { objectPosition: `${this.focalEditor.x * 100}% ${this.focalEditor.y * 100}%` };
            }
        },
        methods: {
            normalizeList(values) {
                return (values || []).map(x => String(x || '').trim()).filter(Boolean);
            },
            initQuill() {
                if (!this.$refs.quillEditor || typeof Quill === 'undefined') return;
                quill = new Quill(this.$refs.quillEditor, {
                    theme: 'snow',
                    placeholder: 'Viết câu chuyện về căn phòng, phong cách không gian, trải nghiệm phù hợp và những điều khách nên biết...',
                    modules: {
                        toolbar: {
                            container: [
                                [{ header: [2, 3, false] }],
                                ['bold', 'italic', 'blockquote'],
                                [{ list: 'ordered' }, { list: 'bullet' }],
                                ['link', 'image', 'video'],
                                ['clean']
                            ],
                            handlers: {
                                image: () => this.openMediaPicker(),
                                video: () => this.insertYouTubeVideo()
                            }
                        }
                    }
                });
                if (this.form.descriptionHtml) {
                    quill.clipboard.dangerouslyPasteHTML(this.form.descriptionHtml, 'silent');
                }
                quill.on('text-change', () => {
                    const html = quill.root.innerHTML;
                    this.form.descriptionHtml = html === '<p><br></p>' ? '' : html;
                });
                this.initGuestGuideQuill();
            },
            initGuestGuideQuill() {
                if (!this.$refs.guestGuideEditor || typeof Quill === 'undefined' || guestGuideQuill) return;
                guestGuideQuill = new Quill(this.$refs.guestGuideEditor, {
                    theme: 'snow',
                    placeholder: 'Ví dụ: Cách nhận phòng, sử dụng khóa cửa, Wi-Fi, máy lạnh, nước nóng, thời gian yên tĩnh và quy trình trả phòng...',
                    modules: {
                        toolbar: [
                            [{ header: [2, 3, false] }],
                            ['bold', 'italic', 'blockquote'],
                            [{ list: 'ordered' }, { list: 'bullet' }],
                            ['link'],
                            ['clean']
                        ]
                    }
                });
                if (this.form.guestGuideHtml) guestGuideQuill.clipboard.dangerouslyPasteHTML(this.form.guestGuideHtml, 'silent');
                guestGuideQuill.on('text-change', () => {
                    const html = guestGuideQuill.root.innerHTML;
                    this.form.guestGuideHtml = html === '<p><br></p>' ? '' : html;
                });
            },
            toggleEditorExpanded() {
                this.editorExpanded = !this.editorExpanded;
                document.body.classList.toggle('room-editor-expanded', this.editorExpanded);
                this.$nextTick(() => quill?.focus());
            },
            closeExpandedEditor() {
                if (!this.editorExpanded) return;
                this.editorExpanded = false;
                document.body.classList.remove('room-editor-expanded');
            },
            handleEditorKeydown(event) {
                if (event.key === 'Escape' && this.editorExpanded) this.closeExpandedEditor();
            },
            openMediaPicker() {
                this.mediaPicker.open = true;
            },
            closeMediaPicker() {
                this.mediaPicker.open = false;
            },
            insertGalleryImage(image) {
                if (!quill) return;
                const selection = quill.getSelection(true);
                const index = selection ? selection.index : quill.getLength();
                quill.insertEmbed(index, 'image', image.largeUrl, 'user');
                quill.setSelection(index + 1, 0, 'silent');
                const candidates = Array.from(quill.root.querySelectorAll('img'));
                const inserted = candidates.reverse().find(node => node.getAttribute('src') === image.largeUrl);
                if (inserted && image.altText) inserted.setAttribute('alt', image.altText);
                this.form.descriptionHtml = quill.root.innerHTML;
                this.closeMediaPicker();
            },
            insertYouTubeVideo() {
                if (!quill) return;
                const raw = window.prompt('Dán URL YouTube (youtube.com hoặc youtu.be)');
                if (!raw) return;
                const embedUrl = this.toYouTubeEmbed(raw.trim());
                if (!embedUrl) return this.notify('URL YouTube không hợp lệ.', 'error');
                const selection = quill.getSelection(true);
                const index = selection ? selection.index : quill.getLength();
                quill.insertEmbed(index, 'video', embedUrl, 'user');
                quill.setSelection(index + 1, 0, 'silent');
                this.form.descriptionHtml = quill.root.innerHTML;
            },
            toYouTubeEmbed(value) {
                try {
                    const url = new URL(value);
                    const host = url.hostname.toLowerCase().replace(/^www\./, '');
                    let id = '';
                    if (host === 'youtu.be') id = url.pathname.split('/').filter(Boolean)[0] || '';
                    if (host === 'youtube.com' || host === 'm.youtube.com') {
                        if (url.pathname === '/watch') id = url.searchParams.get('v') || '';
                        else {
                            const parts = url.pathname.split('/').filter(Boolean);
                            if (['shorts', 'embed', 'live'].includes(parts[0])) id = parts[1] || '';
                        }
                    }
                    if (!/^[A-Za-z0-9_-]{6,20}$/.test(id)) return null;
                    return `https://www.youtube-nocookie.com/embed/${encodeURIComponent(id)}`;
                } catch {
                    return null;
                }
            },
            addItem(target, draftKey) {
                const value = String(this.drafts[draftKey] || '').trim();
                if (!value) return;
                if (this.form[target].some(x => x.toLocaleLowerCase('vi') === value.toLocaleLowerCase('vi'))) {
                    this.drafts[draftKey] = '';
                    return;
                }
                const limit = target === 'amenities' ? 30 : 20;
                if (this.form[target].length >= limit) return this.notify(`Mỗi nhóm tối đa ${limit} mục.`, 'error');
                this.form[target].push(value);
                if (target === 'amenities' && !this.amenityCatalog.some(x => x.toLocaleLowerCase('vi') === value.toLocaleLowerCase('vi'))) this.amenityCatalog.push(value);
                this.drafts[draftKey] = '';
            },
            addAmenity(value) {
                if (this.form.amenities.some(x => x.toLocaleLowerCase('vi') === value.toLocaleLowerCase('vi'))) return;
                if (this.form.amenities.length >= 30) return this.notify('Tối đa 30 tiện nghi.', 'error');
                this.form.amenities.push(value);
            },
            removeItem(target, index) {
                this.form[target].splice(index, 1);
            },
            applyAmenityPreset(preset) {
                const current = new Set(this.form.amenities.map(x => x.toLocaleLowerCase('vi')));
                let added = 0;
                for (const item of preset.amenities || []) {
                    if (current.has(item.toLocaleLowerCase('vi'))) continue;
                    if (this.form.amenities.length >= 30) break;
                    this.form.amenities.push(item);
                    current.add(item.toLocaleLowerCase('vi'));
                    added++;
                }
                this.notify(added ? `Đã thêm ${added} tiện nghi từ bộ “${preset.name}”.` : `Phòng đã có đủ tiện nghi của bộ “${preset.name}”.`, 'success');
            },
            async saveAmenityPreset() {
                const amenities = this.normalizeList(this.form.amenities);
                if (!amenities.length) return;
                const rawName = window.prompt('Tên bộ tiện nghi dùng lại', 'Tiện nghi tiêu chuẩn');
                if (!rawName || !rawName.trim()) return;
                try {
                    const preset = await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/amenity-presets`,
                        { name: rawName.trim(), amenities });
                    this.amenityPresets.push(preset);
                    this.notify('Đã lưu bộ tiện nghi để dùng cho các phòng khác.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu bộ tiện nghi.', 'error');
                }
            },
            async deleteAmenityPreset(preset) {
                if (!window.confirm(`Xóa bộ tiện nghi “${preset.name}”? Các phòng đã áp dụng sẽ không bị thay đổi.`)) return;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/amenity-presets/${preset.id}`);
                    this.amenityPresets = this.amenityPresets.filter(x => x.id !== preset.id);
                    this.notify('Đã xóa bộ tiện nghi.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể xóa bộ tiện nghi.', 'error');
                }
            },
            async saveContent() {
                if (this.saving) return;
                const code = String(this.form.code || '').trim().toUpperCase();
                const name = String(this.form.name || '').trim();
                const capacity = Number(this.form.capacity);
                if (!code) return this.notify('Mã phòng là bắt buộc.', 'error');
                if (!name) return this.notify('Tên phòng là bắt buộc.', 'error');
                if (!Number.isInteger(capacity) || capacity < 1 || capacity > 50) return this.notify('Sức chứa phải từ 1 đến 50 người.', 'error');

                const highlights = this.normalizeList(this.form.highlights);
                if (highlights.length > 8) return this.notify('Tối đa 8 điểm nổi bật.', 'error');
                if (quill) {
                    const html = quill.root.innerHTML;
                    this.form.descriptionHtml = html === '<p><br></p>' ? '' : html;
                }
                if (guestGuideQuill) {
                    const html = guestGuideQuill.root.innerHTML;
                    this.form.guestGuideHtml = html === '<p><br></p>' ? '' : html;
                }
                this.form.code = code;
                this.form.name = name;
                this.form.capacity = capacity;
                this.saving = true;
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content`,
                        {
                            code,
                            name,
                            capacity,
                            slug: this.form.slug || null,
                            shortDescription: this.form.shortDescription || null,
                            descriptionHtml: this.form.descriptionHtml || null,
                            guestGuideHtml: this.form.guestGuideHtml || null,
                            isPublished: this.form.isPublished,
                            amenities: this.normalizeList(this.form.amenities),
                            tags: this.normalizeList(this.form.tags),
                            highlights
                        });
                    this.applyContent(updated);
                    this.notify('Đã lưu thông tin và nội dung phòng.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu nội dung phòng.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            applyContent(updated) {
                const existingImages = this.room.images || [];
                this.room = { ...this.room, ...updated, images: [...(updated.images || existingImages)] };
                this.form = {
                    code: updated.code || '',
                    name: updated.name || '',
                    capacity: Number(updated.capacity || 2),
                    slug: updated.slug || '',
                    shortDescription: updated.shortDescription || '',
                    descriptionHtml: updated.descriptionHtml || '',
                    guestGuideHtml: updated.guestGuideHtml || '',
                    isPublished: updated.isPublished === true,
                    amenities: [...(updated.amenities || [])],
                    tags: [...(updated.tags || [])],
                    highlights: [...(updated.highlights || [])]
                };
                this.baseline = JSON.stringify(this.form);
                this.$nextTick(() => {
                    if (quill && quill.root.innerHTML !== (this.form.descriptionHtml || '<p><br></p>')) {
                        quill.clipboard.dangerouslyPasteHTML(this.form.descriptionHtml || '', 'silent');
                    }
                    if (guestGuideQuill && guestGuideQuill.root.innerHTML !== (this.form.guestGuideHtml || '<p><br></p>')) {
                        guestGuideQuill.clipboard.dangerouslyPasteHTML(this.form.guestGuideHtml || '', 'silent');
                    }
                    this.initGallerySortable();
                });
            },
            async uploadImages(event) {
                const files = Array.from(event.target.files || []);
                event.target.value = '';
                if (!files.length || this.uploading) return;
                const available = Math.max(0, 20 - this.room.images.length);
                if (files.length > available) return this.notify(`Chỉ có thể tải thêm ${available} ảnh.`, 'error');

                this.uploading = true;
                let uploaded = 0;
                try {
                    for (const file of files) {
                        const formData = new FormData();
                        formData.append('file', file);
                        const image = await DeLongApi.postForm(
                            `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images`,
                            formData);
                        this.room.images.push(image);
                        uploaded++;
                    }
                    this.room.images.sort((a, b) => a.sortOrder - b.sortOrder);
                    await this.$nextTick();
                    this.initGallerySortable();
                    this.notify(`Đã xử lý ${uploaded} ảnh và tạo bản tối ưu.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể tải ảnh.', 'error');
                } finally {
                    this.uploading = false;
                }
            },
            initGallerySortable() {
                if (gallerySortable) {
                    gallerySortable.destroy();
                    gallerySortable = null;
                }
                if (!this.$refs.galleryGrid || typeof Sortable === 'undefined') return;
                gallerySortable = new Sortable(this.$refs.galleryGrid, {
                    animation: 160,
                    handle: '.gallery-drag-handle',
                    ghostClass: 'room-gallery-tile-ghost',
                    dragClass: 'room-gallery-tile-dragging',
                    onEnd: event => this.handleGalleryReorder(event)
                });
            },
            async handleGalleryReorder(event) {
                const oldIndex = event.oldIndex;
                const newIndex = event.newIndex;
                if (oldIndex == null || newIndex == null || oldIndex === newIndex) return;
                const previous = [...this.room.images];
                const [moved] = this.room.images.splice(oldIndex, 1);
                this.room.images.splice(newIndex, 0, moved);
                this.room.images.forEach((image, index) => image.sortOrder = index);
                try {
                    await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/reorder`,
                        { imageIds: this.room.images.map(x => x.id) });
                    this.notify('Đã cập nhật thứ tự gallery.', 'success');
                } catch (error) {
                    this.room.images = previous;
                    await this.$nextTick();
                    this.initGallerySortable();
                    this.notify(error.message || 'Không thể sắp xếp ảnh.', 'error');
                }
            },
            async saveImageMeta(image) {
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`,
                        { altText: image.altText || null, isCover: image.isCover === true, focalX: image.focalX, focalY: image.focalY });
                    Object.assign(image, updated);
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu thông tin ảnh.', 'error');
                }
            },
            async setCover(image) {
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`,
                        { altText: image.altText || null, isCover: true, focalX: image.focalX, focalY: image.focalY });
                    this.room.images.forEach(item => { item.isCover = item.id === image.id; });
                    Object.assign(image, updated);
                    this.notify('Đã đổi ảnh bìa.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể đổi ảnh bìa.', 'error');
                }
            },
            openFocalEditor(image) {
                this.focalEditor = { open: true, image, x: Number(image.focalX ?? 0.5), y: Number(image.focalY ?? 0.5) };
            },
            closeFocalEditor() {
                this.focalEditor = { open: false, image: null, x: 0.5, y: 0.5 };
            },
            setFocalFromClick(event) {
                const rect = event.currentTarget.getBoundingClientRect();
                if (!rect.width || !rect.height) return;
                this.focalEditor.x = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
                this.focalEditor.y = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
            },
            async saveFocalPoint() {
                const image = this.focalEditor.image;
                if (!image) return;
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`,
                        { altText: image.altText || null, isCover: image.isCover === true, focalX: this.focalEditor.x, focalY: this.focalEditor.y });
                    Object.assign(image, updated);
                    const version = Date.now();
                    image.thumbnailUrl = `${updated.thumbnailUrl}?v=${version}`;
                    image.cardUrl = `${updated.cardUrl}?v=${version}`;
                    this.closeFocalEditor();
                    this.notify('Đã tạo lại thumbnail theo điểm ảnh.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể cập nhật điểm ảnh.', 'error');
                }
            },
            focalStyle(image) {
                return { '--focal-x': `${Number(image.focalX ?? 0.5) * 100}%`, '--focal-y': `${Number(image.focalY ?? 0.5) * 100}%` };
            },
            async deleteImage(image) {
                if (!window.confirm(`Xóa ảnh này khỏi ${this.room.name}?`)) return;
                try {
                    await DeLongApi.delete(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`);
                    this.room.images = this.room.images.filter(x => x.id !== image.id);
                    if (image.isCover && this.room.images.length) this.room.images[0].isCover = true;
                    await this.$nextTick();
                    this.initGallerySortable();
                    this.notify('Đã xóa ảnh.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể xóa ảnh.', 'error');
                }
            },
            fileSize(bytes) {
                const value = Number(bytes || 0);
                if (value < 1024 * 1024) return `${Math.max(1, Math.round(value / 1024))} KB`;
                return `${(value / (1024 * 1024)).toFixed(1)} MB`;
            },
            notify(message, type) {
                if (this.toast.timer) clearTimeout(this.toast.timer);
                const timer = setTimeout(() => { this.toast.show = false; }, 4000);
                this.toast = { show: true, message, type, timer };
            }
        },
        mounted() {
            this.$nextTick(() => {
                this.initQuill();
                this.initGallerySortable();
            });
            document.addEventListener('keydown', this.handleEditorKeydown);
            window.addEventListener('beforeunload', event => {
                if (!this.dirty) return;
                event.preventDefault();
                event.returnValue = '';
            });
        },
        beforeUnmount() {
            document.removeEventListener('keydown', this.handleEditorKeydown);
            document.body.classList.remove('room-editor-expanded');
            if (gallerySortable) gallerySortable.destroy();
            gallerySortable = null;
            quill = null;
        }
    }).mount(root);
})();
