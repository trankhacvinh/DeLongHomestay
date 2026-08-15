(function () {
    const root = document.getElementById('editorial-global');
    if (!root || !window.Vue) return;
    const initial = JSON.parse(document.getElementById('editorial-global-data')?.textContent || '{}');
    const { createApp } = Vue;
    createApp({
        data() {
  return {
      tab: initial.tab === 'blog' ? 'blog' : 'gallery', properties: initial.properties || [], gallery: [], posts: [], loading: true, saving: false,
      settings: { galleryEnabled: true, galleryMode: 'all', galleryPropertyIds: [], galleryItemIds: [], galleryLimit: 8, galleryTitle: 'Một vài khoảnh khắc tại De Long', galleryLayout: 'mosaic', blogEnabled: true, blogMode: 'all', blogPropertyIds: [], blogPostIds: [], blogLimit: 3, blogTitle: 'Gợi ý cho chuyến nghỉ của bạn' },
      toast: { show: false, type: 'success', message: '' }
  };
        },
        mounted() { this.load(); },
        methods: {
  includes(list, id) { return Array.isArray(list) && list.some(x => String(x) === String(id)); },
  toggle(key, id) { const list = Array.isArray(this.settings[key]) ? [...this.settings[key]] : []; const i = list.findIndex(x => String(x) === String(id)); if (i >= 0) list.splice(i, 1); else list.push(id); this.settings[key] = list; },
  async load() {
      this.loading = true;
      try { const data = await DeLongApi.get('/api/admin/site/global/editorial/'); this.settings = Object.assign({}, this.settings, data.settings || {}); this.gallery = data.gallery || []; this.posts = data.posts || []; }
      catch (err) { this.notify(err.message || 'Không thể tải cấu hình.', 'error'); }
      finally { this.loading = false; }
  },
  async save() {
      if (this.saving) return; this.saving = true;
      try { const saved = await DeLongApi.put('/api/admin/site/global/editorial/', this.settings); this.settings = Object.assign({}, this.settings, saved || {}); this.notify(this.tab === 'gallery' ? 'Đã lưu Gallery trang chủ.' : 'Đã lưu Blog trang chủ.'); }
      catch (err) { this.notify(err.message || 'Không thể lưu cấu hình.', 'error'); }
      finally { this.saving = false; }
  },
  notify(message, type = 'success') { this.toast = { show: true, message, type }; clearTimeout(this.toastTimer); this.toastTimer = setTimeout(() => { this.toast.show = false; }, 2800); }
        }
    }).mount(root);
})();
