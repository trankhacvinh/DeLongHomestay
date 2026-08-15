(function () {
    const root = document.getElementById('editorial-admin');
    if (!root || !window.Vue) return;
    const initial = JSON.parse(document.getElementById('editorial-admin-data')?.textContent || '{}');
    const { createApp } = Vue;
    let postQuill = null;

    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                propertyName: initial.propertyName || '',
                siteSlug: initial.siteSlug || '',
                isAdmin: initial.isAdmin === true,
                tab: initial.tab === 'blog' ? 'blog' : 'gallery',
                gallery: [],
                posts: [],
                galleryLayout: 'mosaic',
                globalGalleryLayout: 'mosaic',
                globalSettings: null,
                loading: true,
                uploading: false,
                uploadingCover: false,
                uploadingBody: false,
                saving: false,
                globalSaving: false,
                postEditor: { open: false, id: null },
                postForm: { slug: '', title: '', excerpt: '', bodyHtml: '', coverImageUrl: '', isPublished: false },
                toast: { show: false, type: 'success', message: '' }
            };
        },
        mounted() { this.load(); },
        methods: {
            normalizeLayout(value) {
                return ['mosaic', 'grid', 'slider'].includes(value) ? value : 'mosaic';
            },
            async load() {
                this.loading = true;
                try {
                    const data = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/editorial/`);
                    this.gallery = data.gallery || [];
                    this.posts = data.posts || [];
                    this.galleryLayout = this.normalizeLayout(data.galleryLayout);

                    if (this.isAdmin && this.tab === 'gallery') {
                        try {
                            const globalData = await DeLongApi.get('/api/admin/site/global/editorial/');
                            this.globalSettings = globalData.settings || null;
                            this.globalGalleryLayout = this.normalizeLayout(this.globalSettings?.galleryLayout);
                        } catch (globalError) {
                            this.notify(globalError.message || 'Không thể tải kiểu Gallery trang chủ chung.', 'error');
                        }
                    }
                } catch (err) {
                    this.notify(err.message || 'Không thể tải nội dung.', 'error');
                } finally {
                    this.loading = false;
                }
            },
            async saveGalleryLayout() {
                try {
                    await DeLongApi.put(`/api/admin/properties/${this.propertyId}/editorial/gallery/layout`, { layout: this.galleryLayout });
                    this.notify(`Trang cơ sở đã chuyển sang ${this.galleryLayout === 'grid' ? 'Grid' : this.galleryLayout === 'slider' ? 'Slider' : 'Mosaic'}.`);
                } catch (err) {
                    this.notify(err.message || 'Không thể lưu kiểu Gallery của cơ sở.', 'error');
                }
            },
            async saveGlobalGalleryLayout() {
                if (!this.isAdmin || !this.globalSettings || this.globalSaving) return;
                this.globalSaving = true;
                try {
                    const payload = Object.assign({}, this.globalSettings, { galleryLayout: this.globalGalleryLayout });
                    const saved = await DeLongApi.put('/api/admin/site/global/editorial/', payload);
                    this.globalSettings = Object.assign({}, this.globalSettings, saved || {}, { galleryLayout: this.globalGalleryLayout });
                    this.notify(`Trang chủ chung đã chuyển sang ${this.globalGalleryLayout === 'grid' ? 'Grid' : this.globalGalleryLayout === 'slider' ? 'Slider' : 'Mosaic'}.`);
                } catch (err) {
                    this.globalGalleryLayout = this.normalizeLayout(this.globalSettings?.galleryLayout);
                    this.notify(err.message || 'Không thể lưu kiểu Gallery trang chủ chung.', 'error');
                } finally {
                    this.globalSaving = false;
                }
            },
            async uploadAsset(file) {
                const form = new FormData();
                form.append('file', file);
                return await DeLongApi.postForm(`/api/admin/properties/${this.propertyId}/site/assets/section`, form);
            },
            async uploadGallery(event) {
                const files = Array.from(event.target.files || []);
                event.target.value = '';
                if (!files.length || this.uploading) return;
                this.uploading = true;
                try {
                    for (const file of files) {
                        const asset = await this.uploadAsset(file);
                        const created = await DeLongApi.post(`/api/admin/properties/${this.propertyId}/editorial/gallery`, {
                            imageUrl: asset.url,
                            altText: this.propertyName,
                            caption: '',
                            isPublished: true
                        });
                        this.gallery.push(created);
                    }
                    this.notify(`Đã tải ${files.length} ảnh.`);
                } catch (err) {
                    this.notify(err.message || 'Không thể tải gallery.', 'error');
                } finally {
                    this.uploading = false;
                }
            },
            async saveGallery(item) {
                try {
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/editorial/gallery/${item.id}`, {
                        imageUrl: item.imageUrl,
                        altText: item.altText,
                        caption: item.caption,
                        isPublished: item.isPublished
                    });
                    Object.assign(item, saved);
                    this.notify('Đã lưu ảnh.');
                } catch (err) {
                    this.notify(err.message || 'Không thể lưu ảnh.', 'error');
                }
            },
            async deleteGallery(item) {
                if (!window.confirm('Xóa ảnh này khỏi gallery?')) return;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/editorial/gallery/${item.id}`);
                    this.gallery = this.gallery.filter(x => x.id !== item.id);
                    this.notify('Đã xóa ảnh.');
                } catch (err) {
                    this.notify(err.message || 'Không thể xóa ảnh.', 'error');
                }
            },
            openPost(post) {
                postQuill = null;
                if (post) {
                    this.postEditor = { open: true, id: post.id };
                    this.postForm = {
                        slug: post.slug || '',
                        title: post.title || '',
                        excerpt: post.excerpt || '',
                        bodyHtml: post.bodyHtml || '',
                        coverImageUrl: post.coverImageUrl || '',
                        isPublished: post.isPublished === true
                    };
                } else {
                    this.postEditor = { open: true, id: null };
                    this.postForm = { slug: '', title: '', excerpt: '', bodyHtml: '', coverImageUrl: '', isPublished: false };
                }
                this.$nextTick(() => this.initPostEditor());
            },
            initPostEditor() {
                if (!this.$refs.postQuillEditor || typeof Quill === 'undefined') return;
                postQuill = new Quill(this.$refs.postQuillEditor, {
                    theme: 'snow',
                    placeholder: 'Viết nội dung bài: câu chuyện, gợi ý trải nghiệm, hướng dẫn chọn phòng hoặc thông tin hữu ích cho khách...',
                    modules: {
                        toolbar: {
                            container: [
                                [{ header: [2, 3, false] }],
                                ['bold', 'italic', 'blockquote'],
                                [{ list: 'ordered' }, { list: 'bullet' }],
                                ['link', 'image'],
                                ['clean']
                            ],
                            handlers: {
                                image: () => this.$refs.postBodyImageInput?.click()
                            }
                        }
                    }
                });
                if (this.postForm.bodyHtml) {
                    postQuill.clipboard.dangerouslyPasteHTML(this.postForm.bodyHtml, 'silent');
                }
                postQuill.on('text-change', () => this.syncPostBody());
            },
            syncPostBody() {
                if (!postQuill) return;
                const html = postQuill.root.innerHTML;
                this.postForm.bodyHtml = html === '<p><br></p>' ? '' : html;
            },
            closePost() {
                if (this.saving) return;
                this.syncPostBody();
                postQuill = null;
                this.postEditor = { open: false, id: null };
            },
            async uploadPostCover(event) {
                const file = event.target.files?.[0];
                event.target.value = '';
                if (!file || this.uploadingCover) return;
                this.uploadingCover = true;
                try {
                    const asset = await this.uploadAsset(file);
                    this.postForm.coverImageUrl = asset.url;
                    this.notify('Đã tải ảnh cover.');
                } catch (err) {
                    this.notify(err.message || 'Không thể tải ảnh cover.', 'error');
                } finally {
                    this.uploadingCover = false;
                }
            },
            async uploadPostBodyImage(event) {
                const input = event.target;
                const file = input.files?.[0];
                input.value = '';
                if (!file || !postQuill || this.uploadingBody) return;
                this.uploadingBody = true;
                try {
                    const asset = await this.uploadAsset(file);
                    const selection = postQuill.getSelection(true);
                    const index = selection ? selection.index : Math.max(0, postQuill.getLength() - 1);
                    postQuill.insertEmbed(index, 'image', asset.url, 'user');
                    postQuill.setSelection(index + 1, 0, 'silent');
                    this.syncPostBody();
                    this.notify('Đã chèn ảnh vào bài viết.');
                } catch (err) {
                    this.notify(err.message || 'Không thể chèn ảnh vào bài viết.', 'error');
                } finally {
                    this.uploadingBody = false;
                }
            },
            async savePost() {
                if (this.saving) return;
                const title = String(this.postForm.title || '').trim();
                if (!title) return this.notify('Tiêu đề bài viết là bắt buộc.', 'error');
                this.syncPostBody();
                this.saving = true;
                try {
                    const payload = Object.assign({}, this.postForm, { title });
                    const saved = this.postEditor.id
                        ? await DeLongApi.put(`/api/admin/properties/${this.propertyId}/editorial/posts/${this.postEditor.id}`, payload)
                        : await DeLongApi.post(`/api/admin/properties/${this.propertyId}/editorial/posts`, payload);
                    const index = this.posts.findIndex(x => x.id === saved.id);
                    if (index >= 0) this.posts.splice(index, 1, saved); else this.posts.unshift(saved);
                    postQuill = null;
                    this.postEditor = { open: false, id: null };
                    this.notify('Đã lưu bài viết.');
                } catch (err) {
                    this.notify(err.message || 'Không thể lưu bài viết.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            async deletePost() {
                if (!this.postEditor.id || !window.confirm('Xóa bài viết này?')) return;
                this.saving = true;
                try {
                    await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/editorial/posts/${this.postEditor.id}`);
                    this.posts = this.posts.filter(x => x.id !== this.postEditor.id);
                    postQuill = null;
                    this.postEditor = { open: false, id: null };
                    this.notify('Đã xóa bài viết.');
                } catch (err) {
                    this.notify(err.message || 'Không thể xóa bài viết.', 'error');
                } finally {
                    this.saving = false;
                }
            },
            notify(message, type = 'success') {
                this.toast = { show: true, message, type };
                clearTimeout(this.toastTimer);
                this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2800);
            }
        }
    }).mount(root);
})();
