(function () {
    const root = document.getElementById('editorial-admin');
    if (!root || !window.Vue) return;
    const initial = JSON.parse(document.getElementById('editorial-admin-data')?.textContent || '{}');
    const { createApp } = Vue;
    createApp({
        data() {
            return {
                propertyId: initial.propertyId,
                propertyName: initial.propertyName || '',
                siteSlug: initial.siteSlug || '',
                isAdmin: initial.isAdmin === true,
                tab: 'gallery',
                gallery: [],
                posts: [],
                loading: true,
                uploading: false,
                saving: false,
                postEditor: { open: false, id: null },
                postForm: { slug: '', title: '', excerpt: '', bodyHtml: '', coverImageUrl: '', isPublished: false },
                toast: { show: false, type: 'success', message: '' }
            };
        },
        mounted() { this.load(); },
        methods: {
            async load() {
                this.loading = true;
                try {
                    const data = await DeLongApi.get(`/api/admin/properties/${this.propertyId}/editorial/`);
                    this.gallery = data.gallery || [];
                    this.posts = data.posts || [];
                } catch (err) { this.notify(err.message || 'Không thể tải Gallery & Blog.', 'error'); }
                finally { this.loading = false; }
            },
            async uploadAsset(file) {
                const form = new FormData(); form.append('file', file);
                return await DeLongApi.postForm(`/api/admin/properties/${this.propertyId}/site/assets/section`, form);
            },
            async uploadGallery(event) {
                const files = Array.from(event.target.files || []); event.target.value = '';
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
                } catch (err) { this.notify(err.message || 'Không thể tải gallery.', 'error'); }
                finally { this.uploading = false; }
            },
            async saveGallery(item) {
                try {
                    const saved = await DeLongApi.put(`/api/admin/properties/${this.propertyId}/editorial/gallery/${item.id}`, {
                        imageUrl: item.imageUrl,
                        altText: item.altText,
                        caption: item.caption,
                        isPublished: item.isPublished
                    });
                    Object.assign(item, saved); this.notify('Đã lưu ảnh.');
                } catch (err) { this.notify(err.message || 'Không thể lưu ảnh.', 'error'); }
            },
            async deleteGallery(item) {
                if (!window.confirm('Xóa ảnh này khỏi gallery?')) return;
                try { await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/editorial/gallery/${item.id}`); this.gallery = this.gallery.filter(x => x.id !== item.id); this.notify('Đã xóa ảnh.'); }
                catch (err) { this.notify(err.message || 'Không thể xóa ảnh.', 'error'); }
            },
            openPost(post) {
                if (post) {
                    this.postEditor = { open: true, id: post.id };
                    this.postForm = { slug: post.slug || '', title: post.title || '', excerpt: post.excerpt || '', bodyHtml: post.bodyHtml || '', coverImageUrl: post.coverImageUrl || '', isPublished: post.isPublished === true };
                } else {
                    this.postEditor = { open: true, id: null };
                    this.postForm = { slug: '', title: '', excerpt: '', bodyHtml: '<p></p>', coverImageUrl: '', isPublished: false };
                }
            },
            closePost() { if (!this.saving) this.postEditor.open = false; },
            async uploadPostCover(event) {
                const file = event.target.files?.[0]; event.target.value = '';
                if (!file) return;
                try { const asset = await this.uploadAsset(file); this.postForm.coverImageUrl = asset.url; this.notify('Đã tải ảnh cover.'); }
                catch (err) { this.notify(err.message || 'Không thể tải ảnh cover.', 'error'); }
            },
            async savePost() {
                if (this.saving) return;
                this.saving = true;
                try {
                    const payload = Object.assign({}, this.postForm);
                    const saved = this.postEditor.id
                        ? await DeLongApi.put(`/api/admin/properties/${this.propertyId}/editorial/posts/${this.postEditor.id}`, payload)
                        : await DeLongApi.post(`/api/admin/properties/${this.propertyId}/editorial/posts`, payload);
                    const index = this.posts.findIndex(x => x.id === saved.id);
                    if (index >= 0) this.posts.splice(index, 1, saved); else this.posts.unshift(saved);
                    this.postEditor.open = false; this.notify('Đã lưu bài viết.');
                } catch (err) { this.notify(err.message || 'Không thể lưu bài viết.', 'error'); }
                finally { this.saving = false; }
            },
            async deletePost() {
                if (!this.postEditor.id || !window.confirm('Xóa bài viết này?')) return;
                this.saving = true;
                try { await DeLongApi.delete(`/api/admin/properties/${this.propertyId}/editorial/posts/${this.postEditor.id}`); this.posts = this.posts.filter(x => x.id !== this.postEditor.id); this.postEditor.open = false; this.notify('Đã xóa bài viết.'); }
                catch (err) { this.notify(err.message || 'Không thể xóa bài viết.', 'error'); }
                finally { this.saving = false; }
            },
            notify(message, type = 'success') {
                this.toast = { show: true, message, type };
                clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2800);
            }
        }
    }).mount(root);
})();
