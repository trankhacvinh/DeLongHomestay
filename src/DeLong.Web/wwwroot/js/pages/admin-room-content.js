(function () {
    const root = document.getElementById('room-content-page');
    if (!root) return;

    const initial = JSON.parse(document.getElementById('room-content-data').textContent || '{}');
    const { createApp } = Vue;

    createApp({
        data() {
            const room = initial.room || {};
            const form = {
                slug: room.slug || '',
                shortDescription: room.shortDescription || '',
                descriptionHtml: room.descriptionHtml || '',
                isPublished: room.isPublished === true,
                amenities: [...(room.amenities || [])],
                tags: [...(room.tags || [])],
                highlights: [...(room.highlights || [])]
            };
            return {
                propertyId: initial.propertyId,
                room: { ...room, images: [...(room.images || [])] },
                form,
                baseline: JSON.stringify(form),
                drafts: { amenity: '', tag: '' },
                saving: false,
                uploading: false,
                toast: { show: false, message: '', type: 'success', timer: null }
            };
        },
        computed: {
            dirty() {
                return JSON.stringify(this.form) !== this.baseline;
            }
        },
        methods: {
            normalizeList(values) {
                return (values || []).map(x => String(x || '').trim()).filter(Boolean);
            },
            captureDescription(event) {
                this.form.descriptionHtml = event.currentTarget.innerHTML;
            },
            focusEditor() {
                this.$refs.richEditor?.focus();
            },
            formatText(command) {
                this.focusEditor();
                document.execCommand(command, false, null);
                this.form.descriptionHtml = this.$refs.richEditor?.innerHTML || '';
            },
            formatBlock(tag) {
                this.focusEditor();
                document.execCommand('formatBlock', false, tag);
                this.form.descriptionHtml = this.$refs.richEditor?.innerHTML || '';
            },
            createLink() {
                const url = window.prompt('Nhập đường dẫn liên kết (https://...)');
                if (!url) return;
                const value = url.trim();
                if (!/^https?:\/\//i.test(value) && !/^mailto:/i.test(value)) {
                    return this.notify('Liên kết phải bắt đầu bằng http://, https:// hoặc mailto:.', 'error');
                }
                this.focusEditor();
                document.execCommand('createLink', false, value);
                this.form.descriptionHtml = this.$refs.richEditor?.innerHTML || '';
            },
            addItem(target, draftKey) {
                const value = String(this.drafts[draftKey] || '').trim();
                if (!value) return;
                if (this.form[target].some(x => x.toLocaleLowerCase('vi') === value.toLocaleLowerCase('vi'))) {
                    this.drafts[draftKey] = '';
                    return;
                }
                if (this.form[target].length >= 20) return this.notify('Mỗi nhóm tối đa 20 mục.', 'error');
                this.form[target].push(value);
                this.drafts[draftKey] = '';
            },
            removeItem(target, index) {
                this.form[target].splice(index, 1);
            },
            async saveContent() {
                if (this.saving) return;
                const highlights = this.normalizeList(this.form.highlights);
                if (highlights.length > 8) return this.notify('Tối đa 8 điểm nổi bật.', 'error');
                this.saving = true;
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content`,
                        {
                            slug: this.form.slug || null,
                            shortDescription: this.form.shortDescription || null,
                            descriptionHtml: this.form.descriptionHtml || null,
                            isPublished: this.form.isPublished,
                            amenities: this.normalizeList(this.form.amenities),
                            tags: this.normalizeList(this.form.tags),
                            highlights
                        });
                    this.applyContent(updated);
                    this.notify('Đã lưu nội dung phòng.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu nội dung phòng.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            applyContent(updated) {
                this.room = { ...this.room, ...updated, images: [...(updated.images || this.room.images || [])] };
                this.form = {
                    slug: updated.slug || '',
                    shortDescription: updated.shortDescription || '',
                    descriptionHtml: updated.descriptionHtml || '',
                    isPublished: updated.isPublished === true,
                    amenities: [...(updated.amenities || [])],
                    tags: [...(updated.tags || [])],
                    highlights: [...(updated.highlights || [])]
                };
                this.baseline = JSON.stringify(this.form);
                this.$nextTick(() => {
                    if (this.$refs.richEditor && this.$refs.richEditor.innerHTML !== this.form.descriptionHtml) {
                        this.$refs.richEditor.innerHTML = this.form.descriptionHtml;
                    }
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
                    this.sortImages();
                    this.notify(`Đã xử lý ${uploaded} ảnh và tạo thumbnail.`, 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể tải ảnh.', 'error');
                } finally {
                    this.uploading = false;
                }
            },
            sortImages() {
                this.room.images.sort((a, b) => Number(b.isCover) - Number(a.isCover) || a.sortOrder - b.sortOrder);
            },
            async saveImageMeta(image) {
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`,
                        { altText: image.altText || null, isCover: image.isCover === true });
                    Object.assign(image, updated);
                } catch (error) {
                    this.notify(error.message || 'Không thể lưu thông tin ảnh.', 'error');
                }
            },
            async setCover(image) {
                try {
                    const updated = await DeLongApi.put(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`,
                        { altText: image.altText || null, isCover: true });
                    this.room.images.forEach(item => { item.isCover = item.id === image.id; });
                    Object.assign(image, updated);
                    this.sortImages();
                    this.notify('Đã đổi ảnh bìa.', 'success');
                } catch (error) {
                    this.notify(error.message || 'Không thể đổi ảnh bìa.', 'error');
                }
            },
            async moveImage(index, amount) {
                const target = index + amount;
                if (target < 0 || target >= this.room.images.length) return;
                const images = this.room.images;
                [images[index], images[target]] = [images[target], images[index]];
                images.forEach((image, i) => image.sortOrder = i);
                try {
                    await DeLongApi.post(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/reorder`,
                        { imageIds: images.map(x => x.id) });
                } catch (error) {
                    [images[index], images[target]] = [images[target], images[index]];
                    images.forEach((image, i) => image.sortOrder = i);
                    this.notify(error.message || 'Không thể sắp xếp ảnh.', 'error');
                }
            },
            async deleteImage(image) {
                if (!window.confirm(`Xóa ảnh này khỏi ${this.room.name}?`)) return;
                try {
                    await DeLongApi.delete(
                        `/api/admin/properties/${this.propertyId}/rooms/${this.room.roomId}/content/images/${image.id}`);
                    this.room.images = this.room.images.filter(x => x.id !== image.id);
                    if (image.isCover && this.room.images.length) this.room.images[0].isCover = true;
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
            window.addEventListener('beforeunload', event => {
                if (!this.dirty) return;
                event.preventDefault();
                event.returnValue = '';
            });
        }
    }).mount(root);
})();
